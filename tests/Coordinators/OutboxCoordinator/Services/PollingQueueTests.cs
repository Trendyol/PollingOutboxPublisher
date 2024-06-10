using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services;
using PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services.Interfaces;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Tests.Coordinators.OutboxCoordinator.Services;

public class PollingQueueTests
{
    private Fixture _fixture;
    private Mock<IPollingSource> _pollingSource;
    private Mock<IConfiguration> _configuration;
    private Mock<IOptions<WorkerSettings>> _outboxSettings;
    private Mock<IMissingDetector> _missingDetector;
    private PollingOutboxQueue _sut;

    [SetUp]
    public void Setup()
    {
        _pollingSource = new Mock<IPollingSource>();
        _configuration=new Mock<IConfiguration>();
        _fixture = new Fixture();
        _outboxSettings = new Mock<IOptions<WorkerSettings>>();
        _outboxSettings.Setup(i => i.Value).Returns(new WorkerSettings { QueueWaitDuration = 5});
        _missingDetector = new Mock<IMissingDetector>();
        var configurationSection = new Mock<IConfigurationSection>();
        configurationSection.Setup(a => a.Value).Returns("1");
        _configuration.Setup(a => a.GetSection("PrefetchCount")).Returns(configurationSection.Object);
        _sut = new PollingOutboxQueue(_pollingSource.Object, _outboxSettings.Object, _missingDetector.Object);
    }

    [Test]
    public async Task Dequeue_WhenThereArePendingItems_ShouldReturnFirst()
    {
        //Arrange
        var cancellationToken = new CancellationToken();
            
        var queuedItem = _fixture.Create<OutboxEvent>();
        var outboxEventsBatch = _fixture.Build<OutboxEventsBatch>()
            .With(x => x.Events, new[] {queuedItem})
            .Create();
        _pollingSource.Setup(x => x.GetNextAsync(It.IsAny<int>())).ReturnsAsync(outboxEventsBatch);

        //Act
        var item = await _sut.DequeueAsync(cancellationToken);

        //Assert
        item.Should().NotBeNull();
        item.First().Should().Be(queuedItem);
    }
}