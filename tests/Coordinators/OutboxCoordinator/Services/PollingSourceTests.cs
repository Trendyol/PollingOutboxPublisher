using System;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services;
using PollingOutboxPublisher.Database.Repositories.Interfaces;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Tests.Coordinators.OutboxCoordinator.Services;

public class PollingSourceTests
{
    private Fixture _fixture;
    private Mock<ILogger<PollingSource>> _logger;
    private Mock<IOutboxOffsetRepository> _outboxOffsetRepository;
    private Mock<IOutboxEventRepository> _outboxEventRepository;
    private PollingSource _sut;

    [SetUp]
    public void Setup()
    {
        _fixture = new Fixture();
        _logger = new Mock<ILogger<PollingSource>>();
        _outboxOffsetRepository = new Mock<IOutboxOffsetRepository>();
        _outboxEventRepository = new Mock<IOutboxEventRepository>();
        _sut = new PollingSource(_logger.Object, _outboxOffsetRepository.Object, _outboxEventRepository.Object);
    }

    [Test]
    public async Task GetNextAsync_WhenLatestOffsetHasNoValue_ShouldLogError()
    {
        //Arrange
        const int batchCount = 10;
            
        //Act
        await _sut.GetNextAsync(batchCount);

        //Assert
        _logger.Verify(
            x => x.Log(
                It.Is<LogLevel>(y => y == LogLevel.Error),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().StartsWith("Latest Offset doesn't found")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)), Times.Once); 
    }
        
    [Test]
    public async Task GetNextAsync_WhenNewestOffsetIsEqualOrSmallerThanLatestOffset_ShouldReturn()
    {
        //Arrange
        const int batchCount = 10;
        const int newestEventId = 5;
        const int latestOffset = 5;
            
        _outboxOffsetRepository.Setup(x => x.GetLatestOffsetAsync()).ReturnsAsync(latestOffset);
        _outboxEventRepository.Setup(x => x.GetNewestEventIdAsync( It.IsAny<long>())).ReturnsAsync(newestEventId);
            
        //Act
        await _sut.GetNextAsync(batchCount);

        //Assert
        _logger.Verify(
            x => x.Log(
                It.Is<LogLevel>(y => y == LogLevel.Error),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().StartsWith("Latest Offset doesn't found")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)), Times.Never);
        _outboxEventRepository.Verify(x => x.GetLatestEventsAsync(batchCount, newestEventId), Times.Never);
    }
        
    [Test]
    public async Task GetNextAsync_TrueStory()
    {
        //Arrange
        const int batchCount = 10;
        const int newestEventId = 10;
        const int latestOffset = 5;
            
        _outboxOffsetRepository.Setup(x => x.GetLatestOffsetAsync()).ReturnsAsync(latestOffset);
        _outboxEventRepository.Setup(x => x.GetNewestEventIdAsync( It.IsAny<long>())).ReturnsAsync(newestEventId);
            
        var outboxEvents = _fixture.CreateMany<OutboxEvent>(2).ToList();

        _outboxEventRepository
            .Setup(x => x.GetLatestEventsAsync( batchCount, newestEventId))
            .ReturnsAsync(outboxEvents);

        //Act
        await _sut.GetNextAsync(batchCount);

        //Assert
        _outboxEventRepository.Verify(x => x.GetLatestEventsAsync( batchCount, newestEventId), Times.Once);
    }
}