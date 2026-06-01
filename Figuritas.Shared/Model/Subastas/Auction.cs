using Figuritas.Shared.Model.Subastas;

namespace Figuritas.Shared.Model;

public class Auction
{
    public int Id { get; set; }

    public int AuctioneerId { get; set; }

    public int UserStickerId { get; set; }

    public List<int> MinimumOfferStickerIds { get; set; } = new();

    public AuctionStatus Status { get; set; } = AuctionStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime EndsAt { get; set; }

    public int? BestCurrentOfferId { get; set; }

    // Deuda técnica aceptada: Campos de worker-coordination en la entidad Auction (mezcla de concern de infraestructura con dominio) — bajo riesgo, requiere refactor más amplio
    /// <summary>Marks that the AuctionEnding alert was already sent. Prevents duplicate worker notifications.</summary>
    public bool AuctionEndingNotificationSent { get; set; } = false;

    /// <summary>
    /// UTC timestamp set atomically when a worker instance claims this auction for automatic closure.
    /// Null means no worker has claimed it yet. Used to prevent double-closure across worker instances.
    /// </summary>
    public DateTime? AutoClosureClaimedAt { get; set; } = null;
}
