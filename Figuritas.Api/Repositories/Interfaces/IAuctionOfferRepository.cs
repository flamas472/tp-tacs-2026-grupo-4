using Figuritas.Shared.Model;
using Figuritas.Shared.Model.Subastas;

namespace Figuritas.Api.Repositories;

public interface IAuctionOfferRepository
{
    List<AuctionOffer> GetAll();
    void Add(AuctionOffer offer);
    AuctionOffer? GetById(int id);
    void Update(AuctionOffer offer);

    /// <summary>
    /// Returns only the <see cref="AuctionOfferState.Pending"/> offers for the given auction.
    /// The filter is applied server-side in MongoDB (not in memory).
    /// Use this method for ranking and winner-selection logic.
    /// </summary>
    Task<List<AuctionOffer>> GetByAuctionIdAsync(int auctionId);

    /// <summary>
    /// Returns all offers for the given auction regardless of <see cref="AuctionOfferState"/>.
    /// Use this method when you need to close the auction and transition all offers (Won + Lost).
    /// </summary>
    Task<List<AuctionOffer>> GetAllByAuctionIdAsync(int auctionId);

    /// <summary>
    /// Returns the most recent active offer placed by <paramref name="bidderId"/> on the given auction,
    /// or null if the bidder has no active offer.
    /// </summary>
    Task<AuctionOffer?> GetActiveBidderOfferAsync(int auctionId, int bidderId);
}
