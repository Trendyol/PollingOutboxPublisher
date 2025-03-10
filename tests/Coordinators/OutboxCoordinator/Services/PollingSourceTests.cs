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
    public async Task GetNextAsync_WhenLatestOffsetHasNoValue_ShouldNotThrowException()
    {
        //Arrange
        const int batchCount = 10;
            
        //Act & Assert
        Assert.DoesNotThrowAsync(async () => await _sut.GetNextAsync(batchCount));
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
            
        //Act & Assert
        Assert.DoesNotThrowAsync(async () => await _sut.GetNextAsync(batchCount));
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

        //Act & Assert
        Assert.DoesNotThrowAsync(async () => await _sut.GetNextAsync(batchCount));
        _outboxEventRepository.Verify(x => x.GetLatestEventsAsync( batchCount, newestEventId), Times.Once);
    }
}