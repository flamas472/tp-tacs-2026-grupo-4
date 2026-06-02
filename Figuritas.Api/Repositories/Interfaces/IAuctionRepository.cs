using Figuritas.Shared.Model;

namespace Figuritas.Api.Repositories;

public interface IAuctionRepository
{
    /// <summary>
    /// Returns a paged list of auctions visible in the marketplace.
    /// When <paramref name="status"/> is null or empty, only Active auctions are returned
    /// (default marketplace behavior).
    /// When <paramref name="status"/> is provided, only auctions matching that status are returned.
    /// Accepted values: "Active", "Closed", "Cancelled".
    /// </summary>
    List<Auction> GetAll(int page = 1, int pageSize = 20, int? excludeAuctioneerId = null, string? status = null);
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

    /// <summary>
    /// Atomically transitions the auction from <see cref="AuctionStatus.Active"/> to
    /// <see cref="AuctionStatus.Closed"/> using a MongoDB filter that includes the
    /// current status as a condition.
    /// Returns true if the update was applied (the auction was Active and is now Closed).
    /// Returns false if the auction was already in a terminal state (Closed or Cancelled),
    /// which indicates a concurrent closure won the race — the caller should abort gracefully.
    /// </summary>
    Task<bool> TryCloseAuctionAtomicallyAsync(int auctionId);

    /// <summary>
    /// Atomically transitions the auction from <see cref="AuctionStatus.Active"/> to
    /// <see cref="AuctionStatus.Cancelled"/> using a MongoDB filter that includes the
    /// current status as a condition.
    /// Returns true if the update was applied; false if already in a terminal state.
    /// </summary>
    Task<bool> TryCancelAuctionAtomicallyAsync(int auctionId);

    /// <summary>
    /// Atomically sets <c>UserSelectedBestOfferId</c> to <paramref name="offerId"/> only if the
    /// auction is still <see cref="AuctionStatus.Active"/>.
    /// Uses a MongoDB <c>$set</c> partial update (not a full document replace) so that a concurrent
    /// worker closure of the same auction does not get overwritten.
    /// Returns true if the field was updated (auction was Active); false if the auction is already
    /// in a terminal state, meaning the caller should abort without proceeding to close.
    /// </summary>
    Task<bool> TrySetUserSelectedBestOfferAsync(int auctionId, int offerId);
}
