using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using Moq;
using NUnit.Framework;
using PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services.Interfaces;
using PollingOutboxPublisher.Coordinators.Services;
using PollingOutboxPublisher.Coordinators.Services.Interfaces;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Tests.Coordinators.OutboxCoordinator;

public class OutboxCoordinatorTests
{
    private Fixture _fixture;
    private Mock<IOutboxDispatcher> _outboxDispatcher;
    private Mock<IPollingQueue> _pollingQueue;
    private Mock<IOffsetSetter> _offsetSetter;
    private PollingOutboxPublisher.Coordinators.OutboxCoordinator.OutboxCoordinator _sut;
    private Mock<IMasterPodChecker> _masterPodChecker;
    private Mock<ICircuitBreaker> _circuitBreaker;

    [SetUp]
    public void Setup()
    {
        _fixture = new Fixture();
        _outboxDispatcher = new Mock<IOutboxDispatcher>();
        _pollingQueue = new Mock<IPollingQueue>();
        _offsetSetter = new Mock<IOffsetSetter>();
        _masterPodChecker = new Mock<IMasterPodChecker>();
        _circuitBreaker = new Mock<ICircuitBreaker>();

        _sut = new PollingOutboxPublisher.Coordinators.OutboxCoordinator.OutboxCoordinator(
            _outboxDispatcher.Object,
            _pollingQueue.Object, 
            _offsetSetter.Object, 
            _masterPodChecker.Object,
            _circuitBreaker.Object);
    }

    [Test]
    public async Task StartAsync_WhenCancellationRequested_ShouldReturn()
    {
        // Arrange
        var cancellationToken = new CancellationToken(true);

        // Act
        await _sut.StartAsync(cancellationToken);

        // Assert
        _masterPodChecker.Verify(x => x.IsMasterPodAsync(cancellationToken), Times.Never);
        _circuitBreaker.Verify(x => x.Reset(), Times.Never);
    }

    [Test]
    public async Task StartAsync_WhenIsMasterPodAsyncReturnFalse_ShouldReturn()
    {
        // Arrange
        var cancellationToken = new CancellationToken();
        _masterPodChecker.Setup(x => x.IsMasterPodAsync(cancellationToken)).ReturnsAsync(false);

        // Act
        await _sut.StartAsync(cancellationToken);

        // Assert
        _pollingQueue.Verify(x => x.DequeueAsync(cancellationToken), Times.Never);
        _circuitBreaker.Verify(x => x.Reset(), Times.Never);
    }

    [Test]
    public async Task StartAsync_WhenOutboxEventsIsEmpty_ShouldNotCallDispatchAsyncOrResetCircuitBreaker()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        var callCount = 0;
        _masterPodChecker
            .Setup(x => x.IsMasterPodAsync(cancellationToken))
            .ReturnsAsync(() => callCount++ < 1);
        _pollingQueue.Setup(x => x.DequeueAsync(cancellationToken)).ReturnsAsync([]);

        // Act
        await _sut.StartAsync(cancellationToken);

        // Assert
        _outboxDispatcher.Verify(x => x.DispatchAsync(It.IsAny<OutboxEvent>()), Times.Never);
        _offsetSetter.Verify(x => x.SetLatestOffset(It.IsAny<OutboxEvent[]>()), Times.Never);
        _circuitBreaker.Verify(x => x.Reset(), Times.Never);
    }

    [Test]
    public async Task StartAsync_WhenOutboxEventsExist_ShouldProcessAndResetCircuitBreaker()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        var outboxEvents = _fixture.CreateMany<OutboxEvent>(3).ToArray();
        var callCount = 0;

        _masterPodChecker
            .Setup(x => x.IsMasterPodAsync(cancellationToken))
            .ReturnsAsync(() => callCount++ < 1); // Returns true for the first call, then false
        _pollingQueue.Setup(x => x.DequeueAsync(cancellationToken)).ReturnsAsync(outboxEvents);

        // Act
        await _sut.StartAsync(cancellationToken);

        // Assert
        _outboxDispatcher.Verify(x => x.DispatchAsync(It.IsAny<OutboxEvent>()), Times.Exactly(outboxEvents.Length));
        _offsetSetter.Verify(x => x.SetLatestOffset(outboxEvents), Times.Once);
        _circuitBreaker.Verify(x => x.Reset(), Times.Once);
    }
}