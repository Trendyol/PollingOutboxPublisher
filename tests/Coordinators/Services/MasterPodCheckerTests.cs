using System;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using PollingOutboxPublisher.ConfigOptions;
using PollingOutboxPublisher.Coordinators.Services;
using PollingOutboxPublisher.Redis.Interfaces;
using StackExchange.Redis;

namespace PollingOutboxPublisher.Tests.Coordinators.Services;

public class MasterPodCheckerTests
{
    private Fixture _fixture;
    private Mock<ILogger<MasterPodChecker>> _loggerMock;
    private Mock<IOptions<MasterPodSettings>> _masterPodSettingsMock;
    private Mock<IRedisLockManager> _lockManagerMock;
    private MasterPodChecker _masterPodChecker;

    [SetUp]
    public void Setup()
    {
        _fixture = new Fixture();
        _loggerMock = new Mock<ILogger<MasterPodChecker>>();
        _masterPodSettingsMock = new Mock<IOptions<MasterPodSettings>>();
        _lockManagerMock = new Mock<IRedisLockManager>();

        _masterPodSettingsMock.Setup(i => i.Value).Returns(new MasterPodSettings { CacheName = "CacheName"});
        _masterPodChecker = new MasterPodChecker(_loggerMock.Object, _masterPodSettingsMock.Object,
            _lockManagerMock.Object);
    }

    [Test]
    public async Task TryToBecomeMasterPodAsync_ShouldCallLockExtendAsync_WhenHasLockIsTrue()
    {
        // Arrange
        _lockManagerMock.Setup(lm =>
                lm.LockTakeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(),
                    It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _lockManagerMock.Setup(lm =>
                lm.LockExtendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<TimeSpan>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        await _masterPodChecker.TryToBecomeMasterPodAsync(CancellationToken.None);

        // Act
        await _masterPodChecker.TryToBecomeMasterPodAsync(CancellationToken.None);

        // Assert
        _lockManagerMock.Verify(
            lm => lm.LockExtendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<TimeSpan>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Test]
    public async Task TryToBecomeMasterPodAsync_ShouldCallLockTakeAsync_WhenHasLockIsFalse()
    {
        // Arrange
        _lockManagerMock.Setup(lm =>
                lm.LockTakeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(),
                    It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _masterPodChecker.TryToBecomeMasterPodAsync(CancellationToken.None);

        // Assert
        _lockManagerMock.Verify(
            lm => lm.LockTakeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<TimeSpan>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Test]
    public async Task IsMasterPodAsync_ShouldReturnTrue_WhenHasLockIsTrue()
    {
        // Arrange
        _lockManagerMock.Setup(lm =>
                lm.LockTakeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<TimeSpan>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        await _masterPodChecker.TryToBecomeMasterPodAsync(CancellationToken.None);

        // Act
        var result = await _masterPodChecker.IsMasterPodAsync(CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task IsMasterPodAsync_ShouldReturnFalse_WhenHasLockIsFalse()
    {
        // Arrange
        _lockManagerMock.Setup(lm =>
            lm.LockTakeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(),
                It.IsAny<CommandFlags>())).ReturnsAsync(false);
            
        // Act
        bool result = await _masterPodChecker.IsMasterPodAsync(CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }
}