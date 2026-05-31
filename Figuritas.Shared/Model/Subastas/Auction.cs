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
}
