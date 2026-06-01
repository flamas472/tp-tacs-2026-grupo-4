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

    public List<AuctionResponseDTO> GetMyAuctions(GetMyAuctionsDTO dto, int callerUserId)
    {
        var auctions = _auctionRepo.GetByAuctioneerId(callerUserId, AuctionStatus.Active.ToString(), dto.Page, dto.PageSize);
        var stickerIds = auctions.Select(a => a.UserStickerId).ToList();
        var stickers = _userStickerRepo.GetMultipleByIdIncludingInactive(stickerIds).ToDictionary(us => us.Id);
        var user = _userRepo.GetById(callerUserId);
        return auctions.Select(a =>
        {
            stickers.TryGetValue(a.UserStickerId, out var us);
            return MapToDto(a, us, user);
        }).ToList();
    }

    public List<AuctionResponseDTO> GetAuctions(int page = 1, int pageSize = 20)
    {
        var auctions = _auctionRepo.GetAll(page, pageSize);
        var stickerIds = auctions.Select(a => a.UserStickerId).Distinct().ToList();
        var userIds = auctions.Select(a => a.AuctioneerId).Distinct().ToList();
        var stickers = _userStickerRepo.GetMultipleByIdIncludingInactive(stickerIds).ToDictionary(us => us.Id);
        var users = _userRepo.GetByIds(userIds).ToDictionary(u => u.Id);
        return auctions.Select(a =>
        {
            stickers.TryGetValue(a.UserStickerId, out var us);
            users.TryGetValue(a.AuctioneerId, out var user);
            return MapToDto(a, us, user);
        }).ToList();
    }

    public AuctionResponseDTO? GetAuction(int id)
    {
        var auction = _auctionRepo.GetById(id);
        if (auction == null) return null;
        var us = _userStickerRepo.GetByIdIncludingInactive(auction.UserStickerId);
        var user = _userRepo.GetById(auction.AuctioneerId);
        return MapToDto(auction, us, user);
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
            us.CanBeAuctioned = false;
            us.CanBeDirectlyExchanged = false;
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

            if (offeredUs.Quantity <= 0)
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
            Status = AuctionOfferStatus.Active
        };

        _offerRepo.Add(offer);

        auction.BestCurrentOfferId = offer.Id;
        _auctionRepo.Update(auction);

        // Auto-add bidder to watchlist if not already watching
        await _watchlistService.EnsureWatchingAsync(bidderId, auctionId);

        return MapOfferToDto(offer);
    }

    public async Task<AuctionResponseDTO> CloseAuction(int auctionId, int? winningOfferId, int callerUserId)
    {
        var auction = _auctionRepo.GetById(auctionId);
        if (auction == null)
            throw new KeyNotFoundException("Auction not found.");

        if (auction.Status != AuctionStatus.Active)
            throw new InvalidOperationException("The auction is not active.");

        if (auction.AuctioneerId != callerUserId)
            throw new InvalidOperationException("Only the auctioneer can close their own auction.");

        var allOffers = await _offerRepo.GetByAuctionIdAsync(auctionId);

        if (winningOfferId == null || !allOffers.Any())
        {
            // No winner: return reserved stock of auctioned sticker to seller
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
            // There is a winner
            var winningOffer = allOffers.FirstOrDefault(o => o.Id == winningOfferId)
                ?? throw new InvalidOperationException("The specified winning offer was not found for this auction.");

            // Transfer auctioned sticker (reserved/inactive) to winner's inventory
            var auctionedSticker = _userStickerRepo.GetByIdIncludingInactive(auction.UserStickerId);
            if (auctionedSticker != null)
            {
                var winnerExisting = _userStickerRepo.GetByUserId(winningOffer.BidderId)
                    .FirstOrDefault(s => s.Sticker.Id == auctionedSticker.Sticker.Id);
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

            // Transfer winning offer stickers (reserved/inactive) to auctioneer
            var winningStickers = _userStickerRepo.GetMultipleByIdIncludingInactive(winningOffer.OfferedUserStickerIds);
            foreach (var winningSticker in winningStickers)
            {
                var auctioneerExisting = _userStickerRepo.GetByUserId(auction.AuctioneerId)
                    .FirstOrDefault(s => s.Sticker.Id == winningSticker.Sticker.Id);
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

            // Automation: clean up MissingStickers for winner and auctioneer
            if (auctionedSticker != null)
            {
                await _missingStickerRepo.DeleteAsync(winningOffer.BidderId, auctionedSticker.Sticker.Id);
            }
            foreach (var winningSticker in winningStickers)
            {
                await _missingStickerRepo.DeleteAsync(auction.AuctioneerId, winningSticker.Sticker.Id);
            }

            // Return losing bidders' reserved stickers.
            // Skip Superseded offers: their stock was already released when they were superseded.
            var losingOffers = allOffers
                .Where(o => o.Id != winningOfferId && o.Status == AuctionOfferStatus.Active)
                .ToList();
            foreach (var losingOffer in losingOffers)
            {
                var loserStickers = _userStickerRepo.GetMultipleByIdIncludingInactive(losingOffer.OfferedUserStickerIds);
                foreach (var loserSticker in loserStickers)
                {
                    loserSticker.Quantity++;
                    loserSticker.Active = true;
                    _userStickerRepo.Update(loserSticker);
                }
            }
        }

        auction.Status = AuctionStatus.Closed;
        _auctionRepo.Update(auction);

        return MapToDto(auction);
    }

    /// <summary>
    /// Closes an expired auction automatically (called by the background worker, not by the auctioneer).
    /// Applies exactly the same business rules as <see cref="CloseAuction"/> but:
    /// - Skips the caller-ownership check (the worker has no user identity).
    /// - Automatically selects the winner based on <see cref="Auction.BestCurrentOfferId"/>.
    /// Virtual to allow unit-test mocking without requiring an interface extraction.
    /// </summary>
    public virtual async Task<AuctionResponseDTO> CloseAuctionAutomatically(int auctionId)
    {
        var auction = _auctionRepo.GetById(auctionId);
        if (auction == null)
            throw new KeyNotFoundException("Auction not found.");

        if (auction.Status != AuctionStatus.Active)
            throw new InvalidOperationException("The auction is not active.");

        // Delegate to the shared closure logic using the recorded best offer as winner.
        // Passing callerUserId = auction.AuctioneerId satisfies the ownership guard in CloseAuction.
        return await CloseAuction(auctionId, auction.BestCurrentOfferId, auction.AuctioneerId);
    }

    private static AuctionResponseDTO MapToDto(Auction auction, UserSticker? us = null, User? user = null) => new()
    {
        Id = auction.Id,
        AuctioneerId = auction.AuctioneerId,
        UserStickerId = auction.UserStickerId,
        MinimumOfferStickerIds = auction.MinimumOfferStickerIds,
        Status = auction.Status.ToString(),
        CreatedAt = auction.CreatedAt,
        EndsAt = auction.EndsAt,
        BestCurrentOfferId = auction.BestCurrentOfferId,
        StickerNumber = us?.Sticker.Number ?? 0,
        StickerDescription = us?.Sticker.Description ?? string.Empty,
        StickerNationalTeam = us?.Sticker.NationalTeam ?? string.Empty,
        StickerTeam = us?.Sticker.Team ?? string.Empty,
        StickerImageUrl = us?.Sticker.ImageUrl ?? string.Empty,
        AuctioneerUsername = user?.Username ?? string.Empty
    };

    private static AuctionOfferResponseDTO MapOfferToDto(AuctionOffer offer) => new()
    {
        Id = offer.Id,
        AuctionId = offer.AuctionId,
        BidderId = offer.BidderId,
        OfferedUserStickerIds = offer.OfferedUserStickerIds,
        CreatedAt = offer.CreatedAt
    };
}
