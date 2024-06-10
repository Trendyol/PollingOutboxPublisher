using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using Moq;
using NUnit.Framework;
using PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services.Interfaces;
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

    [SetUp]
    public void Setup()
    {
        _fixture = new Fixture();
        _outboxDispatcher = new Mock<IOutboxDispatcher>();
        _pollingQueue = new Mock<IPollingQueue>();
        _offsetSetter = new Mock<IOffsetSetter>();
        _masterPodChecker = new Mock<IMasterPodChecker>();

        _sut = new PollingOutboxPublisher.Coordinators.OutboxCoordinator.OutboxCoordinator(_outboxDispatcher.Object,
            _pollingQueue.Object, _offsetSetter.Object, _masterPodChecker.Object);
    }

    [Test]
    public async Task StartStartAsync_WhenOutboxEventsIsEmpty_ShouldNotCallDispatchAsync()
    {
        //Arrange
        var cancellationToken = new CancellationToken();
        _pollingQueue.Setup(x => x.DequeueAsync(cancellationToken)).ReturnsAsync(Array.Empty<OutboxEvent>());

        //Act
        await _sut.StartAsync(cancellationToken);

        //Assert
        _outboxDispatcher.Verify(x => x.DispatchAsync(It.IsAny<OutboxEvent>()), Times.Never);
    }

    [Test]
    public async Task StartStartAsync_TrueStory()
    {
        //Arrange
        var cancellationToken = new CancellationToken();
        var missingEvents = _fixture.CreateMany<MissingEvent>(3).ToArray();
        var outboxEvents = _fixture.CreateMany<OutboxEvent>(3).ToArray();
        var callCount = 0;

        _masterPodChecker
            .Setup(x => x.IsMasterPodAsync(cancellationToken))
            .ReturnsAsync(() => callCount++ < 1); // Returns true for the first call, then false
        _pollingQueue.Setup(x => x.DequeueAsync(cancellationToken)).ReturnsAsync(outboxEvents);

        //Act
        await _sut.StartAsync(cancellationToken);

        //Assert
        _outboxDispatcher.Verify(x => x.DispatchAsync(It.IsAny<OutboxEvent>()), Times.Exactly(outboxEvents.Length));
    }
}