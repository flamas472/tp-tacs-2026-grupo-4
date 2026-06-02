using Figuritas.Api.Repositories;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model;
using Figuritas.Shared.Model.Subastas;

namespace Figuritas.Api.Services;

public class AuctionService
{
    private readonly IUserStickerRepository _userStickerRepo;
    private readonly IAuctionRepository _auctionRepo;
    private readonly IAuctionOfferRepository _offerRepo;
    private readonly IMissingStickerRepository _missingStickerRepo;
    private readonly AuctionWatchlistService _watchlistService;
    private readonly IUserRepository _userRepo;

    public AuctionService(
        IUserStickerRepository userStickerRepo,
        IAuctionRepository auctionRepo,
        IAuctionOfferRepository offerRepo,
        IMissingStickerRepository missingStickerRepo,
        AuctionWatchlistService watchlistService,
        IUserRepository userRepo)
    {
        _userStickerRepo = userStickerRepo;
        _auctionRepo = auctionRepo;
        _offerRepo = offerRepo;
        _missingStickerRepo = missingStickerRepo;
        _watchlistService = watchlistService;
        _userRepo = userRepo;
    }

    public async Task<List<AuctionResponseDTO>> GetMyAuctions(GetMyAuctionsDTO dto, int callerUserId)
    {
        var auctions = _auctionRepo.GetByAuctioneerId(callerUserId, dto.Status, dto.Page, dto.PageSize);
        var stickerIds = auctions.Select(a => a.UserStickerId).ToList();
        var auctionIds = auctions.Select(a => a.Id).ToList();
        var stickers = _userStickerRepo.GetMultipleByIdIncludingInactive(stickerIds).ToDictionary(us => us.Id);
        var user = _userRepo.GetById(callerUserId);
        var offerCounts = await _offerRepo.CountByAuctionIdsAsync(auctionIds);
        return auctions.Select(a =>
        {
            stickers.TryGetValue(a.UserStickerId, out var us);
            offerCounts.TryGetValue(a.Id, out var count);
            return MapToDto(a, us, user, count);
        }).ToList();
    }

    public async Task<List<AuctionResponseDTO>> GetAuctions(int page = 1, int pageSize = 20, int? excludeAuctioneerId = null, string? status = null)
    {
        var auctions = _auctionRepo.GetAll(page, pageSize, excludeAuctioneerId, status);
        var stickerIds = auctions.Select(a => a.UserStickerId).Distinct().ToList();
        var userIds = auctions.Select(a => a.AuctioneerId).Distinct().ToList();
        var auctionIds = auctions.Select(a => a.Id).ToList();
        var stickers = _userStickerRepo.GetMultipleByIdIncludingInactive(stickerIds).ToDictionary(us => us.Id);
        var users = _userRepo.GetByIds(userIds).ToDictionary(u => u.Id);
        var offerCounts = await _offerRepo.CountByAuctionIdsAsync(auctionIds);
        return auctions.Select(a =>
        {
            stickers.TryGetValue(a.UserStickerId, out var us);
            users.TryGetValue(a.AuctioneerId, out var user);
            offerCounts.TryGetValue(a.Id, out var count);
            return MapToDto(a, us, user, count);
        }).ToList();
    }

    public async Task<AuctionResponseDTO?> GetAuction(int id)
    {
        var auction = _auctionRepo.GetById(id);
        if (auction == null) return null;
        var us = _userStickerRepo.GetByIdIncludingInactive(auction.UserStickerId);
        var user = _userRepo.GetById(auction.AuctioneerId);
        var offerCounts = await _offerRepo.CountByAuctionIdsAsync(new List<int> { auction.Id });
        offerCounts.TryGetValue(auction.Id, out var offerCount);
        return MapToDto(auction, us, user, offerCount);
    }

    public async Task<List<AuctionOfferResponseDTO>> GetOffersForAuctionAsync(int auctionId)
    {
        var offers = await _offerRepo.GetAllByAuctionIdAsync(auctionId);
        var bidderIds = offers.Select(o => o.BidderId).Distinct().ToList();
        var bidders = _userRepo.GetByIds(bidderIds).ToDictionary(u => u.Id);
        return offers.Select(o =>
        {
            bidders.TryGetValue(o.BidderId, out var bidder);
            return MapOfferToDto(o, bidder?.Username ?? string.Empty);
        }).ToList();
    }

    public AuctionResponseDTO CreateAuction(int callerUserId, PostAuctionRequestDTO dto)
    {
        var us = _userStickerRepo.GetById(dto.UserStickerId);
        if (us == null)
            throw new InvalidOperationException("UserSticker not found in inventory.");

        if (us.UserId != callerUserId)
            throw new InvalidOperationException("UserSticker does not belong to the caller.");

        if (!us.CanBeAuctioned)
            throw new InvalidOperationException("UserSticker is not available for auction.");

        if (us.Quantity <= 0)
            throw new InvalidOperationException("UserSticker has no available stock.");

        if (dto.EndsAt <= DateTime.UtcNow)
            throw new InvalidOperationException("EndsAt must be a future date.");

        // Reserve one unit from stock
        us.Quantity -= 1;
        if (us.Quantity == 0)
        {
            us.Active = false;
        }
        _userStickerRepo.Update(us);

        var auction = new Auction
        {
            AuctioneerId = callerUserId,
            UserStickerId = dto.UserStickerId,
            MinimumOfferStickerIds = dto.MinimumOfferStickerIds,
            EndsAt = dto.EndsAt,
            Status = AuctionStatus.Active
        };

        _auctionRepo.Add(auction);

        var user = _userRepo.GetById(callerUserId);
        return MapToDto(auction, us, user);
    }

    public async Task<AuctionOfferResponseDTO> CreateOfferAsync(int bidderId, int auctionId, PostAuctionOfferRequestDTO dto)
    {
        if (dto.OfferedUserStickerIds == null || dto.OfferedUserStickerIds.Count == 0)
            throw new InvalidOperationException("Offered sticker list cannot be empty.");

        var duplicateIds = dto.OfferedUserStickerIds
            .GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Any())
            throw new InvalidOperationException(
                $"Duplicate sticker IDs are not allowed in a single offer. Repeated ID(s): {string.Join(", ", duplicateIds)}");

        var auction = _auctionRepo.GetById(auctionId);
        if (auction == null)
            throw new KeyNotFoundException("Auction not found.");

        if (DateTime.UtcNow > auction.EndsAt)
            throw new InvalidOperationException("The auction has already ended.");

        if (auction.Status != AuctionStatus.Active)
            throw new InvalidOperationException("The auction is not active.");

        if (bidderId == auction.AuctioneerId)
            throw new InvalidOperationException("The auctioneer cannot bid on their own auction.");

        var offeredUserStickers = _userStickerRepo.GetMultipleById(dto.OfferedUserStickerIds);

        if (offeredUserStickers.Count != dto.OfferedUserStickerIds.Count)
            throw new InvalidOperationException("One or more offered sticker IDs do not exist.");

        // Validate ownership and stock for each offered sticker
        foreach (var offeredUs in offeredUserStickers)
        {
            if (offeredUs.UserId != bidderId)
                throw new InvalidOperationException(
                    $"UserSticker {offeredUs.Id} does not belong to the bidder.");

            if (!offeredUs.Active || offeredUs.Quantity <= 0)
                throw new InvalidOperationException(
                    $"UserSticker {offeredUs.Id} has no available stock.");
        }

        // Guard 1: Prevent a bidder from competing against their own leading offer (auto-bid).
        if (auction.BestCurrentOfferId.HasValue)
        {
            var currentBestOffer = _offerRepo.GetById(auction.BestCurrentOfferId.Value);
            if (currentBestOffer != null && currentBestOffer.BidderId == bidderId)
                throw new InvalidOperationException(
                    "You already hold the current best offer. You cannot outbid yourself.");
        }

        // Validate minimum offer requirements: compare catalog sticker IDs
        var offeredCatalogStickerIds = offeredUserStickers.Select(us => us.Sticker.Id).ToHashSet();
        var missingRequired = auction.MinimumOfferStickerIds
            .Where(requiredId => !offeredCatalogStickerIds.Contains(requiredId))
            .ToList();

        if (missingRequired.Any())
            throw new InvalidOperationException(
                $"Offer does not meet minimum requirements. Missing catalog sticker ID(s): {string.Join(", ", missingRequired)}");

        // Guard 2: If the bidder already has a non-winning active offer on this auction, supersede it
        // and release the stock that was reserved for it before reserving stock for the new offer.
        var previousOffer = await _offerRepo.GetActiveBidderOfferAsync(auctionId, bidderId);
        if (previousOffer != null)
        {
            // Mark the previous offer as superseded
            previousOffer.Status = AuctionOfferStatus.Superseded;
            previousOffer.State = AuctionOfferState.Lost;
            _offerRepo.Update(previousOffer);

            // Release the stock reserved by the previous offer to avoid duplicate blocking
            var previousStickers = _userStickerRepo.GetMultipleByIdIncludingInactive(previousOffer.OfferedUserStickerIds);
            foreach (var prevSticker in previousStickers)
            {
                prevSticker.Quantity++;
                prevSticker.Active = true;
                _userStickerRepo.Update(prevSticker);
            }
        }

        // Reserve stock: decrement quantity for each offered sticker
        foreach (var offeredUs in offeredUserStickers)
        {
            offeredUs.Quantity--;
            if (offeredUs.Quantity <= 0)
            {
                offeredUs.Active = false;
            }
            _userStickerRepo.Update(offeredUs);
        }

        var offer = new AuctionOffer
        {
            AuctionId = auctionId,
            BidderId = bidderId,
            OfferedUserStickerIds = dto.OfferedUserStickerIds,
            Status = AuctionOfferStatus.Active,
            State = AuctionOfferState.Pending
        };

        _offerRepo.Add(offer);

        // Recalculate the best offer using all three criteria (replaces the previous
        // "last-offer-wins" assignment with a proper ranking computation).
        await RecalculateBestOfferAsync(auction);

        // Auto-add bidder to watchlist if not already watching
        await _watchlistService.EnsureWatchingAsync(bidderId, auctionId);

        return MapOfferToDto(offer);
    }

    /// <summary>
    /// Allows the auctioneer to manually accept a specific offer, closing the auction.
    /// Enforces ownership, active-status, offer-membership, and pending-state guards.
    /// Uses an atomic MongoDB update to prevent race conditions with the expiration worker.
    /// </summary>
    public async Task<AuctionResponseDTO> AcceptOfferAsync(int auctionId, int offerId, int callerUserId)
    {
        var auction = _auctionRepo.GetById(auctionId);
        if (auction == null)
            throw new KeyNotFoundException("Auction not found.");

        // Guard: only the auctioneer can accept offers.
        if (auction.AuctioneerId != callerUserId)
            throw new UnauthorizedAccessException("Only the auctioneer can accept offers on their auction.");

        if (auction.Status != AuctionStatus.Active)
            throw new InvalidOperationException("The auction is not active.");

        // Guard: the offer must belong to this auction.
        var offer = _offerRepo.GetById(offerId);
        if (offer == null || offer.AuctionId != auctionId)
            throw new KeyNotFoundException("The specified offer does not belong to this auction.");

        // Guard: only Pending offers can be accepted.
        if (offer.State != AuctionOfferState.Pending)
            throw new InvalidOperationException("The offer is no longer pending and cannot be accepted.");

        // Persist the auctioneer's explicit choice using a partial $set update conditioned on
        // Status == Active.  This avoids replacing the full document (which would reinstate
        // Status = Active if a concurrent worker already moved it to Closed/Cancelled).
        // ModifiedCount == 0 means the auction is already in a terminal state → abort.
        var selectionPersisted = await _auctionRepo.TrySetUserSelectedBestOfferAsync(auctionId, offerId);
        if (!selectionPersisted)
            throw new InvalidOperationException("Auction is no longer active and cannot be accepted.");

        // Atomic close: Active → Closed in MongoDB.
        // If ModifiedCount == 0, the worker closed the auction between our $set and this call.
        // The worker will use UserSelectedBestOfferId (just persisted) as the winning offer,
        // so we can abort gracefully — the correct winner is already guaranteed.
        var claimed = await _auctionRepo.TryCloseAuctionAtomicallyAsync(auctionId);
        if (!claimed)
            throw new InvalidOperationException("Auction is no longer active and cannot be accepted.");

        // Finalize: transition offer states and transfer inventory.
        await FinalizeClosedAuctionAsync(auctionId, winningOfferId: offerId);

        return MapToDto(_auctionRepo.GetById(auctionId)!);
    }

    /// <summary>
    /// Closes an expired auction automatically (called by the background worker, not by the auctioneer).
    /// Applies priority logic:
    ///   A) UserSelectedBestOfferId != null → that offer wins.
    ///   B) BestCurrentOfferId != null → system-ranked best offer wins.
    ///   C) No pending offers → auction is cancelled, auctioneer's stock restored.
    ///
    /// Uses an atomic MongoDB update to prevent race conditions with manual acceptance.
    /// Virtual to allow unit-test mocking without requiring an interface extraction.
    /// </summary>
    public virtual async Task<AuctionResponseDTO> CloseAuctionAutomatically(int auctionId)
    {
        var auction = _auctionRepo.GetById(auctionId);
        if (auction == null)
            throw new KeyNotFoundException("Auction not found.");

        if (auction.Status != AuctionStatus.Active)
            throw new InvalidOperationException("The auction is not active.");

        // Determine winning offer using priority order.
        int? winningOfferId = auction.UserSelectedBestOfferId ?? auction.BestCurrentOfferId;

        if (winningOfferId == null)
        {
            // Case C: no offers — cancel the auction and restore auctioneer's stock.
            var cancelled = await _auctionRepo.TryCancelAuctionAtomicallyAsync(auctionId);
            if (!cancelled)
                throw new InvalidOperationException("Auction is no longer active.");

            var auctionedSticker = _userStickerRepo.GetByIdIncludingInactive(auction.UserStickerId);
            if (auctionedSticker != null)
            {
                auctionedSticker.Quantity++;
                auctionedSticker.Active = true;
                _userStickerRepo.Update(auctionedSticker);
            }
        }
        else
        {
            // Cases A & B: close and finalize with a winner.
            var closed = await _auctionRepo.TryCloseAuctionAtomicallyAsync(auctionId);
            if (!closed)
                throw new InvalidOperationException("Auction is no longer active.");

            await FinalizeClosedAuctionAsync(auctionId, winningOfferId);
        }

        return MapToDto(_auctionRepo.GetById(auctionId)!);
    }

    /// <summary>
    /// Cancels a pending offer placed by the authenticated bidder on an active auction.
    ///
    /// Guards (in order):
    ///   1. Auction must be Active.
    ///   2. Caller must be the offer owner (BidderId) → UnauthorizedAccessException (→ 403).
    ///   3. The offer must not be the current leading offer
    ///      (UserSelectedBestOfferId ?? BestCurrentOfferId) → InvalidOperationException (→ 400).
    ///
    /// Atomicity (Escenario A):
    ///   Uses <see cref="IAuctionOfferRepository.TryCancelOfferAtomicallyAsync"/> with a
    ///   <c>State == Pending</c> filter.  If ModifiedCount == 0 the offer was already
    ///   cancelled / won / lost and we abort without touching stock.
    ///   Stock is released only after the atomic transition succeeds.
    /// </summary>
    public async Task<AuctionOfferResponseDTO> CancelOfferAsync(int auctionId, int offerId, int callerUserId)
    {
        var auction = _auctionRepo.GetById(auctionId);
        if (auction == null)
            throw new KeyNotFoundException("Auction not found.");

        if (auction.Status != AuctionStatus.Active)
            throw new InvalidOperationException("The auction is not active.");

        var offer = _offerRepo.GetById(offerId);
        if (offer == null || offer.AuctionId != auctionId)
            throw new KeyNotFoundException("Offer not found for this auction.");

        // Guard 2: only the owner of the offer can cancel it.
        if (offer.BidderId != callerUserId)
            throw new UnauthorizedAccessException("Only the bidder who placed this offer can cancel it.");

        // Guard 3: cannot cancel the current leading offer.
        var leadingOfferId = auction.UserSelectedBestOfferId ?? auction.BestCurrentOfferId;
        if (leadingOfferId.HasValue && leadingOfferId.Value == offerId)
            throw new InvalidOperationException(
                "You cannot cancel the current leading offer. It is the best offer for this auction.");

        // Atomic transition: Pending → Cancelled.
        // If ModifiedCount == 0 the offer is no longer Pending — abort without releasing stock.
        var cancelled = await _offerRepo.TryCancelOfferAtomicallyAsync(offerId);
        if (!cancelled)
            throw new InvalidOperationException(
                "The offer is no longer pending and cannot be cancelled.");

        // Release the stock that was reserved when the offer was created.
        var reservedStickers = _userStickerRepo.GetMultipleByIdIncludingInactive(offer.OfferedUserStickerIds);
        foreach (var sticker in reservedStickers)
        {
            sticker.Quantity++;
            sticker.Active = true;
            _userStickerRepo.Update(sticker);
        }

        // Reload the offer (state is now Cancelled in DB) and recalculate the ranking.
        var updatedOffer = _offerRepo.GetById(offerId)!;
        await RecalculateBestOfferAsync(auction);

        return MapOfferToDto(updatedOffer);
    }

    /// <summary>
    /// Appends additional stickers to an existing pending offer (accumulative modification).
    ///
    /// Guards (in order):
    ///   1. Auction must be Active.
    ///   2. Caller must be the offer owner → UnauthorizedAccessException (→ 403).
    ///   3. Offer must not be the current leading offer → InvalidOperationException (→ 400).
    ///   4. AdditionalStickerIds must not contain duplicates within itself → InvalidOperationException (→ 400).
    ///   5. New IDs must not overlap with already-committed IDs → InvalidOperationException (→ 400).
    ///
    /// Atomicity (Escenario B):
    ///   Uses <see cref="IAuctionOfferRepository.AppendOfferedStickersAsync"/> which issues a
    ///   MongoDB <c>$push/$each</c> update conditioned on <c>State == Pending</c>.
    ///   Two concurrent PATCHes consolidate both arrays sequentially in MongoDB.
    ///   If ModifiedCount == 0 the offer is no longer Pending → abort without reserving stock.
    /// </summary>
    public async Task<AuctionOfferResponseDTO> UpdateOfferAsync(
        int auctionId, int offerId, UpdateAuctionOfferRequestDTO dto, int callerUserId)
    {
        var auction = _auctionRepo.GetById(auctionId);
        if (auction == null)
            throw new KeyNotFoundException("Auction not found.");

        if (auction.Status != AuctionStatus.Active)
            throw new InvalidOperationException("The auction is not active.");

        var offer = _offerRepo.GetById(offerId);
        if (offer == null || offer.AuctionId != auctionId)
            throw new KeyNotFoundException("Offer not found for this auction.");

        // Guard 2: only the owner can modify the offer.
        if (offer.BidderId != callerUserId)
            throw new UnauthorizedAccessException("Only the bidder who placed this offer can modify it.");

        // Guard 3: cannot modify the current leading offer.
        var leadingOfferId = auction.UserSelectedBestOfferId ?? auction.BestCurrentOfferId;
        if (leadingOfferId.HasValue && leadingOfferId.Value == offerId)
            throw new InvalidOperationException(
                "You cannot modify the current leading offer. It is the best offer for this auction.");

        if (dto.AdditionalStickerIds == null || dto.AdditionalStickerIds.Count == 0)
            throw new InvalidOperationException("AdditionalStickerIds cannot be empty.");

        // Guard 4: no duplicates within the new list.
        var duplicateIds = dto.AdditionalStickerIds
            .GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Any())
            throw new InvalidOperationException(
                $"Duplicate sticker IDs in AdditionalStickerIds: {string.Join(", ", duplicateIds)}");

        // Guard 5: new IDs must not overlap with already-committed IDs.
        var existingSet = offer.OfferedUserStickerIds.ToHashSet();
        var overlapping = dto.AdditionalStickerIds.Where(id => existingSet.Contains(id)).ToList();
        if (overlapping.Any())
            throw new InvalidOperationException(
                $"The following sticker IDs are already committed to this offer: {string.Join(", ", overlapping)}");

        // Validate stock and ownership of the new stickers.
        var newStickers = _userStickerRepo.GetMultipleById(dto.AdditionalStickerIds);
        if (newStickers.Count != dto.AdditionalStickerIds.Count)
            throw new InvalidOperationException("One or more additional sticker IDs do not exist.");

        foreach (var sticker in newStickers)
        {
            if (sticker.UserId != callerUserId)
                throw new InvalidOperationException(
                    $"UserSticker {sticker.Id} does not belong to the caller.");
            if (!sticker.Active || sticker.Quantity <= 0)
                throw new InvalidOperationException(
                    $"UserSticker {sticker.Id} has no available stock.");
        }

        // Reserve stock for the new stickers before persisting the append.
        // If the atomic append fails (offer no longer Pending), we must roll back the stock.
        foreach (var sticker in newStickers)
        {
            sticker.Quantity--;
            if (sticker.Quantity <= 0)
                sticker.Active = false;
            _userStickerRepo.Update(sticker);
        }

        // Atomic append: $push/$each conditioned on State == Pending.
        // Escenario B: two concurrent PATCHes both land without overwriting each other.
        // Escenario D: if the auction closed concurrently, ModifiedCount == 0 → rollback stock.
        var appended = await _offerRepo.AppendOfferedStickersAsync(offerId, dto.AdditionalStickerIds);
        if (!appended)
        {
            // Roll back reserved stock since the append did not persist.
            foreach (var sticker in newStickers)
            {
                sticker.Quantity++;
                sticker.Active = true;
                _userStickerRepo.Update(sticker);
            }
            throw new InvalidOperationException(
                "The offer is no longer pending and cannot be modified.");
        }

        // Reload the offer to get the updated OfferedUserStickerIds list, then recalculate.
        var updatedOffer = _offerRepo.GetById(offerId)!;
        await RecalculateBestOfferAsync(auction);

        return MapOfferToDto(updatedOffer);
    }

    /// <summary>
    /// Allows the auctioneer to pre-select a preferred offer without closing the auction.
    /// The selection is persisted in <see cref="Auction.UserSelectedBestOfferId"/> and will be
    /// used as the winning offer when the auction eventually closes (either manually or via the worker).
    ///
    /// Guards (in order):
    ///   1. The auction must exist.
    ///   2. The caller must be the auctioneer → <see cref="UnauthorizedAccessException"/> (→ 403).
    ///   3. The auction must be Active.
    ///   4. OfferId must be > 0 — deselection is not supported.
    ///   5. The offer must belong to this auction and be in <see cref="AuctionOfferState.Pending"/>.
    ///   6. Persists via <see cref="IAuctionRepository.TrySetUserSelectedBestOfferAsync"/> which
    ///      uses a <c>Status == Active</c> guard. If <c>ModifiedCount == 0</c> the auction is no
    ///      longer active and we abort gracefully.
    /// </summary>
    public async Task<AuctionResponseDTO> SelectBestOfferAsync(int auctionId, int offerId, int callerUserId)
    {
        var auction = _auctionRepo.GetById(auctionId);
        if (auction == null)
            throw new KeyNotFoundException("Auction not found.");

        if (auction.AuctioneerId != callerUserId)
            throw new UnauthorizedAccessException("Only the auctioneer can pre-select an offer.");

        if (auction.Status != AuctionStatus.Active)
            throw new InvalidOperationException("The auction is not active.");

        if (offerId <= 0)
            throw new InvalidOperationException("OfferId must be a valid positive integer. Deselection is not supported.");

        var offer = _offerRepo.GetById(offerId);
        if (offer == null || offer.AuctionId != auctionId)
            throw new KeyNotFoundException("The specified offer does not belong to this auction.");

        if (offer.State != AuctionOfferState.Pending)
            throw new InvalidOperationException("Only Pending offers can be pre-selected.");

        var persisted = await _auctionRepo.TrySetUserSelectedBestOfferAsync(auctionId, offerId);
        if (!persisted)
            throw new InvalidOperationException("Auction is no longer active and the selection could not be saved.");

        return MapToDto(_auctionRepo.GetById(auctionId)!);
    }

    /// <summary>
    /// Legacy endpoint: auctioneer closes their auction with an optional explicit winner.
    /// Preserved for backward compatibility with the existing POST /api/auctions/{id}/close endpoint.
    /// </summary>
    public async Task<AuctionResponseDTO> CloseAuction(int auctionId, int? winningOfferId, int callerUserId)
    {
        var auction = _auctionRepo.GetById(auctionId);
        if (auction == null)
            throw new KeyNotFoundException("Auction not found.");

        // Ownership validation before any side-effects.
        if (auction.AuctioneerId != callerUserId)
            throw new InvalidOperationException("Only the auctioneer can close their own auction.");

        // Atomic close: transition Active → Closed/Cancelled BEFORE executing any side-effects.
        // This prevents a double-execution scenario where a concurrent worker closes the auction
        // between our in-memory status check and the final Update, which would cause all
        // inventory side-effects (stock transfers, offer state changes) to run twice.
        bool atomicallyClosed;
        if (winningOfferId == null)
        {
            atomicallyClosed = await _auctionRepo.TryCancelAuctionAtomicallyAsync(auctionId);
        }
        else
        {
            atomicallyClosed = await _auctionRepo.TryCloseAuctionAtomicallyAsync(auctionId);
        }

        if (!atomicallyClosed)
            throw new InvalidOperationException("The auction is not active.");

        var allOffers = await _offerRepo.GetAllByAuctionIdAsync(auctionId);

        if (winningOfferId == null || !allOffers.Any())
        {
            // No winner: return reserved stock of auctioned sticker to seller.
            var auctionedSticker = _userStickerRepo.GetByIdIncludingInactive(auction.UserStickerId);
            if (auctionedSticker != null)
            {
                auctionedSticker.Quantity++;
                auctionedSticker.Active = true;
                _userStickerRepo.Update(auctionedSticker);
            }

            // Return reserved stickers only for Active offers — Superseded offers had their stock
            // released at the time they were superseded, so including them here would double-return.
            var activeOffers = allOffers.Where(o => o.Status == AuctionOfferStatus.Active).ToList();
            foreach (var offer in activeOffers)
            {
                offer.State = AuctionOfferState.Lost;
                _offerRepo.Update(offer);

                var bidderStickers = _userStickerRepo.GetMultipleByIdIncludingInactive(offer.OfferedUserStickerIds);
                foreach (var bidderSticker in bidderStickers)
                {
                    bidderSticker.Quantity++;
                    bidderSticker.Active = true;
                    _userStickerRepo.Update(bidderSticker);
                }
            }
        }
        else
        {
            // There is a winner.
            var winningOffer = allOffers.FirstOrDefault(o => o.Id == winningOfferId)
                ?? throw new InvalidOperationException("The specified winning offer was not found for this auction.");

            // Transfer auctioned sticker (reserved/inactive) to winner's inventory.
            var auctionedSticker = _userStickerRepo.GetByIdIncludingInactive(auction.UserStickerId);
            if (auctionedSticker != null)
            {
                // Use inclusive lookup to avoid duplicates when the winner already has an inactive record.
                var winnerExisting = await _userStickerRepo.GetByUserIdAndCatalogIdIncludingInactiveAsync(
                    winningOffer.BidderId, auctionedSticker.Sticker.Id);
                if (winnerExisting != null)
                {
                    winnerExisting.Quantity++;
                    winnerExisting.Active = true;
                    _userStickerRepo.Update(winnerExisting);
                }
                else
                {
                    var newWinnerSticker = new UserSticker
                    {
                        Sticker = auctionedSticker.Sticker,
                        UserId = winningOffer.BidderId,
                        Quantity = 1,
                        Active = true,
                        CanBeDirectlyExchanged = false,
                        CanBeAuctioned = false
                    };
                    _userStickerRepo.Add(newWinnerSticker);
                }
            }

            // Transfer winning offer stickers (reserved/inactive) to auctioneer.
            var winningStickers = _userStickerRepo.GetMultipleByIdIncludingInactive(winningOffer.OfferedUserStickerIds);
            foreach (var winningSticker in winningStickers)
            {
                // Use inclusive lookup to avoid duplicates when the auctioneer already has an inactive record.
                var auctioneerExisting = await _userStickerRepo.GetByUserIdAndCatalogIdIncludingInactiveAsync(
                    auction.AuctioneerId, winningSticker.Sticker.Id);
                if (auctioneerExisting != null)
                {
                    auctioneerExisting.Quantity++;
                    auctioneerExisting.Active = true;
                    _userStickerRepo.Update(auctioneerExisting);
                }
                else
                {
                    var newAuctioneerSticker = new UserSticker
                    {
                        Sticker = winningSticker.Sticker,
                        UserId = auction.AuctioneerId,
                        Quantity = 1,
                        Active = true,
                        CanBeDirectlyExchanged = false,
                        CanBeAuctioned = false
                    };
                    _userStickerRepo.Add(newAuctioneerSticker);
                }
            }

            // Automation: clean up MissingSticker records for winner and auctioneer.
            if (auctionedSticker != null)
            {
                await _missingStickerRepo.DeleteAsync(winningOffer.BidderId, auctionedSticker.Sticker.Id);
            }
            foreach (var winningSticker in winningStickers)
            {
                await _missingStickerRepo.DeleteAsync(auction.AuctioneerId, winningSticker.Sticker.Id);
            }

            // Mark winning offer as Won.
            winningOffer.State = AuctionOfferState.Won;
            _offerRepo.Update(winningOffer);

            // Return losing bidders' reserved stickers.
            // Skip Superseded offers: their stock was already released when they were superseded.
            var losingOffers = allOffers
                .Where(o => o.Id != winningOfferId && o.Status == AuctionOfferStatus.Active)
                .ToList();
            foreach (var losingOffer in losingOffers)
            {
                losingOffer.State = AuctionOfferState.Lost;
                _offerRepo.Update(losingOffer);

                var loserStickers = _userStickerRepo.GetMultipleByIdIncludingInactive(losingOffer.OfferedUserStickerIds);
                foreach (var loserSticker in loserStickers)
                {
                    loserSticker.Quantity++;
                    loserSticker.Active = true;
                    _userStickerRepo.Update(loserSticker);
                }
            }
        }

        // The atomic operation already persisted the terminal status; reload to return current state.
        return MapToDto(_auctionRepo.GetById(auctionId)!);
    }

    /// <summary>
    /// Returns a paged list of all bids placed by the authenticated user on auctions created
    /// by other users. Each item is enriched with the auction's sticker info and a flag
    /// indicating whether the bid is currently the system-ranked or auctioneer-selected winner.
    /// </summary>
    public async Task<List<MyBidResponseDTO>> GetMyBidsAsync(int bidderId, int page, int pageSize)
    {
        var offers = await _offerRepo.GetByBidderIdAsync(bidderId, page, pageSize);
        if (offers.Count == 0)
            return new List<MyBidResponseDTO>();

        // Batch-load all parent auctions to avoid N+1 queries.
        var auctionIds = offers.Select(o => o.AuctionId).Distinct().ToList();
        var auctionMap = auctionIds
            .Select(id => _auctionRepo.GetById(id))
            .Where(a => a != null)
            .ToDictionary(a => a!.Id);

        // Batch-load all UserStickers referenced by the auctions.
        var userStickerIds = auctionMap.Values.Select(a => a!.UserStickerId).Distinct().ToList();
        var userStickerMap = _userStickerRepo.GetMultipleByIdIncludingInactive(userStickerIds)
            .ToDictionary(us => us.Id);

        return offers.Select(offer =>
        {
            auctionMap.TryGetValue(offer.AuctionId, out var auction);
            UserSticker? us = null;
            if (auction != null)
                userStickerMap.TryGetValue(auction.UserStickerId, out us);

            // IsCurrentWinner: true if this offer is either the system-ranked best or the
            // auctioneer-explicitly-selected winner.
            var isCurrentWinner = auction != null &&
                (auction.BestCurrentOfferId == offer.Id || auction.UserSelectedBestOfferId == offer.Id);

            return new MyBidResponseDTO
            {
                OfferId = offer.Id,
                AuctionId = offer.AuctionId,
                StickerNumber = us?.Sticker.Number ?? 0,
                StickerDescription = us?.Sticker.Description ?? string.Empty,
                StickerNationalTeam = us?.Sticker.NationalTeam ?? string.Empty,
                StickerTeam = us?.Sticker.Team ?? string.Empty,
                AuctionStatus = auction?.Status.ToString() ?? string.Empty,
                CreatedAt = offer.CreatedAt,
                State = offer.State.ToString(),
                IsCurrentWinner = isCurrentWinner
            };
        }).ToList();
    }

    /// <summary>
    /// Core finalization logic shared by <see cref="AcceptOfferAsync"/> and
    /// <see cref="CloseAuctionAutomatically"/>. Called after the auction status has
    /// already been atomically transitioned to Closed.
    ///
    /// Responsibilities:
    /// - Marks the winning offer as <see cref="AuctionOfferState.Won"/>.
    /// - Marks all other Pending offers as <see cref="AuctionOfferState.Lost"/> and restores
    ///   their bidders' reserved stock (Quantity++, Active = true).
    /// - Transfers the auctioned sticker to the winner's inventory.
    /// - Transfers the winning offer's stickers to the auctioneer's inventory.
    /// - Cleans up MissingSticker records for both parties.
    /// </summary>
    private async Task FinalizeClosedAuctionAsync(int auctionId, int? winningOfferId)
    {
        var auction = _auctionRepo.GetById(auctionId)
            ?? throw new KeyNotFoundException($"Auction {auctionId} not found during finalization.");

        var allOffers = await _offerRepo.GetAllByAuctionIdAsync(auctionId);

        if (winningOfferId == null || !allOffers.Any(o => o.State == AuctionOfferState.Pending))
        {
            // Desert auction: restore auctioneer's stock and mark all pending offers as Lost.
            var auctionedSticker = _userStickerRepo.GetByIdIncludingInactive(auction.UserStickerId);
            if (auctionedSticker != null)
            {
                auctionedSticker.Quantity++;
                auctionedSticker.Active = true;
                _userStickerRepo.Update(auctionedSticker);
            }

            var pendingOffers = allOffers.Where(o => o.State == AuctionOfferState.Pending).ToList();
            foreach (var offer in pendingOffers)
            {
                offer.State = AuctionOfferState.Lost;
                _offerRepo.Update(offer);

                var bidderStickers = _userStickerRepo.GetMultipleByIdIncludingInactive(offer.OfferedUserStickerIds);
                foreach (var bidderSticker in bidderStickers)
                {
                    bidderSticker.Quantity++;
                    bidderSticker.Active = true;
                    _userStickerRepo.Update(bidderSticker);
                }
            }
            return;
        }

        var winningOffer = allOffers.FirstOrDefault(o => o.Id == winningOfferId)
            ?? throw new InvalidOperationException($"Winning offer {winningOfferId} not found for auction {auctionId}.");

        // Transfer auctioned sticker (reserved/inactive) to winner's inventory.
        var auctionedStickerForTransfer = _userStickerRepo.GetByIdIncludingInactive(auction.UserStickerId);
        if (auctionedStickerForTransfer != null)
        {
            // Use inclusive lookup to avoid duplicates when the winner already has an inactive record.
            var winnerExisting = await _userStickerRepo.GetByUserIdAndCatalogIdIncludingInactiveAsync(
                winningOffer.BidderId, auctionedStickerForTransfer.Sticker.Id);
            if (winnerExisting != null)
            {
                winnerExisting.Quantity++;
                winnerExisting.Active = true;
                _userStickerRepo.Update(winnerExisting);
            }
            else
            {
                var newWinnerSticker = new UserSticker
                {
                    Sticker = auctionedStickerForTransfer.Sticker,
                    UserId = winningOffer.BidderId,
                    Quantity = 1,
                    Active = true,
                    CanBeDirectlyExchanged = false,
                    CanBeAuctioned = false
                };
                _userStickerRepo.Add(newWinnerSticker);
            }
        }

        // Transfer winning offer stickers (reserved/inactive) to auctioneer.
        var winningStickers = _userStickerRepo.GetMultipleByIdIncludingInactive(winningOffer.OfferedUserStickerIds);
        foreach (var winningSticker in winningStickers)
        {
            // Use inclusive lookup to avoid duplicates when the auctioneer already has an inactive record.
            var auctioneerExisting = await _userStickerRepo.GetByUserIdAndCatalogIdIncludingInactiveAsync(
                auction.AuctioneerId, winningSticker.Sticker.Id);
            if (auctioneerExisting != null)
            {
                auctioneerExisting.Quantity++;
                auctioneerExisting.Active = true;
                _userStickerRepo.Update(auctioneerExisting);
            }
            else
            {
                var newAuctioneerSticker = new UserSticker
                {
                    Sticker = winningSticker.Sticker,
                    UserId = auction.AuctioneerId,
                    Quantity = 1,
                    Active = true,
                    CanBeDirectlyExchanged = false,
                    CanBeAuctioned = false
                };
                _userStickerRepo.Add(newAuctioneerSticker);
            }
        }

        // Clean up MissingSticker records for winner and auctioneer.
        if (auctionedStickerForTransfer != null)
        {
            await _missingStickerRepo.DeleteAsync(winningOffer.BidderId, auctionedStickerForTransfer.Sticker.Id);
        }
        foreach (var winningSticker in winningStickers)
        {
            await _missingStickerRepo.DeleteAsync(auction.AuctioneerId, winningSticker.Sticker.Id);
        }

        // Mark winning offer as Won.
        winningOffer.State = AuctionOfferState.Won;
        _offerRepo.Update(winningOffer);

        // Mark all remaining Pending offers as Lost and restore their stock.
        // Superseded offers already had their stock released at supersession time.
        var losingOffers = allOffers
            .Where(o => o.Id != winningOfferId && o.State == AuctionOfferState.Pending)
            .ToList();
        foreach (var losingOffer in losingOffers)
        {
            losingOffer.State = AuctionOfferState.Lost;
            _offerRepo.Update(losingOffer);

            var loserStickers = _userStickerRepo.GetMultipleByIdIncludingInactive(losingOffer.OfferedUserStickerIds);
            foreach (var loserSticker in loserStickers)
            {
                loserSticker.Quantity++;
                loserSticker.Active = true;
                _userStickerRepo.Update(loserSticker);
            }
        }
    }

    /// <summary>
    /// Recalculates <see cref="Auction.BestCurrentOfferId"/> for the given auction using
    /// the canonical three-criteria ranking:
    ///   1. Most missing stickers of the auctioneer covered (higher is better).
    ///   2. Most total stickers offered (higher is better).
    ///   3. Oldest offer (lower CreatedAt wins — FIFO).
    ///
    /// All data is fetched in batch to avoid N+1 queries:
    ///   - One query for the auctioneer's missing sticker IDs.
    ///   - One query for all active offers on this auction.
    ///   - One batch query for all UserStickers referenced by those offers.
    ///
    /// The result is persisted immediately. If there are no active offers the field is set to null.
    /// </summary>
    private async Task RecalculateBestOfferAsync(Auction auction)
    {
        // Batch 1: auctioneer's missing sticker catalog IDs (projected — no full documents loaded).
        var auctioneerMissingIds = (await _missingStickerRepo.GetStickerIdsByUserIdAsync(auction.AuctioneerId))
                                   .ToHashSet();

        // Batch 2: all Pending offers for this auction (server-side filtered).
        var activeOffers = await _offerRepo.GetByAuctionIdAsync(auction.Id);
        var rankableOffers = activeOffers.Where(o => o.Status == AuctionOfferStatus.Active).ToList();

        if (rankableOffers.Count == 0)
        {
            auction.BestCurrentOfferId = null;
            _auctionRepo.Update(auction);
            return;
        }

        // Batch 3: all UserStickers referenced by active offers in a single query.
        var allOfferedIds = rankableOffers
            .SelectMany(o => o.OfferedUserStickerIds)
            .Distinct()
            .ToList();

        var userStickerMap = _userStickerRepo
            .GetMultipleByIdIncludingInactive(allOfferedIds)
            .ToDictionary(us => us.Id);

        // Rank each offer by the three criteria.
        var bestOffer = rankableOffers
            .OrderByDescending(o =>
            {
                // Criterion 1: count of the auctioneer's missing stickers covered by this offer.
                var offeredCatalogIds = o.OfferedUserStickerIds
                    .Where(usId => userStickerMap.ContainsKey(usId))
                    .Select(usId => userStickerMap[usId].Sticker.Id)
                    .ToHashSet();
                return offeredCatalogIds.Count(sid => auctioneerMissingIds.Contains(sid));
            })
            .ThenByDescending(o => o.OfferedUserStickerIds.Count) // Criterion 2: total stickers offered.
            .ThenBy(o => o.CreatedAt)                              // Criterion 3: FIFO — oldest wins.
            .First();

        auction.BestCurrentOfferId = bestOffer.Id;
        _auctionRepo.Update(auction);
    }

    private static AuctionResponseDTO MapToDto(Auction auction, UserSticker? us = null, User? user = null, int offerCount = 0) => new()
    {
        Id = auction.Id,
        AuctioneerId = auction.AuctioneerId,
        UserStickerId = auction.UserStickerId,
        MinimumOfferStickerIds = auction.MinimumOfferStickerIds,
        Status = auction.Status.ToString(),
        CreatedAt = auction.CreatedAt,
        EndsAt = auction.EndsAt,
        BestCurrentOfferId = auction.BestCurrentOfferId,
        UserSelectedBestOfferId = auction.UserSelectedBestOfferId,
        StickerNumber = us?.Sticker.Number ?? 0,
        StickerDescription = us?.Sticker.Description ?? string.Empty,
        StickerNationalTeam = us?.Sticker.NationalTeam ?? string.Empty,
        StickerTeam = us?.Sticker.Team ?? string.Empty,
        StickerImageUrl = us?.Sticker.ImageUrl ?? string.Empty,
        AuctioneerUsername = user?.Username ?? string.Empty,
        OfferCount = offerCount
    };

    private static AuctionOfferResponseDTO MapOfferToDto(AuctionOffer offer, string bidderUsername = "") => new()
    {
        Id = offer.Id,
        AuctionId = offer.AuctionId,
        BidderId = offer.BidderId,
        OfferedUserStickerIds = offer.OfferedUserStickerIds,
        CreatedAt = offer.CreatedAt,
        State = offer.State.ToString(),
        BidderUsername = bidderUsername
    };
}
