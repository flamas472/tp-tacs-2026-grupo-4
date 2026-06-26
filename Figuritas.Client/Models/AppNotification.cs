namespace Figuritas.Client.Models;

/// <summary>Tipos de notificaciones soportados por la plataforma.</summary>
public enum NotificationType
{
    /// <summary>Se recibió una nueva propuesta de intercambio.</summary>
    NewProposal,
    /// <summary>Una propuesta enviada fue aceptada.</summary>
    ProposalAccepted,
    /// <summary>Una propuesta enviada fue rechazada.</summary>
    ProposalRejected,
    /// <summary>Una figurita faltante apareció disponible en el mercado.</summary>
    StickerAvailable,
    /// <summary>Una subasta de interés está próxima a finalizar.</summary>
    AuctionEnding,
    /// <summary>Se recibió una nueva puja en una subasta propia.</summary>
    NewBid,
    /// <summary>Otro usuario dejó una calificación después de un intercambio.</summary>
    RatingReceived
}

/// <summary>Representa una notificación dentro de la aplicación.</summary>
public class AppNotification
{
    public int Id { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>URL relativa a la que se navega al hacer click.</summary>
    public string? NavigationUrl { get; set; }
}
