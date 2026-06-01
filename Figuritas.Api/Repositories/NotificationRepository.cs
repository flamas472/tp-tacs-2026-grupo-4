using Figuritas.Shared.Model.Notificaciones;
using MongoDB.Driver;

namespace Figuritas.Api.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly IMongoCollection<Notification> _notifications;
    private readonly IIdGenerator _idGenerator;

    public NotificationRepository(MongoDbContext context, IIdGenerator idGenerator)
    {
        _notifications = context.Collection<Notification>("Notifications");
        _idGenerator = idGenerator;
        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        var indexKeys = Builders<Notification>.IndexKeys
            .Ascending(n => n.UserId)
            .Descending(n => n.CreatedAt);

        var indexModel = new CreateIndexModel<Notification>(indexKeys);
        _notifications.Indexes.CreateOne(indexModel);
    }

    public async Task<Notification> AddAsync(Notification notification)
    {
        notification.Id = _idGenerator.GetNextId<Notification>();
        await _notifications.InsertOneAsync(notification);
        return notification;
    }

    public async Task<List<Notification>> GetByUserIdPagedAsync(int userId, int page, int pageSize)
    {
        return await _notifications
            .Find(n => n.UserId == userId)
            .SortByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();
    }

    public async Task<Notification?> GetByIdAsync(int id)
    {
        return await _notifications.Find(n => n.Id == id).FirstOrDefaultAsync();
    }

    public async Task MarkAsReadAsync(int id)
    {
        await _notifications.UpdateOneAsync(
            n => n.Id == id,
            Builders<Notification>.Update.Set(n => n.IsRead, true));
    }

    public async Task<bool> ExistsForUserWithReferenceAsync(int userId, NotificationType type, int referenceId)
    {
        return await _notifications.Find(n =>
            n.UserId == userId &&
            n.Type == type &&
            n.ReferenceId == referenceId).AnyAsync();
    }
}
