namespace Figuritas.Shared.DTO.response;

public class AuctionOfferResponseDTO
{
    public int Id { get; set; }
    public int AuctionId { get; set; }
    public int BidderId { get; set; }
    public List<int> OfferedUserStickerIds { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}
