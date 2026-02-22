using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Notifier.Api.Hubs;

[AllowAnonymous]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.Identity?.Name ?? Context.GetHttpContext()?.Request.Query["userId"].FirstOrDefault() ?? Context.ConnectionId;
        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        _logger.LogInformation("Client connected: {ConnectionId}, userId: {UserId}, QueryParams: {QueryString}", 
            Context.ConnectionId, userId, Context.GetHttpContext()?.Request.QueryString);
        await base.OnConnectedAsync();
    }

    public async Task SetUserId(string userId)
    {
        _logger.LogInformation("SetUserId called: ConnectionId={ConnectionId}, UserId={UserId}", Context.ConnectionId, userId);
        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        _logger.LogInformation("Added connection {ConnectionId} to group {UserId}", Context.ConnectionId, userId);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
