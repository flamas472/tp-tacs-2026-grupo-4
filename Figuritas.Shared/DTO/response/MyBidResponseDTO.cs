namespace Figuritas.Shared.DTO.response;

/// <summary>
/// Represents a bid (auction offer) placed by the authenticated user on a third-party auction.
/// Returned by GET /api/dashboard/bids.
/// </summary>
public class MyBidResponseDTO
{
    public int OfferId { get; set; }

    // ── Auction context ─────────────────────────────────────────────────────
    public int AuctionId { get; set; }

    /// <summary>Catalog sticker number of the sticker being auctioned.</summary>
    public int StickerNumber { get; set; }

    /// <summary>Description of the sticker being auctioned.</summary>
    public string StickerDescription { get; set; } = string.Empty;

    /// <summary>National team of the sticker being auctioned.</summary>
    public string StickerNationalTeam { get; set; } = string.Empty;

    /// <summary>Club team of the sticker being auctioned.</summary>
    public string StickerTeam { get; set; } = string.Empty;

    public string AuctionStatus { get; set; } = string.Empty;

    // ── Offer data ──────────────────────────────────────────────────────────
    public DateTime CreatedAt { get; set; }

    /// <summary>Lifecycle state of the offer: Pending, Won, Lost, or Cancelled.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// True when this offer is currently the system-ranked or auctioneer-selected best offer.
    /// Compares against BestCurrentOfferId and UserSelectedBestOfferId on the parent auction.
    /// </summary>
    public bool IsCurrentWinner { get; set; }
}
