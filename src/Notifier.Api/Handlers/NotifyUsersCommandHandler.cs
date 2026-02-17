using System.Diagnostics;
using HolidayMonitor.Contracts;
using Microsoft.AspNetCore.SignalR;
using NServiceBus;
using Notifier.Api.Hubs;

namespace Notifier.Api.Handlers;

public class NotifyUsersCommandHandler : IHandleMessages<NotifyUsersCommand>
{
    private static readonly ActivitySource ActivitySource = new("Notifier");
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotifyUsersCommandHandler> _logger;

    public NotifyUsersCommandHandler(
        IHubContext<NotificationHub> hubContext,
        ILogger<NotifyUsersCommandHandler> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task Handle(NotifyUsersCommand message, IMessageHandlerContext context)
    {
        using var activity = ActivitySource.StartActivity("NotifyUsers");
        activity?.SetTag("userCount", message.UserIds.Count);
        activity?.SetTag("country", message.CountryCode);

        var payload = new
        {
            type = "HolidayDetected",
            countryCode = message.CountryCode,
            countryName = message.CountryName,
            date = message.Date,
            localName = message.LocalName,
            name = message.Name,
            detectedAtUtc = message.DetectedAtUtc
        };

        foreach (var userId in message.UserIds)
        {
            await _hubContext.Clients.Group(userId).SendAsync("HolidayDetected", payload, context.CancellationToken);
        }

        _logger.LogInformation("Pushed holiday notification to {Count} users: {Country} - {Name}",
            message.UserIds.Count, message.CountryCode, message.Name);
    }
}
