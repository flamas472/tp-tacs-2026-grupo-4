using Figuritas.Shared.Model;

namespace Figuritas.Api.Repositories;

public interface IAuctionRepository
{
    List<Auction> GetAll();
    void Add(Auction auction);
    Auction? GetById(int id);
}
