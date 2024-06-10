using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using Moq;
using NUnit.Framework;
using PollingOutboxPublisher.Coordinators.MissingCoordinator;
using PollingOutboxPublisher.Coordinators.MissingCoordinator.Services.Interfaces;
using PollingOutboxPublisher.Coordinators.Services.Interfaces;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Tests.Coordinators.MissingCoordinator;

public class MissingEventsCoordinatorTests
{
    private MissingEventsCoordinator _sut; // System Under Test
    private Mock<IOutboxDispatcher> _outboxDispatcherMock;
    private Mock<IPollingMissingQueue> _pollingMissingQueueMock;
    private Mock<IMissingEventCleaner> _missingEventCleanerMock;
    private Mock<IMasterPodChecker> _masterPodCheckerMock;
    private IFixture _fixture;

    [SetUp]
    public void Setup()
    {
        _outboxDispatcherMock = new Mock<IOutboxDispatcher>();
        _pollingMissingQueueMock = new Mock<IPollingMissingQueue>();
        _missingEventCleanerMock = new Mock<IMissingEventCleaner>();
        _masterPodCheckerMock = new Mock<IMasterPodChecker>();
        _fixture = new Fixture();
            
        _sut = new MissingEventsCoordinator(
            _outboxDispatcherMock.Object,
            _pollingMissingQueueMock.Object,
            _missingEventCleanerMock.Object,
            _masterPodCheckerMock.Object
        );
    }
        
    [Test]
    public async Task StartAsync_WhenCancellationRequested_ShouldReturn()
    {
        // Arrange
        var cancellationToken = new CancellationToken(true);

        // Act
        await _sut.StartAsync(cancellationToken);

        // Assert
        _masterPodCheckerMock.Verify(x => x.IsMasterPodAsync(cancellationToken), Times.Never);
    }
        
    [Test]
    public async Task StartAsync_WhenIsMasterPodAsyncReturnFalse_ShouldReturn()
    {
        // Arrange
        var cancellationToken = new CancellationToken(true);

        // Act
        await _sut.StartAsync(cancellationToken);

        // Assert
        _pollingMissingQueueMock.Verify(x => x.DequeueAsync(cancellationToken), Times.Never);
    }

    [Test]
    public async Task StartAsync_WhenMappedMissingEventsIsNotNull_ShouldDispatchAndCleanMissingEvents()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var mappedMissingEvents = _fixture.CreateMany<MappedMissingEvent>(3).ToList();
        var callCount = 0;

        _masterPodCheckerMock
            .Setup(x => x.IsMasterPodAsync(cancellationToken))
            .ReturnsAsync(() => callCount++ < 1); // Returns true for the first call, then false
        _pollingMissingQueueMock
            .Setup(x => x.DequeueAsync(cancellationToken))
            .ReturnsAsync((It.IsAny<MissingEvent[]>(), It.IsAny<MissingEvent[]>(), mappedMissingEvents, It.IsAny<OutboxEvent[]>()));

        // Act
        await _sut.StartAsync(cancellationToken);

        // Assert
        _outboxDispatcherMock.Verify(x => x.DispatchMissingAsync(It.IsAny<MappedMissingEvent>()), Times.Exactly(3));
        _missingEventCleanerMock.Verify(x => x.HandleNonMatchedMissingEventsAsync(It.IsAny<OutboxEvent[]>(), It.IsAny<MissingEvent[]>()), Times.Once);
        _missingEventCleanerMock.Verify(x => x.CleanMissingEventsHaveOutboxEventAsync(It.IsAny<MissingEvent[]>(),
            It.IsAny<List<MappedMissingEvent>>()), Times.Once);
    }

    [Test]
    public async Task StartAsync_WhenMissingEventsIsNotNull_ShouldCallCleanMissingEventsNotHaveOutboxEventAsync()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        var missingEvents = _fixture.CreateMany<MissingEvent>(3).ToArray();
        var outboxEvents = _fixture.CreateMany<OutboxEvent>(3).ToArray();
        var callCount = 0;

        _masterPodCheckerMock
            .Setup(x => x.IsMasterPodAsync(cancellationToken))
            .ReturnsAsync(() => callCount++ < 1); // Returns true for the first call, then false
        _pollingMissingQueueMock
            .Setup(x => x.DequeueAsync(cancellationToken))
            .ReturnsAsync((It.IsAny<MissingEvent[]>(), missingEvents, It.IsAny<List<MappedMissingEvent>>(), outboxEvents));

        // Act
        await _sut.StartAsync(cancellationToken);

        // Assert
        _missingEventCleanerMock.Verify(x => x.CleanMissingEventsNotHaveOutboxEventAsync(missingEvents), Times.Once);
    }
}