namespace Figuritas.Shared.DTO.response;

public class AuctionResponseDTO
{
    public int Id { get; set; }
    public int AuctioneerId { get; set; }
    public int UserStickerId { get; set; }
    public List<int> MinimumOfferStickerIds { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime EndsAt { get; set; }
    public int? BestCurrentOfferId { get; set; }
}
