using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Figuritas.Api.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    // Business logic is handled in NotificationService.
    // The hub is solely responsible for real-time delivery.
}
