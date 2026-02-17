using HolidayMonitor.Contracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using NServiceBus;
using Notifier.Api.Handlers;
using Notifier.Api.Hubs;

namespace Notifier.Api.Tests;

[TestFixture]
public class NotifyUsersCommandHandlerTests
{
    private Mock<IHubContext<NotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<NotifyUsersCommandHandler>> _mockLogger = null!;
    private Mock<IMessageHandlerContext> _mockContext = null!;
    private Mock<IHubClients> _mockClients = null!;
    private Mock<IClientProxy> _mockClientProxy = null!;
    private Mock<IGroupManager> _mockGroups = null!;
    private NotifyUsersCommandHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _mockHubContext = new Mock<IHubContext<NotificationHub>>();
        _mockLogger = new Mock<ILogger<NotifyUsersCommandHandler>>();
        _mockContext = new Mock<IMessageHandlerContext>();
        _mockClientProxy = new Mock<IClientProxy>();
        _mockClients = new Mock<IHubClients>();
        _mockGroups = new Mock<IGroupManager>();

        _mockContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
        _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);

        _handler = new NotifyUsersCommandHandler(_mockHubContext.Object, _mockLogger.Object);
    }

    [Test]
    public async Task Handle_SendsSignalRMessage_ToAllUsers()
    {
        // Arrange
        var message = new NotifyUsersCommand
        {
            UserIds = new List<string> { "user1", "user2", "user3" },
            CountryCode = "US",
            CountryName = "United States",
            Date = new DateTime(2024, 7, 4),
            Name = "Independence Day",
            LocalName = "Independence Day",
            DetectedAtUtc = DateTime.UtcNow
        };

        _mockClientProxy.Setup(p => p.SendCoreAsync(
                "HolidayDetected",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(message, _mockContext.Object);

        // Assert
        _mockClients.Verify(c => c.Group("user1"), Times.Once);
        _mockClients.Verify(c => c.Group("user2"), Times.Once);
        _mockClients.Verify(c => c.Group("user3"), Times.Once);
        _mockClientProxy.Verify(p => p.SendCoreAsync(
            "HolidayDetected",
            It.Is<object[]>(args => args.Length == 1),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Test]
    public async Task Handle_SendsCorrectPayload_ToSignalR()
    {
        // Arrange
        var message = new NotifyUsersCommand
        {
            UserIds = new List<string> { "user1" },
            CountryCode = "GB",
            CountryName = "United Kingdom",
            Date = new DateTime(2024, 12, 25),
            Name = "Christmas Day",
            LocalName = "Christmas Day",
            DetectedAtUtc = DateTime.UtcNow
        };

        object[]? capturedArgs = null;
        _mockClientProxy.Setup(p => p.SendCoreAsync(
                "HolidayDetected",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object[], CancellationToken>((_, args, _) => capturedArgs = args)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(message, _mockContext.Object);

        // Assert
        Assert.That(capturedArgs, Is.Not.Null);
        Assert.That(capturedArgs!.Length, Is.EqualTo(1));
        
        var payload = capturedArgs[0];
        var payloadType = payload.GetType();
        Assert.That(payloadType.GetProperty("countryCode")?.GetValue(payload), Is.EqualTo("GB"));
        Assert.That(payloadType.GetProperty("countryName")?.GetValue(payload), Is.EqualTo("United Kingdom"));
        Assert.That(payloadType.GetProperty("name")?.GetValue(payload), Is.EqualTo("Christmas Day"));
    }

    [Test]
    public async Task Handle_LogsInformation_AfterSending()
    {
        // Arrange
        var message = new NotifyUsersCommand
        {
            UserIds = new List<string> { "user1" },
            CountryCode = "US",
            CountryName = "United States",
            Date = DateTime.UtcNow,
            Name = "Test Holiday",
            LocalName = "Test Holiday",
            DetectedAtUtc = DateTime.UtcNow
        };

        _mockClientProxy.Setup(p => p.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(message, _mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Pushed holiday notification")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_HandlesEmptyUserList_Gracefully()
    {
        // Arrange
        var message = new NotifyUsersCommand
        {
            UserIds = new List<string>(),
            CountryCode = "US",
            CountryName = "United States",
            Date = DateTime.UtcNow,
            Name = "Test Holiday",
            LocalName = "Test Holiday",
            DetectedAtUtc = DateTime.UtcNow
        };

        // Act
        await _handler.Handle(message, _mockContext.Object);

        // Assert
        _mockClients.Verify(c => c.Group(It.IsAny<string>()), Times.Never);
        _mockClientProxy.Verify(p => p.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
