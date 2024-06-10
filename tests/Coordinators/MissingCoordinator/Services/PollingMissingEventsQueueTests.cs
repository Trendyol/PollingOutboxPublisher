using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Coordinators.MissingCoordinator.Services;
using PollingOutboxPublisher.Coordinators.MissingCoordinator.Services.Interfaces;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Tests.Coordinators.MissingCoordinator.Services;

public class PollingMissingEventsQueueTests
{
    private PollingMissingEventsQueue _sut; // System Under Test
    private Mock<IPollingMissingEventsSource> _pollingMissingEventsSourceMock; // Mocking dependencies
    private Mock<IOptions<WorkerSettings>> _workerSettingsMock;

    [SetUp]
    public void Setup()
    {
        _pollingMissingEventsSourceMock = new Mock<IPollingMissingEventsSource>();
        _workerSettingsMock = new Mock<IOptions<WorkerSettings>>();
        _workerSettingsMock.Setup(x => x.Value).Returns(new WorkerSettings
            { MissingEventsWaitDuration = 1000, MissingEventsBatchSize = 10, MissingEventsMaxRetryCount = 5 });
        _sut = new PollingMissingEventsQueue(_pollingMissingEventsSourceMock.Object, _workerSettingsMock.Object);
    }

    [Test]
    public async Task DequeueAsync_WhenThereIsNoMissingEvents_ShouldDelayAndReturnNullTuple()
    {
        // Arrange
        var cancellationToken = new System.Threading.CancellationToken();
        _pollingMissingEventsSourceMock.Setup(x => x.GetMissingEventsAsync(It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<MissingEvent>());

        // Act
        var result = await _sut.DequeueAsync(cancellationToken);

        // Assert
        result.Item1.Should().BeNull();
        result.Item2.Should().BeNull();
        result.Item3.Should().BeNull();
        result.Item4.Should().BeNull();
    }

    [Test]
    public async Task DequeueAsync_WhenThereIsNoOutboxEvents_ShouldDelayAndReturnNullTuple()
    {
        // Arrange
        var cancellationToken = new System.Threading.CancellationToken();
        _pollingMissingEventsSourceMock.Setup(x => x.GetMissingEventsAsync(It.IsAny<int>()))
            .ReturnsAsync(new[] { new MissingEvent() });
        _pollingMissingEventsSourceMock.Setup(x => x.GetNextOutboxEventsAsync(It.IsAny<MissingEvent[]>()))
            .ReturnsAsync(Array.Empty<OutboxEvent>());

        // Act
        var result = await _sut.DequeueAsync(cancellationToken);

        // Assert
        result.Item1.Should().NotBeNull();
        result.Item2.Should().NotBeNull();
        result.Item3.Should().BeNull();
        result.Item4.Should().BeNull();
    }
        
    [Test]
    public async Task DequeueAsync_TrueStory()
    {
        // Arrange
        var cancellationToken = new System.Threading.CancellationToken();
        var missingEvents = new[] { new MissingEvent() };
        var outboxEvents = new[] { new OutboxEvent() };
        _pollingMissingEventsSourceMock.Setup(x => x.GetMissingEventsAsync(It.IsAny<int>()))
            .ReturnsAsync(missingEvents);
        _pollingMissingEventsSourceMock.Setup(x => x.GetNextOutboxEventsAsync(It.IsAny<MissingEvent[]>()))
            .ReturnsAsync(outboxEvents);

        // Act
        var result = await _sut.DequeueAsync(cancellationToken);

        // Assert
        result.Item1.Should().NotBeNull();
        result.Item2.Should().NotBeNull();
        result.Item3.Should().NotBeNull();
        result.Item4.Should().NotBeNull();
    }
}