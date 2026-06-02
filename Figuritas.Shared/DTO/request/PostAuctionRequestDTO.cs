namespace Figuritas.Shared.DTO.request;

public class PostAuctionRequestDTO
{
    public int UserStickerId { get; set; }
    public List<int> MinimumOfferStickerIds { get; set; } = new();
    public DateTime EndsAt { get; set; }
}
