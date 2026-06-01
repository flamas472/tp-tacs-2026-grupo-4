using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model.Notificaciones;

namespace Figuritas.Api.Services;

public interface INotificationService
{
    Task<Notification?> SendNotificationAsync(
        int userId,
        NotificationType type,
        string title,
        string message,
        DateTime? expiresAt = null,
        int? referenceId = null);

    Task<List<NotificationResponseDTO>> GetMyNotificationsAsync(int userId, GetMyNotificationsDTO dto);

    Task MarkAsReadAsync(int notificationId, int callerUserId);

    void UpdatePreferences(int userId, UpdatePreferencesDTO dto);
}
