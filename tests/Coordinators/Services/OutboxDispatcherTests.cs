using System;
using System.Threading.Tasks;
using AutoFixture;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Coordinators.Services;
using PollingOutboxPublisher.Coordinators.Services.Interfaces;
using PollingOutboxPublisher.Database.Repositories.Interfaces;
using PollingOutboxPublisher.Exceptions;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Tests.Coordinators.Services;

public class OutboxDispatcherTests
{
    private Fixture _fixture;
    private Mock<IOptions<BenchmarkOptions>> _benchmarkOptions;
    private Mock<IKafkaService> _kafkaService;
    private Mock<ILogger<OutboxDispatcher>> _logger;
    private Mock<IMissingEventRepository> _missingEventRepository;
    private Mock<IOptions<WorkerSettings>> _workerSettings;
    private OutboxDispatcher _sut;


    [SetUp]
    public void Setup()
    {
        _fixture = new Fixture();
        _benchmarkOptions = new Mock<IOptions<BenchmarkOptions>>();
        _kafkaService = new Mock<IKafkaService>();
        _logger = new Mock<ILogger<OutboxDispatcher>>();
        _missingEventRepository = new Mock<IMissingEventRepository>();
        _workerSettings = new Mock<IOptions<WorkerSettings>>();
        _workerSettings.Setup(i => i.Value)
            .Returns(new WorkerSettings { MissingEventsMaxRetryCount = 5, BrokerErrorsMaxRetryCount = 5});

        _sut = new OutboxDispatcher(_logger.Object, _kafkaService.Object,
            _benchmarkOptions.Object, _workerSettings.Object, _missingEventRepository.Object);
    }

    [Test]
    public async Task DispatchAsync_WhenPublishingIsOff_ShouldNotThrowException()
    {
        //Arrange
        var outboxEvent = _fixture.Create<OutboxEvent>();
        _benchmarkOptions.Setup(i => i.Value)
            .Returns(new BenchmarkOptions { IsPublishingOn = false});

        //Act & Assert
        Assert.DoesNotThrowAsync(async () => await _sut.DispatchAsync(outboxEvent));
    }
        
    [Test]
    public async Task DispatchAsync_WhenExceptionOccurred_ShouldInsertMissingEvent()
    {
        //Arrange
        var outboxEvent = _fixture.Create<OutboxEvent>();

        //Act
        await _sut.DispatchAsync(outboxEvent);
            
        //Assert
        _missingEventRepository.Verify(x => x.InsertAsync( It.IsAny<MissingEvent>()), Times.Once);
    }
        
    [Test]
    public void DispatchAsync_WhenExceptionOccurredAndEventCannotBeConvertedToMissingEvent_ShouldThrowException()
    {
        //Arrange
        var outboxEvent = _fixture.Create<OutboxEvent>();
        var expectedException = new Exception("Test exception");

        _kafkaService.Setup(x => x.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>()))
            .ThrowsAsync(expectedException);

        _missingEventRepository.Setup(x => x.InsertAsync(It.IsAny<MissingEvent>()))
            .ThrowsAsync(new Exception("Database error"));

        //Act & Assert
        var exception = Assert.ThrowsAsync<Exception>(async () => 
            await _sut.DispatchAsync(outboxEvent));
    
        Assert.That(exception.Message, Is.EqualTo("Database error"));
    }
        
    [Test]
    public async Task DispatchAsync_TrueStory()
    {
        //Arrange
        var outboxEvent = _fixture.Build<OutboxEvent>()
            .With(x => x.Header, "{'test': 'test'}")
            .With(x => x.Value, "{}")
            .Create();
            
        _benchmarkOptions.Setup(i => i.Value)
            .Returns(new BenchmarkOptions { IsPublishingOn = true});

        //Act & Assert
        Assert.DoesNotThrowAsync(async () => await _sut.DispatchAsync(outboxEvent));
    }
        
    [Test]
    public async Task DispatchMissingAsync_WhenPublishingIsOff_ShouldNotThrowException()
    {
        //Arrange
        var mappedMissingEvent = _fixture.Create<MappedMissingEvent>();
        _benchmarkOptions.Setup(i => i.Value)
            .Returns(new BenchmarkOptions { IsPublishingOn = false});

        //Act & Assert
        Assert.DoesNotThrowAsync(async () => await _sut.DispatchMissingAsync(mappedMissingEvent));
    }
        
    [Test]
    public async Task DispatchMissingAsync_WhenMessageBrokerUnavailableExceptionOccurred_ShouldMarkExceptionThrown()
    {
        //Arrange
        var mappedMissingEvent = _fixture
            .Build<MappedMissingEvent>()
            .With(x => x.MissingEvent, _fixture.Build<MissingEvent>()
                .With(x => x.RetryCount, 0)
                .With(x => x.ExceptionThrown, false)
                .Create())
            .With(x => x.OutboxEvent, _fixture.Build<OutboxEvent>()
                .With(x => x.Header, "{'test': 'test'}")
                .With(x => x.Value, "{}")
                .Create())
            .Create();
        _benchmarkOptions.Setup(i => i.Value)
            .Returns(new BenchmarkOptions { IsPublishingOn = true});
            
        _kafkaService.Setup(x => x.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string,string>>()))
            .ThrowsAsync(new MessageBrokerUnavailableException());

        //Act
        var result = await _sut.DispatchMissingAsync(mappedMissingEvent);
            
        //Assert
        Assert.That(result.MissingEvent.ExceptionThrown, Is.True);
    }
        
    [Test]
    public async Task DispatchMissingAsync_WhenMessageMessageBrokerDeliveryFailedExceptionOccurred_ShouldUpdateRetryCountOfMissingEvent()
    {
        //Arrange
        var mappedMissingEvent = _fixture
            .Build<MappedMissingEvent>()
            .With(x => x.MissingEvent, _fixture.Build<MissingEvent>()
                .With(x => x.RetryCount, 0)
                .Create())
            .With(x => x.OutboxEvent, _fixture.Build<OutboxEvent>()
                .With(x => x.Header, "{'test': 'test'}")
                .With(x => x.Value, "{}")
                .Create())
            .Create();
        _benchmarkOptions.Setup(i => i.Value)
            .Returns(new BenchmarkOptions { IsPublishingOn = true});
            
        _kafkaService.Setup(x => x.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string,string>>()))
            .ThrowsAsync(new MessageBrokerDeliveryFailedException());

        //Act
        await _sut.DispatchMissingAsync(mappedMissingEvent);
            
        //Assert
        _missingEventRepository.Verify(x => x.UpdateRetryCountAndExceptionThrownAsync( It.IsAny<MissingEvent>()), Times.Once);
    }
        
    [Test]
    public async Task DispatchMissingAsync_WhenExceptionOccurred_ShouldUpdateRetryCountOfMissingEvent()
    {
        //Arrange
        var mappedMissingEvent = _fixture
            .Build<MappedMissingEvent>()
            .With(x => x.MissingEvent, _fixture.Build<MissingEvent>()
                .With(x => x.RetryCount, 0)
                .Create())
            .Create();
        _benchmarkOptions.Setup(i => i.Value)
            .Returns(new BenchmarkOptions { IsPublishingOn = true});

        //Act
        await _sut.DispatchMissingAsync(mappedMissingEvent);
            
        //Assert
        _missingEventRepository.Verify(x => x.UpdateRetryCountAndExceptionThrownAsync( It.IsAny<MissingEvent>()), Times.Once);
    }
}