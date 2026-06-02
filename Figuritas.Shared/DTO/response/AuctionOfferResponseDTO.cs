namespace Figuritas.Shared.DTO.response;

public class AuctionOfferResponseDTO
{
    public int Id { get; set; }
    public int AuctionId { get; set; }
    public int BidderId { get; set; }
    public List<int> OfferedUserStickerIds { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    /// <summary>Lifecycle state of the offer: Pending, Won, or Lost.</summary>
    public string State { get; set; } = string.Empty;
}
