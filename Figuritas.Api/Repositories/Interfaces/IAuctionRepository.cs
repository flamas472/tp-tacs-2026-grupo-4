using Figuritas.Shared.Model;

namespace Figuritas.Api.Repositories;

public interface IAuctionRepository
{
    List<Auction> GetAll(int page = 1, int pageSize = 20);
    void Add(Auction auction);
    Auction? GetById(int id);
    void Update(Auction auction);
    List<Auction> GetByAuctioneerId(int auctioneerId, string? status, int page, int pageSize);
    Task<List<Auction>> GetActiveAuctionsEndingBeforeAsync(DateTime threshold);

    /// <summary>
    /// Returns all active auctions whose EndsAt is strictly before <paramref name="now"/>.
    /// These are auctions that have expired but have not been closed yet.
    /// </summary>
    Task<List<Auction>> GetExpiredActiveAuctionsAsync(DateTime now);

    /// <summary>
    /// Atomically sets AuctionEndingNotificationSent = true only if it is currently false.
    /// Returns true if the flag was successfully claimed (i.e., was false before this call).
    /// Returns false if the flag was already true (another process already claimed it).
    /// </summary>
    Task<bool> TryClaimEndingNotificationAsync(int auctionId);

    /// <summary>
    /// Atomically sets AutoClosureClaimedAt = UtcNow only if it is currently null (not yet claimed).
    /// Returns true if the claim was acquired by this call; false if another process already claimed it.
    /// Prevents multiple worker instances from closing the same auction simultaneously.
    /// </summary>
    Task<bool> TryClaimAutomaticClosureAsync(int auctionId);
}
