using Figuritas.Shared.Model.Notificaciones;

namespace Figuritas.Shared.DTO.response;

public class NotificationResponseDTO
{
    public int Id { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
