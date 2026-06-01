using Figuritas.Shared.Model;
using Figuritas.Shared.Model.Subastas;

namespace Figuritas.Api.Repositories;

public interface IAuctionOfferRepository
{
    List<AuctionOffer> GetAll();
    void Add(AuctionOffer offer);
    AuctionOffer? GetById(int id);
    void Update(AuctionOffer offer);
    Task<List<AuctionOffer>> GetByAuctionIdAsync(int auctionId);

    /// <summary>
    /// Returns the most recent active offer placed by <paramref name="bidderId"/> on the given auction,
    /// or null if the bidder has no active offer.
    /// </summary>
    Task<AuctionOffer?> GetActiveBidderOfferAsync(int auctionId, int bidderId);
}
