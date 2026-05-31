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

    public AuctionService(
        IUserStickerRepository userStickerRepo,
        IAuctionRepository auctionRepo,
        IAuctionOfferRepository offerRepo)
    {
        _userStickerRepo = userStickerRepo;
        _auctionRepo = auctionRepo;
        _offerRepo = offerRepo;
    }

    public List<AuctionResponseDTO> GetMyAuctions(GetMyAuctionsDTO dto, int callerUserId)
    {
        return _auctionRepo.GetByAuctioneerId(callerUserId, AuctionStatus.Active.ToString(), dto.Page, dto.PageSize)
                           .Select(MapToDto)
                           .ToList();
    }

    public List<AuctionResponseDTO> GetAuctions()
    {
        return _auctionRepo.GetAll().Select(MapToDto).ToList();
    }

    public AuctionResponseDTO? GetAuction(int id)
    {
        var auction = _auctionRepo.GetById(id);
        return auction == null ? null : MapToDto(auction);
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

        return MapToDto(auction);
    }

    public AuctionOfferResponseDTO CreateOffer(int bidderId, int auctionId, PostAuctionOfferRequestDTO dto)
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

        // Validate minimum offer requirements: compare catalog sticker IDs
        var offeredCatalogStickerIds = offeredUserStickers.Select(us => us.Sticker.Id).ToHashSet();
        var missingRequired = auction.MinimumOfferStickerIds
            .Where(requiredId => !offeredCatalogStickerIds.Contains(requiredId))
            .ToList();

        if (missingRequired.Any())
            throw new InvalidOperationException(
                $"Offer does not meet minimum requirements. Missing catalog sticker ID(s): {string.Join(", ", missingRequired)}");

        var offer = new AuctionOffer
        {
            AuctionId = auctionId,
            BidderId = bidderId,
            OfferedUserStickerIds = dto.OfferedUserStickerIds
        };

        _offerRepo.Add(offer);

        auction.BestCurrentOfferId = offer.Id;
        _auctionRepo.Update(auction);

        return MapOfferToDto(offer);
    }

    private static AuctionResponseDTO MapToDto(Auction auction) => new()
    {
        Id = auction.Id,
        AuctioneerId = auction.AuctioneerId,
        UserStickerId = auction.UserStickerId,
        MinimumOfferStickerIds = auction.MinimumOfferStickerIds,
        Status = auction.Status.ToString(),
        CreatedAt = auction.CreatedAt,
        EndsAt = auction.EndsAt,
        BestCurrentOfferId = auction.BestCurrentOfferId
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
