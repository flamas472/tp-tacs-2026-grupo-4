using Figuritas.Shared.Model;

namespace Figuritas.Api.Repositories;

public interface IAuctionRepository
{
    List<Auction> GetAll();
    void Add(Auction auction);
    Auction? GetById(int id);
    void Update(Auction auction);
    List<Auction> GetByAuctioneerId(int auctioneerId, string? status, int page, int pageSize);
}
