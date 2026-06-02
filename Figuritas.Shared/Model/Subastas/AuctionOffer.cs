using Figuritas.Shared.Model.Subastas;

namespace Figuritas.Shared.Model;

public class AuctionOffer
{
    public int Id { get; set; }

    public int AuctionId { get; set; }

    public int BidderId { get; set; }

    public List<int> OfferedUserStickerIds { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Internal workflow status used to track superseded offers within the bidding process.
    /// An offer is <see cref="AuctionOfferStatus.Superseded"/> when the same bidder places a
    /// new offer, making the previous one inactive for ranking purposes.
    /// </summary>
    public AuctionOfferStatus Status { get; set; } = AuctionOfferStatus.Active;

    /// <summary>
    /// Lifecycle outcome of this offer.  All offers start as <see cref="AuctionOfferState.Pending"/>.
    /// When the auction closes, the winner transitions to <see cref="AuctionOfferState.Won"/> and
    /// every other pending offer transitions to <see cref="AuctionOfferState.Lost"/>.
    /// </summary>
    public AuctionOfferState State { get; set; } = AuctionOfferState.Pending;
}
