using System.Threading.Tasks;
using AutoFixture;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services;
using PollingOutboxPublisher.Database.Repositories.Interfaces;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Tests.Coordinators.OutboxCoordinator.Services;

public class MissingDetectorTests
{
    private Mock<IMissingEventRepository> _missingEventRepository;
    private Mock<ILogger<MissingDetector>> _logger;
    private Fixture _fixture;
    private MissingDetector _sut;

    [SetUp]
    public void Setup()
    {
        _missingEventRepository = new Mock<IMissingEventRepository>();
        _logger = new Mock<ILogger<MissingDetector>>();
        _fixture = new Fixture();
        _sut = new MissingDetector(_missingEventRepository.Object, _logger.Object);
    }
        
    [Test]
    public async Task DetectAsync_WhenMissingIdFound_ShouldInsert()
    {
        //Arrange
        var latestOffset = 1;
        var gapBetweenEventIds = _fixture.Create<int>();
        var firstMissingId = _fixture.Create<int>();
        var lastMissingId = firstMissingId + gapBetweenEventIds + 1;
        var expectedFirstId = latestOffset + 1;
        var gapBetweenLatestOffsetAndFirstMissingId = firstMissingId - expectedFirstId;
        var expectedMissingEvents = gapBetweenEventIds + gapBetweenLatestOffsetAndFirstMissingId;
            
        var queuedItem1 = _fixture.Build<OutboxEvent>()
            .With(x => x.Id, firstMissingId)
            .Create();
        var queuedItem2 = _fixture.Build<OutboxEvent>()
            .With(x => x.Id, lastMissingId)
            .Create();
        var outboxEventsBatch = _fixture.Build<OutboxEventsBatch>()
            .With(x => x.Events, new[] {queuedItem1, queuedItem2})
            .With(x => x.LatestOffset, latestOffset)
            .Create();

        //Act
        await _sut.DetectAsync(outboxEventsBatch);

        //Assert
        _missingEventRepository.Verify(x =>
                x.InsertAsync( It.IsAny<MissingEvent>()), 
            Times.Exactly(expectedMissingEvents));
    }
        
    [Test]
    public async Task DetectAsync_WhenThereIsNotAnyOutboxEvent_ShouldNotInsert()
    {
        //Arrange
        var outboxEventsBatch = new OutboxEventsBatch();

        //Act
        await _sut.DetectAsync(outboxEventsBatch);

        //Assert
        _missingEventRepository.Verify(x =>
                x.InsertAsync( It.IsAny<MissingEvent>()), 
            Times.Never);
    }
}