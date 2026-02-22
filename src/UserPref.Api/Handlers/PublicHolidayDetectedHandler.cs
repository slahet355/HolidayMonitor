using System.Diagnostics;
using HolidayMonitor.Contracts;
using NServiceBus;
using UserPref.Api.Repositories;

namespace UserPref.Api.Handlers;

public class PublicHolidayDetectedHandler : IHandleMessages<PublicHolidayDetected>
{
    private static readonly ActivitySource ActivitySource = new("UserPref");
    private readonly ISubscriptionRepository _subscriptions;
    private readonly ILogger<PublicHolidayDetectedHandler> _logger;

    public PublicHolidayDetectedHandler(
        ISubscriptionRepository subscriptions,
        ILogger<PublicHolidayDetectedHandler> logger)
    {
        _subscriptions = subscriptions;
        _logger = logger;
    }

    public async Task Handle(PublicHolidayDetected message, IMessageHandlerContext context)
    {
        using var activity = ActivitySource.StartActivity("ProcessHolidayDetected");
        activity?.SetTag("country", message.CountryCode);
        activity?.SetTag("holiday", message.Name);
        
        _logger.LogInformation("Received PublicHolidayDetected: {Country} - {Name} on {Date}", 
            message.CountryCode, message.Name, message.Date.ToString("yyyy-MM-dd"));

        var userIds = await _subscriptions.GetUserIdsSubscribedToCountryAsync(message.CountryCode, context.CancellationToken);
        
        _logger.LogInformation("Found {Count} subscribers for {Country}", userIds.Count, message.CountryCode);
        
        if (userIds.Count > 0)
        {
            _logger.LogInformation("Subscriber UserIds: {UserIds}", string.Join(", ", userIds));
        }
        
        if (userIds.Count == 0)
        {
            _logger.LogDebug("No subscribers for {Country} - {Name}", message.CountryCode, message.Name);
            return;
        }

        var cmd = new NotifyUsersCommand
        {
            UserIds = userIds,
            CountryCode = message.CountryCode,
            CountryName = message.CountryName,
            Date = message.Date,
            LocalName = message.LocalName,
            Name = message.Name,
            DetectedAtUtc = message.DetectedAtUtc
        };
        await context.Send(cmd);
        _logger.LogInformation("Sent NotifyUsersCommand to {Count} users for {Country} - {Name}",
            userIds.Count, message.CountryCode, message.Name);
    }
}
