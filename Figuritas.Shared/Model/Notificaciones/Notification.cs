namespace Figuritas.Shared.Model.Notificaciones;

public class Notification
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public NotificationType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Optional reference to a domain entity (e.g. a UserSticker publication ID).
    /// Used for idempotency checks to avoid sending the same alert twice.
    /// </summary>
    public int? ReferenceId { get; set; }
}
