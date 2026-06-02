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

    /// <summary>
    /// Atomically transitions the offer from <see cref="AuctionOfferState.Pending"/> to
    /// <see cref="AuctionOfferState.Cancelled"/> using a MongoDB filter that includes
    /// <c>State == Pending</c> as a guard condition.
    /// Returns true if the state was successfully changed (offer was Pending and is now Cancelled).
    /// Returns false if the offer was not in Pending state, meaning it was already cancelled,
    /// won, or lost — the caller should abort gracefully without releasing stock.
    /// </summary>
    Task<bool> TryCancelOfferAtomicallyAsync(int offerId);

    /// <summary>
    /// Atomically appends <paramref name="additionalStickerIds"/> to the offer's
    /// <see cref="AuctionOffer.OfferedUserStickerIds"/> list using a MongoDB <c>$push/$each</c>
    /// update conditioned on <c>State == Pending</c>.
    /// Returns true if the update was applied (offer was still Pending).
    /// Returns false if the offer was no longer Pending (closed, won, lost, or cancelled),
    /// meaning the caller should abort without reserving any additional stock.
    /// Using <c>$push/$each</c> instead of a full document replace guarantees that two
    /// concurrent PATCH requests consolidate both arrays sequentially in MongoDB without
    /// either overwriting the other (Escenario B).
    /// </summary>
    Task<bool> AppendOfferedStickersAsync(int offerId, List<int> additionalStickerIds);
}
