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
    public async Task DispatchAsync_WhenPublishingIsOff_ShouldLog()
    {
        //Arrange
        var outboxEvent = _fixture.Create<OutboxEvent>();
        _benchmarkOptions.Setup(i => i.Value)
            .Returns(new BenchmarkOptions { IsPublishingOn = false});

        //Act
        await _sut.DispatchAsync(outboxEvent);

        //Assert
        _logger.Verify(
            x => x.Log(
                It.Is<LogLevel>(y => y == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().StartsWith("Publishing closed.")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)), Times.Once);  
    }
        
    [Test]
    public async Task DispatchAsync_WhenExceptionOccurred_ShouldInsertMissingEvent()
    {
        //Arrange
        var outboxEvent = _fixture.Create<OutboxEvent>();

        //Act & Assert
        await _sut.DispatchAsync(outboxEvent);
            
        _logger.Verify(
            x => x.Log(
                It.Is<LogLevel>(y => y == LogLevel.Error),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().StartsWith("Message broker is unavailable.")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)), Times.Once);  
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

        //Act
        await _sut.DispatchAsync(outboxEvent);

        //Assert
        _logger.Verify(
            x => x.Log(
                It.Is<LogLevel>(y => y == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().StartsWith("Event published.")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)), Times.Once);  
    }
        
    [Test]
    public async Task DispatchMissingAsync_WhenPublishingIsOff_ShouldLog()
    {
        //Arrange
        var mappedMissingEvent = _fixture.Create<MappedMissingEvent>();
        _benchmarkOptions.Setup(i => i.Value)
            .Returns(new BenchmarkOptions { IsPublishingOn = false});

        //Act
        await _sut.DispatchMissingAsync(mappedMissingEvent);

        //Assert
        _logger.Verify(
            x => x.Log(
                It.Is<LogLevel>(y => y == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().StartsWith("Publishing closed.")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)), Times.Once);  
    }
        
    [Test]
    public async Task DispatchMissingAsync_WhenMessageBrokerUnavailableExceptionOccurred_ShouldUpdateRetryCountOfMissingEvent()
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
            .ThrowsAsync(new MessageBrokerUnavailableException());

        //Act & Assert
        await _sut.DispatchMissingAsync(mappedMissingEvent);
            
        _logger.Verify(
            x => x.Log(
                It.Is<LogLevel>(y => y == LogLevel.Error),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().StartsWith("Message broker is unavailable.")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)), Times.Once);  
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

        //Act & Assert
        await _sut.DispatchMissingAsync(mappedMissingEvent);
            
        _logger.Verify(
            x => x.Log(
                It.Is<LogLevel>(y => y == LogLevel.Error),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().StartsWith("Message broker delivery failed.")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)), Times.Once);
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

        //Act & Assert
        await _sut.DispatchMissingAsync(mappedMissingEvent);
            
        _logger.Verify(
            x => x.Log(
                It.Is<LogLevel>(y => y == LogLevel.Error),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().StartsWith("An error occurred.")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)), Times.Once);  
        _missingEventRepository.Verify(x => x.UpdateRetryCountAndExceptionThrownAsync( It.IsAny<MissingEvent>()), Times.Once);
    }
}