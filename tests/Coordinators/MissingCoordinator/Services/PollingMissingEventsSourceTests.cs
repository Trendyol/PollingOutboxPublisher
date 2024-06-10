using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using PollingOutboxPublisher.Coordinators.MissingCoordinator.Services;
using PollingOutboxPublisher.Database.Repositories.Interfaces;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Tests.Coordinators.MissingCoordinator.Services;

public class PollingMissingEventsSourceTests
{
    private PollingMissingEventsSource _sut; // System Under Test
    private Mock<ILogger<PollingMissingEventsSource>> _loggerMock;
    private Mock<IMissingEventRepository> _missingEventRepositoryMock;
    private Mock<IOutboxEventRepository> _outboxEventRepositoryMock;
    private IFixture _fixture;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<PollingMissingEventsSource>>();
        _missingEventRepositoryMock = new Mock<IMissingEventRepository>();
        _outboxEventRepositoryMock = new Mock<IOutboxEventRepository>();
        _fixture = new Fixture();

        _sut = new PollingMissingEventsSource(
            _loggerMock.Object,
            _missingEventRepositoryMock.Object,
            _outboxEventRepositoryMock.Object
        );
    }

    [Test]
    public async Task GetMissingEventsAsync_WhenMissingEventsIsEmpty_ShouldReturnEmptyArray()
    {
        // Arrange
        var expectedMissingEvents = new List<MissingEvent>();
        _missingEventRepositoryMock.Setup(m => m.GetMissingEventsAsync(It.IsAny<int>()))
            .ReturnsAsync(expectedMissingEvents);

        // Act
        var result = await _sut.GetMissingEventsAsync(10);

        // Assert
        result.Should().BeEmpty();
        _missingEventRepositoryMock.Verify(m => m.GetMissingEventsAsync(It.IsAny<int>()), Times.Once);
    }

    [Test]
    public async Task GetMissingEventsAsync_TrueStory()
    {
        // Arrange
        var expectedMissingEvents = _fixture.CreateMany<MissingEvent>(3).ToList();
        _missingEventRepositoryMock.Setup(m => m.GetMissingEventsAsync(It.IsAny<int>()))
            .ReturnsAsync(expectedMissingEvents);

        // Act
        var result = await _sut.GetMissingEventsAsync(10);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(expectedMissingEvents);
        _missingEventRepositoryMock.Verify(m => m.GetMissingEventsAsync(It.IsAny<int>()), Times.Once);
    }

    [Test]
    public async Task GetNextOutboxEventsAsync_TrueStory()
    {
        // Arrange
        var retryableMissingEvents = _fixture.CreateMany<MissingEvent>(3).ToArray();
        var outboxEvents = _fixture.CreateMany<OutboxEvent>(3).ToList();
        _outboxEventRepositoryMock.Setup(m => m.GetOutboxEventsAsync(It.IsAny<long[]>()))
            .ReturnsAsync(outboxEvents);

        // Act
        var result = await _sut.GetNextOutboxEventsAsync(retryableMissingEvents);

        // Assert
        result.Should().HaveCount(3);
        _outboxEventRepositoryMock.Verify(m => m.GetOutboxEventsAsync(It.IsAny<long[]>()), Times.Once);
    }
}