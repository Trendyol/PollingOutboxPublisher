using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Coordinators.MissingCoordinator.Services;
using PollingOutboxPublisher.Database.Repositories.Interfaces;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Tests.Coordinators.MissingCoordinator.Services;

public class MissingEventCleanerTests
{
    private Mock<ILogger<MissingEventCleaner>> _logger;
    private Mock<IMissingEventRepository> _missingEventRepository;
    private Mock<IExceededEventRepository> _exceededEventRepository;
    private Mock<IOptions<WorkerSettings>> _workerSettings;
    private Fixture _fixture;
    private MissingEventCleaner _sut;

    [SetUp]
    public void Setup()
    {
        _exceededEventRepository = new Mock<IExceededEventRepository>();
        _missingEventRepository = new Mock<IMissingEventRepository>();
        _logger = new Mock<ILogger<MissingEventCleaner>>();
        _fixture = new Fixture();
        _workerSettings = new Mock<IOptions<WorkerSettings>>();
        _workerSettings.Setup(i => i.Value)
            .Returns(new WorkerSettings { MissingEventsMaxRetryCount = 5, BrokerErrorsMaxRetryCount = 5});

        _sut = new MissingEventCleaner(_workerSettings.Object, _logger.Object,
            _missingEventRepository.Object, _exceededEventRepository.Object);
    }

    [Test]
    public async Task CleanMissingEventsHaveOutboxEventAsync_WhenMissingEventDontHaveExceptionIdListIsNotEmpty_ShouldDelete()
    {
        //Arrange
        var missingEvents = _fixture.CreateMany<MissingEvent>(5).ToArray();
        var missingEventsWithException = _fixture.Build<MissingEvent>()
            .With(x => x.ExceptionThrown, false)
            .Create();
        var results = _fixture.Build<MappedMissingEvent>()
            .With(x => x.MissingEvent, missingEventsWithException)
            .CreateMany<MappedMissingEvent>(5).ToList();

        //Act
        await _sut.CleanMissingEventsHaveOutboxEventAsync(missingEvents, results);

        //Assert
        _missingEventRepository.Verify(x => x.DeleteMissingEventsAsync( It.IsAny<List<long>>()), Times.AtLeast(1));
        _exceededEventRepository.Verify(x => x.InsertAsync( It.IsAny<ExceededEvent>()), Times.AtLeast(1));
    }
        
    [Test]
    public async Task CleanMissingEventsHaveOutboxEventAsync_WhenRetryLimitExceededMissingEventsIsEmpty_ShouldReturn()
    {
        //Arrange
        var missingEventsWithException = _fixture.Build<MissingEvent>()
            .With(x => x.RetryCount, _workerSettings.Object.Value.BrokerErrorsMaxRetryCount - 1)
            .With(x => x.ExceptionThrown, true)
            .Create();
        var results = _fixture.Build<MappedMissingEvent>()
            .With(x => x.MissingEvent, missingEventsWithException)
            .CreateMany<MappedMissingEvent>(5).ToList();
        var missingEvents = new[] { missingEventsWithException };
            
        //Act
        await _sut.CleanMissingEventsHaveOutboxEventAsync(missingEvents, results);

        //Assert
        _missingEventRepository.Verify(x => x.DeleteMissingEventsAsync( It.IsAny<List<long>>()), Times.Never);
    }
        
    [Test]
    public async Task HandleNonMatchedMissingEventsAsync_WhenThereAreNonMatchedMissingEvents_ShouldIncrementRetryCount()
    {
        //Arrange
        var outboxEvents = _fixture.CreateMany<OutboxEvent>(5).ToArray();

        var retryableMissingEvents = outboxEvents.Select(outboxEvent => _fixture.Build<MissingEvent>()
                .With(x => x.Id, outboxEvent.Id)
                .With(x => x.RetryCount, 1)
                .Create())
            .ToList();
            
        var nonMatchedId = outboxEvents.Max(x => x.Id) + 1;
        retryableMissingEvents.Add(_fixture.Build<MissingEvent>()
            .With(x => x.Id, nonMatchedId)
            .With(x => x.RetryCount, 1)
            .Create());

        //Act
        await _sut.HandleNonMatchedMissingEventsAsync(outboxEvents, retryableMissingEvents.ToArray());

        //Assert
        _missingEventRepository.Verify(x =>
                x.IncrementRetryCountAsync(
                     
                    It.Is<IEnumerable<long>>(x => x.Contains(nonMatchedId))), 
            Times.Once);
    }
        
    [Test]
    public async Task HandleNonMatchedMissingEventsAsync_WhenRetryableMissingEventsListIsNull_ShouldReturn()
    {
        //Arrange
        var outboxEvents = _fixture.CreateMany<OutboxEvent>(5).ToArray();
            
        //Act
        await _sut.HandleNonMatchedMissingEventsAsync(outboxEvents, null);

        //Assert
        _missingEventRepository.Verify(x =>
                x.IncrementRetryCountAsync(
                         
                    It.IsAny<IEnumerable<long>>()), 
            Times.Never());
    }
        
    [Test]
    public async Task HandleNonMatchedMissingEventsAsync_WhenOutboxEventsListIsNull_ShouldIncrementForAllRetryableMissingEvents()
    {
        //Arrange
        var retryableMissingEvents = _fixture.CreateMany<MissingEvent>(5).ToArray();

        //Act
        await _sut.HandleNonMatchedMissingEventsAsync(null, retryableMissingEvents.ToArray());

        //Assert
        _missingEventRepository.Verify(x =>
                x.IncrementRetryCountAsync(
                         
                    It.Is<IEnumerable<long>>(x => x.Count()==retryableMissingEvents.Length)), 
            Times.Once());
    }

    [Test]
    public async Task CleanMissingEventsNotHaveOutboxEventAsync_WhenRetryLimitExceededEventNotExists_ShouldReturn()
    {
        //Arrange
        var retryCount = 0;
            
        var retryableMissingEvents = _fixture.Build<MissingEvent>()
            .With(x => x.RetryCount, retryCount)
            .CreateMany(5)
            .ToArray();

        //Act & Assert
        Assert.DoesNotThrowAsync(async () => await _sut.CleanMissingEventsNotHaveOutboxEventAsync(retryableMissingEvents));
    }

    [Test]
    public async Task CleanMissingEventsNotHaveOutboxEventAsync_WhenRetryLimitExceededEventExists_ShouldMoveRecordsToExceededTable()
    {
        //Arrange
        var retryCount = 5;
            
        var nonRetryableMissingEvents = _fixture.Build<MissingEvent>()
            .With(x => x.RetryCount, retryCount)
            .CreateMany(5)
            .ToArray();

        //Act
        await _sut.CleanMissingEventsNotHaveOutboxEventAsync(nonRetryableMissingEvents);

        //Assert
        _exceededEventRepository.Verify(x => x.InsertAsync( It.IsAny<ExceededEvent>()), Times.Exactly(nonRetryableMissingEvents.Length));
        _missingEventRepository.Verify(x => x.DeleteMissingEventsAsync( It.IsAny<List<long>>()), Times.Once);
    }
}