using Figuritas.Shared.Model;

namespace Figuritas.Api.Repositories;

public interface IAuctionOfferRepository
{
    List<AuctionOffer> GetAll();
    void Add(AuctionOffer offer);
    AuctionOffer? GetById(int id);
    Task<List<AuctionOffer>> GetByAuctionIdAsync(int auctionId);
}
