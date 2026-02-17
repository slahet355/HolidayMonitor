using HolidayMonitor.Contracts;
using Microsoft.Extensions.Logging;
using Moq;
using NServiceBus;
using UserPref.Api.Handlers;
using UserPref.Api.Repositories;

namespace UserPref.Api.Tests;

[TestFixture]
public class PublicHolidayDetectedHandlerTests
{
    private Mock<ISubscriptionRepository> _mockRepository = null!;
    private Mock<ILogger<PublicHolidayDetectedHandler>> _mockLogger = null!;
    private Mock<IMessageHandlerContext> _mockContext = null!;
    private PublicHolidayDetectedHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _mockRepository = new Mock<ISubscriptionRepository>();
        _mockLogger = new Mock<ILogger<PublicHolidayDetectedHandler>>();
        _mockContext = new Mock<IMessageHandlerContext>();
        _mockContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        _handler = new PublicHolidayDetectedHandler(_mockRepository.Object, _mockLogger.Object);
    }

    [Test]
    public async Task Handle_SendsNotifyUsersCommand_WhenSubscribersExist()
    {
        // Arrange
        var message = new PublicHolidayDetected
        {
            CountryCode = "US",
            CountryName = "United States",
            Date = new DateTime(2024, 7, 4),
            Name = "Independence Day",
            LocalName = "Independence Day",
            DetectedAtUtc = DateTime.UtcNow
        };
        var userIds = new List<string> { "user1", "user2" };

        _mockRepository.Setup(r => r.GetUserIdsSubscribedToCountryAsync(
                message.CountryCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userIds);

        NotifyUsersCommand? sentCommand = null;
        _mockContext.Setup(c => c.Send(It.IsAny<NotifyUsersCommand>(), It.IsAny<SendOptions>()))
            .Callback<object, SendOptions>((cmd, _) => sentCommand = cmd as NotifyUsersCommand)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(message, _mockContext.Object);

        // Assert
        _mockRepository.Verify(r => r.GetUserIdsSubscribedToCountryAsync(
            message.CountryCode, It.IsAny<CancellationToken>()), Times.Once);
        _mockContext.Verify(c => c.Send(It.IsAny<NotifyUsersCommand>(), It.IsAny<SendOptions>()), Times.Once);
        Assert.That(sentCommand, Is.Not.Null);
        Assert.That(sentCommand!.UserIds, Is.EqualTo(userIds));
        Assert.That(sentCommand.CountryCode, Is.EqualTo(message.CountryCode));
        Assert.That(sentCommand.Name, Is.EqualTo(message.Name));
    }

    [Test]
    public async Task Handle_DoesNotSendCommand_WhenNoSubscribers()
    {
        // Arrange
        var message = new PublicHolidayDetected
        {
            CountryCode = "XX",
            CountryName = "Unknown",
            Date = DateTime.UtcNow,
            Name = "Test Holiday",
            LocalName = "Test Holiday",
            DetectedAtUtc = DateTime.UtcNow
        };

        _mockRepository.Setup(r => r.GetUserIdsSubscribedToCountryAsync(
                message.CountryCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        // Act
        await _handler.Handle(message, _mockContext.Object);

        // Assert
        _mockRepository.Verify(r => r.GetUserIdsSubscribedToCountryAsync(
            message.CountryCode, It.IsAny<CancellationToken>()), Times.Once);
        _mockContext.Verify(c => c.Send(It.IsAny<NotifyUsersCommand>(), It.IsAny<SendOptions>()), Times.Never);
    }

    [Test]
    public async Task Handle_LogsInformation_WhenSubscribersExist()
    {
        // Arrange
        var message = new PublicHolidayDetected
        {
            CountryCode = "US",
            CountryName = "United States",
            Date = new DateTime(2024, 7, 4),
            Name = "Independence Day",
            LocalName = "Independence Day",
            DetectedAtUtc = DateTime.UtcNow
        };
        var userIds = new List<string> { "user1" };

        _mockRepository.Setup(r => r.GetUserIdsSubscribedToCountryAsync(
                message.CountryCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userIds);

        // Act
        await _handler.Handle(message, _mockContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Sent NotifyUsersCommand")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
