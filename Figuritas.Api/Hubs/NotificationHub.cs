using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Figuritas.Api.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected to NotificationHub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    // Business logic is handled in NotificationService.
    // The hub is solely responsible for real-time delivery.
}
