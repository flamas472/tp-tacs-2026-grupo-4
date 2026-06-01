using Figuritas.Shared.Model;

namespace Figuritas.Api.Repositories;

public interface IAuctionRepository
{
    List<Auction> GetAll();
    void Add(Auction auction);
    Auction? GetById(int id);
    void Update(Auction auction);
    List<Auction> GetByAuctioneerId(int auctioneerId, string? status, int page, int pageSize);
    Task<List<Auction>> GetActiveAuctionsEndingBeforeAsync(DateTime threshold);

    /// <summary>
    /// Atomically sets AuctionEndingNotificationSent = true only if it is currently false.
    /// Returns true if the flag was successfully claimed (i.e., was false before this call).
    /// Returns false if the flag was already true (another process already claimed it).
    /// </summary>
    Task<bool> TryClaimEndingNotificationAsync(int auctionId);
}
