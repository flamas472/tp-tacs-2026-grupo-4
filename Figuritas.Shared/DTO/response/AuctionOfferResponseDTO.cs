namespace Figuritas.Shared.DTO.response;

public class AuctionOfferResponseDTO
{
    public int Id { get; set; }
    public string AuctionId { get; set; } = string.Empty;
    public int BidderId { get; set; }
    public List<int> OfferedUserStickerIds { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}
