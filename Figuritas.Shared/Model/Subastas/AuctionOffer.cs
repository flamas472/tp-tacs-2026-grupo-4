namespace Figuritas.Shared.Model;

public class AuctionOffer
{
    public int Id { get; set; }

    public int AuctionId { get; set; }

    public int BidderId { get; set; }

    public List<int> OfferedUserStickerIds { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
