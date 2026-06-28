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

        // Batch-load all offered stickers in a single query, including those that have been
        // deactivated (Active=false) because their stock was fully reserved for this offer.
        var allOfferedIds = offers.SelectMany(o => o.OfferedUserStickerIds).Distinct().ToList();
        var stickerMap = _userStickerRepo
            .GetMultipleByIdIncludingInactive(allOfferedIds)
            .ToDictionary(us => us.Id);

        return offers.Select(o =>
        {
            bidders.TryGetValue(o.BidderId, out var bidder);
            var previews = o.OfferedUserStickerIds
                .Where(id => stickerMap.ContainsKey(id))
                .Select(id => stickerMap[id])
                .Select(us => new StickerPreviewDTO
                {
                    UserStickerId = us.Id,
                    Number        = us.Sticker.Number,
                    ImageUrl      = us.Sticker.ImageUrl,
                    Description   = us.Sticker.Description
                })
                .ToList();
            return MapOfferToDto(o, bidder?.Username ?? string.Empty, previews);
        }).ToList();
    }

    public async Task<AuctionResponseDTO> CreateAuction(int callerUserId, PostAuctionRequestDTO dto)
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

        // Atomically reserve one unit of stock. TryReserveOneUnitAsync guards Quantity > 0 && Active == true,
        // so a concurrent auction creation or proposal for the same sticker cannot both succeed.
        var reserved = await _userStickerRepo.TryReserveOneUnitAsync(dto.UserStickerId);
        if (!reserved)
            throw new InvalidOperationException("UserSticker has no available stock.");

        await _userStickerRepo.DeactivateIfEmptyAsync(dto.UserStickerId);

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
            previousOffer.Status = AuctionOfferStatus.Superseded;
            previousOffer.State = AuctionOfferState.Lost;
            _offerRepo.Update(previousOffer);

            var previousStickers = _userStickerRepo.GetMultipleByIdIncludingInactive(previousOffer.OfferedUserStickerIds);
            foreach (var prevSticker in previousStickers)
                await _userStickerRepo.IncrementQuantityAndActivateAsync(prevSticker.Id);
        }

        // Reserve stock atomically for each offered sticker. On any failure, rollback the
        // already-reserved stickers before propagating the error.
        var reservedIds = new List<int>();
        foreach (var offeredUs in offeredUserStickers)
        {
            var ok = await _userStickerRepo.TryReserveOneUnitAsync(offeredUs.Id);
            if (!ok)
            {
                foreach (var rid in reservedIds)
                    await _userStickerRepo.IncrementQuantityAndActivateAsync(rid);
                throw new InvalidOperationException(
                    $"UserSticker {offeredUs.Id} has insufficient stock.");
            }
            reservedIds.Add(offeredUs.Id);
            await _userStickerRepo.DeactivateIfEmptyAsync(offeredUs.Id);
        }

        var offer = new AuctionOffer
        {
            AuctionId = auctionId,
            BidderId = bidderId,
            OfferedUserStickerIds = dto.OfferedUserStickerIds,
            Status = AuctionOfferStatus.Active,
            State = AuctionOfferState.Pending
        };

        // TAREA 6: Re-validate auction state after stock has been reserved to close the window
        // between the initial Active check and the offer insertion.
        var freshAuction = _auctionRepo.GetById(auctionId);
        if (freshAuction == null || freshAuction.Status != AuctionStatus.Active || DateTime.UtcNow > freshAuction.EndsAt)
        {
            foreach (var rid in reservedIds)
                await _userStickerRepo.IncrementQuantityAndActivateAsync(rid);
            throw new InvalidOperationException("The auction has expired or is no longer active.");
        }

        // TAREA 5: The unique partial index on (AuctionId, BidderId) where Status == Active
        // prevents two concurrent bids from the same bidder from both landing. If the insert
        // fails with DuplicateKey the repository re-throws as InvalidOperationException.
        try
        {
            _offerRepo.Add(offer);
        }
        catch (InvalidOperationException)
        {
            foreach (var rid in reservedIds)
                await _userStickerRepo.IncrementQuantityAndActivateAsync(rid);
            throw;
        }

        await RecalculateBestOfferAsync(auction);

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

        if (auction.AuctioneerId != callerUserId)
            throw new UnauthorizedAccessException("Only the auctioneer can accept offers on their auction.");

        if (auction.Status != AuctionStatus.Active)
            throw new InvalidOperationException("The auction is not active.");

        var offer = _offerRepo.GetById(offerId);
        if (offer == null || offer.AuctionId != auctionId)
            throw new KeyNotFoundException("The specified offer does not belong to this auction.");

        if (offer.State != AuctionOfferState.Pending)
            throw new InvalidOperationException("The offer is no longer pending and cannot be accepted.");

        var selectionPersisted = await _auctionRepo.TrySetUserSelectedBestOfferAsync(auctionId, offerId);
        if (!selectionPersisted)
            throw new InvalidOperationException("Auction is no longer active and cannot be accepted.");

        var claimed = await _auctionRepo.TryCloseAuctionAtomicallyAsync(auctionId);
        if (!claimed)
            throw new InvalidOperationException("Auction is no longer active and cannot be accepted.");

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

        int? winningOfferId = auction.UserSelectedBestOfferId ?? auction.BestCurrentOfferId;

        if (winningOfferId == null)
        {
            var cancelled = await _auctionRepo.TryCancelAuctionAtomicallyAsync(auctionId);
            if (!cancelled)
                throw new InvalidOperationException("Auction is no longer active.");

            await _userStickerRepo.IncrementQuantityAndActivateAsync(auction.UserStickerId);
        }
        else
        {
            var closed = await _auctionRepo.TryCloseAuctionAtomicallyAsync(auctionId);
            if (!closed)
                throw new InvalidOperationException("Auction is no longer active.");

            await FinalizeClosedAuctionAsync(auctionId, winningOfferId);
        }

        return MapToDto(_auctionRepo.GetById(auctionId)!);
    }

    /// <summary>
    /// Cancels a pending offer placed by the authenticated bidder on an active auction.
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

        if (offer.BidderId != callerUserId)
            throw new UnauthorizedAccessException("Only the bidder who placed this offer can cancel it.");

        var leadingOfferId = auction.UserSelectedBestOfferId ?? auction.BestCurrentOfferId;
        if (leadingOfferId.HasValue && leadingOfferId.Value == offerId)
            throw new InvalidOperationException(
                "You cannot cancel the current leading offer. It is the best offer for this auction.");

        var cancelled = await _offerRepo.TryCancelOfferAtomicallyAsync(offerId);
        if (!cancelled)
            throw new InvalidOperationException(
                "The offer is no longer pending and cannot be cancelled.");

        foreach (var sid in offer.OfferedUserStickerIds)
            await _userStickerRepo.IncrementQuantityAndActivateAsync(sid);

        var updatedOffer = _offerRepo.GetById(offerId)!;
        await RecalculateBestOfferAsync(auction);

        return MapOfferToDto(updatedOffer);
    }

    /// <summary>
    /// Appends additional stickers to an existing pending offer (accumulative modification).
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

        if (offer.BidderId != callerUserId)
            throw new UnauthorizedAccessException("Only the bidder who placed this offer can modify it.");

        var leadingOfferId = auction.UserSelectedBestOfferId ?? auction.BestCurrentOfferId;
        if (leadingOfferId.HasValue && leadingOfferId.Value == offerId)
            throw new InvalidOperationException(
                "You cannot modify the current leading offer. It is the best offer for this auction.");

        if (dto.AdditionalStickerIds == null || dto.AdditionalStickerIds.Count == 0)
            throw new InvalidOperationException("AdditionalStickerIds cannot be empty.");

        var duplicateIds = dto.AdditionalStickerIds
            .GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Any())
            throw new InvalidOperationException(
                $"Duplicate sticker IDs in AdditionalStickerIds: {string.Join(", ", duplicateIds)}");

        var existingSet = offer.OfferedUserStickerIds.ToHashSet();
        var overlapping = dto.AdditionalStickerIds.Where(id => existingSet.Contains(id)).ToList();
        if (overlapping.Any())
            throw new InvalidOperationException(
                $"The following sticker IDs are already committed to this offer: {string.Join(", ", overlapping)}");

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

        // Reserve stock atomically for the additional stickers.
        var reservedNewIds = new List<int>();
        foreach (var sticker in newStickers)
        {
            var ok = await _userStickerRepo.TryReserveOneUnitAsync(sticker.Id);
            if (!ok)
            {
                foreach (var rid in reservedNewIds)
                    await _userStickerRepo.IncrementQuantityAndActivateAsync(rid);
                throw new InvalidOperationException($"UserSticker {sticker.Id} has insufficient stock.");
            }
            reservedNewIds.Add(sticker.Id);
            await _userStickerRepo.DeactivateIfEmptyAsync(sticker.Id);
        }

        var appended = await _offerRepo.AppendOfferedStickersAsync(offerId, dto.AdditionalStickerIds);
        if (!appended)
        {
            foreach (var rid in reservedNewIds)
                await _userStickerRepo.IncrementQuantityAndActivateAsync(rid);
            throw new InvalidOperationException(
                "The offer is no longer pending and cannot be modified.");
        }

        var updatedOffer = _offerRepo.GetById(offerId)!;
        await RecalculateBestOfferAsync(auction);

        return MapOfferToDto(updatedOffer);
    }

    /// <summary>
    /// Allows the auctioneer to clear a previously pre-selected offer without closing the auction.
    /// </summary>
    public async Task<AuctionResponseDTO> ClearBestOfferAsync(int auctionId, int callerUserId)
    {
        var auction = _auctionRepo.GetById(auctionId);
        if (auction == null)
            throw new KeyNotFoundException("Auction not found.");

        if (auction.AuctioneerId != callerUserId)
            throw new UnauthorizedAccessException("Only the auctioneer can clear the selected offer.");

        if (auction.Status != AuctionStatus.Active)
            throw new InvalidOperationException("The auction is not active.");

        var cleared = await _auctionRepo.TryClearUserSelectedBestOfferAsync(auctionId);
        if (!cleared)
            throw new InvalidOperationException("Auction is no longer active and the selection could not be cleared.");

        return MapToDto(_auctionRepo.GetById(auctionId)!);
    }

    /// <summary>
    /// Allows the auctioneer to pre-select a preferred offer without closing the auction.
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
    /// </summary>
    public async Task<AuctionResponseDTO> CloseAuction(int auctionId, int? winningOfferId, int callerUserId)
    {
        var auction = _auctionRepo.GetById(auctionId);
        if (auction == null)
            throw new KeyNotFoundException("Auction not found.");

        if (auction.AuctioneerId != callerUserId)
            throw new InvalidOperationException("Only the auctioneer can close their own auction.");

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
            await _userStickerRepo.IncrementQuantityAndActivateAsync(auction.UserStickerId);

            var activeOffers = allOffers.Where(o => o.Status == AuctionOfferStatus.Active).ToList();
            foreach (var offer in activeOffers)
            {
                offer.State = AuctionOfferState.Lost;
                _offerRepo.Update(offer);

                foreach (var sid in offer.OfferedUserStickerIds)
                    await _userStickerRepo.IncrementQuantityAndActivateAsync(sid);
            }
        }
        else
        {
            var winningOffer = allOffers.FirstOrDefault(o => o.Id == winningOfferId)
                ?? throw new InvalidOperationException("The specified winning offer was not found for this auction.");

            var auctionedSticker = _userStickerRepo.GetByIdIncludingInactive(auction.UserStickerId);
            if (auctionedSticker != null)
            {
                var winnerExisting = await _userStickerRepo.GetByUserIdAndCatalogIdIncludingInactiveAsync(
                    winningOffer.BidderId, auctionedSticker.Sticker.Id);
                if (winnerExisting != null)
                {
                    await _userStickerRepo.IncrementQuantityAndActivateAsync(winnerExisting.Id);
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

            var winningStickers = _userStickerRepo.GetMultipleByIdIncludingInactive(winningOffer.OfferedUserStickerIds);
            foreach (var winningSticker in winningStickers)
            {
                var auctioneerExisting = await _userStickerRepo.GetByUserIdAndCatalogIdIncludingInactiveAsync(
                    auction.AuctioneerId, winningSticker.Sticker.Id);
                if (auctioneerExisting != null)
                {
                    await _userStickerRepo.IncrementQuantityAndActivateAsync(auctioneerExisting.Id);
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

            if (auctionedSticker != null)
                await _missingStickerRepo.DeleteAsync(winningOffer.BidderId, auctionedSticker.Sticker.Id);

            foreach (var winningSticker in winningStickers)
                await _missingStickerRepo.DeleteAsync(auction.AuctioneerId, winningSticker.Sticker.Id);

            winningOffer.State = AuctionOfferState.Won;
            _offerRepo.Update(winningOffer);

            var losingOffers = allOffers
                .Where(o => o.Id != winningOfferId && o.Status == AuctionOfferStatus.Active)
                .ToList();
            foreach (var losingOffer in losingOffers)
            {
                losingOffer.State = AuctionOfferState.Lost;
                _offerRepo.Update(losingOffer);

                foreach (var sid in losingOffer.OfferedUserStickerIds)
                    await _userStickerRepo.IncrementQuantityAndActivateAsync(sid);
            }
        }

        return MapToDto(_auctionRepo.GetById(auctionId)!);
    }

    /// <summary>
    /// Returns a paged list of all bids placed by the authenticated user.
    /// </summary>
    public async Task<List<MyBidResponseDTO>> GetMyBidsAsync(int bidderId, int page, int pageSize)
    {
        var offers = await _offerRepo.GetByBidderIdAsync(bidderId, page, pageSize);
        if (offers.Count == 0)
            return new List<MyBidResponseDTO>();

        var auctionIds = offers.Select(o => o.AuctionId).Distinct().ToList();
        var auctionMap = auctionIds
            .Select(id => _auctionRepo.GetById(id))
            .Where(a => a != null)
            .ToDictionary(a => a!.Id);

        var userStickerIds = auctionMap.Values.Select(a => a!.UserStickerId).Distinct().ToList();
        var userStickerMap = _userStickerRepo.GetMultipleByIdIncludingInactive(userStickerIds)
            .ToDictionary(us => us.Id);

        return offers.Select(offer =>
        {
            auctionMap.TryGetValue(offer.AuctionId, out var auction);
            UserSticker? us = null;
            if (auction != null)
                userStickerMap.TryGetValue(auction.UserStickerId, out us);

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
    /// Core finalization logic shared by AcceptOfferAsync and CloseAuctionAutomatically.
    /// Guarded by FinalizationCompleted to prevent duplicate execution (TAREA 9).
    /// </summary>
    private async Task FinalizeClosedAuctionAsync(int auctionId, int? winningOfferId)
    {
        // RC-09 fix: claim the finalization slot atomically FIRST.
        // TryMarkFinalizationCompletedAsync uses a filter { Id==auctionId, FinalizationCompleted==false }
        // with $set { FinalizationCompleted: true }, so only one concurrent caller can win the race.
        // Reading FinalizationCompleted from an in-memory object and checking it before doing work
        // (the old pattern) was stale: two workers could both read false simultaneously and both proceed.
        var claimed = await _auctionRepo.TryMarkFinalizationCompletedAsync(auctionId);
        if (!claimed)
            return;

        var auction = _auctionRepo.GetById(auctionId)
            ?? throw new KeyNotFoundException($"Auction {auctionId} not found during finalization.");

        var allOffers = await _offerRepo.GetAllByAuctionIdAsync(auctionId);

        if (winningOfferId == null || !allOffers.Any(o => o.State == AuctionOfferState.Pending))
        {
            await _userStickerRepo.IncrementQuantityAndActivateAsync(auction.UserStickerId);

            var pendingOffers = allOffers.Where(o => o.State == AuctionOfferState.Pending).ToList();
            foreach (var offer in pendingOffers)
            {
                offer.State = AuctionOfferState.Lost;
                _offerRepo.Update(offer);

                foreach (var sid in offer.OfferedUserStickerIds)
                    await _userStickerRepo.IncrementQuantityAndActivateAsync(sid);
            }

            return;
        }

        var winningOffer = allOffers.FirstOrDefault(o => o.Id == winningOfferId)
            ?? throw new InvalidOperationException($"Winning offer {winningOfferId} not found for auction {auctionId}.");

        var auctionedStickerForTransfer = _userStickerRepo.GetByIdIncludingInactive(auction.UserStickerId);
        if (auctionedStickerForTransfer != null)
        {
            var winnerExisting = await _userStickerRepo.GetByUserIdAndCatalogIdIncludingInactiveAsync(
                winningOffer.BidderId, auctionedStickerForTransfer.Sticker.Id);
            if (winnerExisting != null)
            {
                await _userStickerRepo.IncrementQuantityAndActivateAsync(winnerExisting.Id);
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

        var winningStickers = _userStickerRepo.GetMultipleByIdIncludingInactive(winningOffer.OfferedUserStickerIds);
        foreach (var winningSticker in winningStickers)
        {
            var auctioneerExisting = await _userStickerRepo.GetByUserIdAndCatalogIdIncludingInactiveAsync(
                auction.AuctioneerId, winningSticker.Sticker.Id);
            if (auctioneerExisting != null)
            {
                await _userStickerRepo.IncrementQuantityAndActivateAsync(auctioneerExisting.Id);
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

        if (auctionedStickerForTransfer != null)
            await _missingStickerRepo.DeleteAsync(winningOffer.BidderId, auctionedStickerForTransfer.Sticker.Id);

        foreach (var winningSticker in winningStickers)
            await _missingStickerRepo.DeleteAsync(auction.AuctioneerId, winningSticker.Sticker.Id);

        winningOffer.State = AuctionOfferState.Won;
        _offerRepo.Update(winningOffer);

        var losingOffers = allOffers
            .Where(o => o.Id != winningOfferId && o.State == AuctionOfferState.Pending)
            .ToList();
        foreach (var losingOffer in losingOffers)
        {
            losingOffer.State = AuctionOfferState.Lost;
            _offerRepo.Update(losingOffer);

            foreach (var sid in losingOffer.OfferedUserStickerIds)
                await _userStickerRepo.IncrementQuantityAndActivateAsync(sid);
        }
    }

    /// <summary>
    /// Recalculates BestCurrentOfferId using a partial $set to avoid overwriting
    /// UserSelectedBestOfferId (RC-08 fix).
    /// </summary>
    private async Task RecalculateBestOfferAsync(Auction auction)
    {
        var auctioneerMissingIds = (await _missingStickerRepo.GetStickerIdsByUserIdAsync(auction.AuctioneerId))
                                   .ToHashSet();

        var activeOffers = await _offerRepo.GetByAuctionIdAsync(auction.Id);
        var rankableOffers = activeOffers.Where(o => o.Status == AuctionOfferStatus.Active).ToList();

        if (rankableOffers.Count == 0)
        {
            await _auctionRepo.SetBestCurrentOfferIdAsync(auction.Id, null);
            return;
        }

        var allOfferedIds = rankableOffers
            .SelectMany(o => o.OfferedUserStickerIds)
            .Distinct()
            .ToList();

        var userStickerMap = _userStickerRepo
            .GetMultipleByIdIncludingInactive(allOfferedIds)
            .ToDictionary(us => us.Id);

        var bestOffer = rankableOffers
            .OrderByDescending(o =>
            {
                var offeredCatalogIds = o.OfferedUserStickerIds
                    .Where(usId => userStickerMap.ContainsKey(usId))
                    .Select(usId => userStickerMap[usId].Sticker.Id)
                    .ToHashSet();
                return offeredCatalogIds.Count(sid => auctioneerMissingIds.Contains(sid));
            })
            .ThenByDescending(o => o.OfferedUserStickerIds.Count)
            .ThenBy(o => o.CreatedAt)
            .First();

        await _auctionRepo.SetBestCurrentOfferIdAsync(auction.Id, bestOffer.Id);
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

    private static AuctionOfferResponseDTO MapOfferToDto(
        AuctionOffer offer,
        string bidderUsername = "",
        List<StickerPreviewDTO>? offeredStickerPreviews = null) => new()
    {
        Id = offer.Id,
        AuctionId = offer.AuctionId,
        BidderId = offer.BidderId,
        OfferedUserStickerIds = offer.OfferedUserStickerIds,
        CreatedAt = offer.CreatedAt,
        State = offer.State.ToString(),
        BidderUsername = bidderUsername,
        OfferedStickerPreviews = offeredStickerPreviews ?? new()
    };
}
