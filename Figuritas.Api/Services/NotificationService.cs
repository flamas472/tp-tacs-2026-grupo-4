using Figuritas.Api.Hubs;
using Figuritas.Api.Repositories;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model.Notificaciones;
using Microsoft.AspNetCore.SignalR;

namespace Figuritas.Api.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepo;
    private readonly IUserRepository _userRepo;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository notificationRepo,
        IUserRepository userRepo,
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationService> logger)
    {
        _notificationRepo = notificationRepo;
        _userRepo = userRepo;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Checks user preferences, persists the notification and attempts real-time delivery via SignalR.
    /// Persistence is guaranteed. SignalR delivery is best-effort.
    /// </summary>
    public async Task<Notification?> SendNotificationAsync(
        int userId,
        NotificationType type,
        string title,
        string message,
        DateTime? expiresAt = null,
        int? referenceId = null)
    {
        var user = _userRepo.GetById(userId);
        if (user == null)
            return null;

        // Check user preferences
        if (!IsPreferenceEnabled(user, type))
            return null;

        // Idempotency guard for reference-based notifications
        if (referenceId.HasValue)
        {
            var alreadySent = await _notificationRepo.ExistsForUserWithReferenceAsync(userId, type, referenceId.Value);
            if (alreadySent)
                return null;
        }

        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            ExpiresAt = expiresAt,
            ReferenceId = referenceId
        };

        await _notificationRepo.AddAsync(notification);

        // Best-effort real-time delivery via SignalR
        try
        {
            var dto = MapToDto(notification);
            await _hubContext.Clients.User(userId.ToString())
                .SendAsync("ReceiveNotification", dto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR delivery failed for user {UserId}, notification {NotificationId}",
                userId, notification.Id);
        }

        return notification;
    }

    public async Task<List<NotificationResponseDTO>> GetMyNotificationsAsync(int userId, GetMyNotificationsDTO dto)
    {
        var notifications = await _notificationRepo.GetByUserIdPagedAsync(userId, dto.Page, dto.PageSize);
        return notifications.Select(MapToDto).ToList();
    }

    public async Task MarkAsReadAsync(int notificationId, int callerUserId)
    {
        var notification = await _notificationRepo.GetByIdAsync(notificationId);
        if (notification == null)
            throw new KeyNotFoundException("Notification not found.");

        if (notification.UserId != callerUserId)
            throw new UnauthorizedAccessException("This notification does not belong to the caller.");

        await _notificationRepo.MarkAsReadAsync(notificationId);
    }

    public void UpdatePreferences(int userId, UpdatePreferencesDTO dto)
    {
        var user = _userRepo.GetById(userId);
        if (user == null)
            throw new KeyNotFoundException("User not found.");

        user.AlertOnMissingStickerAvailable = dto.AlertOnMissingStickerAvailable;
        user.AlertOnAuctionEnding = dto.AlertOnAuctionEnding;
        user.AlertOnNewProposal = dto.AlertOnNewProposal;

        _userRepo.Update(user);
    }

    private static bool IsPreferenceEnabled(Figuritas.Shared.Model.User user, NotificationType type)
    {
        return type switch
        {
            NotificationType.NewProposal => user.AlertOnNewProposal,
            NotificationType.AuctionEnding => user.AlertOnAuctionEnding,
            NotificationType.MissingStickerAvailable => user.AlertOnMissingStickerAvailable,
            _ => true
        };
    }

    private static NotificationResponseDTO MapToDto(Notification notification) => new()
    {
        Id = notification.Id,
        Type = notification.Type,
        Title = notification.Title,
        Message = notification.Message,
        IsRead = notification.IsRead,
        CreatedAt = notification.CreatedAt
    };
}
