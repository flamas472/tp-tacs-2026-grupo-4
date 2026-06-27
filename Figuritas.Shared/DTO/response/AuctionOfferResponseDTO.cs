namespace Figuritas.Shared.DTO.response;

public class AuctionOfferResponseDTO
{
    public int Id { get; set; }
    public int AuctionId { get; set; }
    public int BidderId { get; set; }
    public List<int> OfferedUserStickerIds { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    /// <summary>Lifecycle state of the offer: Pending, Won, Lost, or Cancelled.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>Username of the user who placed this offer.</summary>
    public string BidderUsername { get; set; } = string.Empty;

    /// <summary>
    /// Preview details for every sticker committed to this offer, including those that have
    /// been deactivated (Active=false / Quantity=0) because their stock was fully reserved.
    /// Populated server-side so the client never needs to filter by Active.
    /// </summary>
    public List<StickerPreviewDTO> OfferedStickerPreviews { get; set; } = new();
}
