using Figuritas.Shared.Model.Notificaciones;

namespace Figuritas.Api.Repositories;

public interface INotificationRepository
{
    Task<Notification> AddAsync(Notification notification);
    Task<List<Notification>> GetByUserIdPagedAsync(int userId, int page, int pageSize);
    Task<Notification?> GetByIdAsync(int id);
    Task MarkAsReadAsync(int id);
    Task<bool> ExistsForUserWithReferenceAsync(int userId, NotificationType type, int referenceId);
}
