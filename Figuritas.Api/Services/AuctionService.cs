using Figuritas.Api.Repositories;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.Model;

namespace Figuritas.Api.Services;

public class AuctionService
{
    private readonly IUserStickerRepository _userStickerRepo;
    private readonly IUserRepository _userRepository;
    private readonly IStickerRepository _stickerRepo;
    private readonly IAuctionRepository _auctionRepo;
    private readonly IAuctionOfferRepository _offerRepo;

    public AuctionService(IUserStickerRepository userStickerRepo, IUserRepository userRepo, IStickerRepository stickerRepo, IAuctionRepository auctionRepo, IAuctionOfferRepository offerRepo)
    {
        _userStickerRepo = userStickerRepo;
        _userRepository = userRepo;
        _stickerRepo = stickerRepo;
        _auctionRepo = auctionRepo;
        _offerRepo = offerRepo;
    }

    public List<Auction> GetAuctions()
    {
        return _auctionRepo.GetAll();
    }

    public Auction? GetAuction(int id)
    {
        return _auctionRepo.GetById(id);
    }

    public Auction CreateAuction(int userId, PostAuctionDTO dto)
    {
        var userSticker = _userStickerRepo.GetById(dto.AuctionedStickerId);
        if (userSticker == null) throw new ArgumentException("Sticker not found in user inventory.");
        if (userSticker.UserId != userId) throw new ArgumentException("Sticker does not belong to the user.");

        if (dto.StartDate >= dto.EndDate) throw new ArgumentException("Start date must be before end date.");

        var auction = new Auction
        {
            Auctioneer = _userRepository.GetById(userId)!,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            AuctionedSticker = userSticker,
            MinimumOffer = dto.MinimumOfferStickerIds
                .Select(id => _stickerRepo.GetById(id))
                .Where(s => s != null)
                .Select(s => s!)
                .ToList()
        };

        _auctionRepo.Add(auction);

        return auction;
    }

    public AuctionOffer CreateOffer(int bidderId, int auctionId, PostAuctionOfferDTO dto)
    {
        if (auctionId <= 0)
            throw new ArgumentException("Invalid auction ID.");

        var auction = _auctionRepo.GetById(auctionId);
        if (auction == null)
            throw new KeyNotFoundException("Auction not found.");

        if (DateTime.UtcNow > auction.EndDate)
            throw new InvalidOperationException("The auction has already ended.");

        var offeredUserStickers = _userStickerRepo.GetMultipleById(dto.UserStickerIds);

        var offeredStickerNumbers = offeredUserStickers.Select(us => us.Sticker.Number).ToHashSet();
        var minimumNotMet = auction.MinimumOffer
            .Where(required => !offeredStickerNumbers.Contains(required.Number))
            .ToList();

        if (minimumNotMet.Any())
            throw new InvalidOperationException(
                $"Offer does not meet minimum requirements. Missing sticker(s): {string.Join(", ", minimumNotMet.Select(s => s.Number))}");

        var bidder = _userRepository.GetById(bidderId);
        if (bidder == null)
            throw new ArgumentException("Bidder not found.");

        var aucOffer = new AuctionOffer
        {
            Bidder = bidder,
            Auction = auction,
            Offer = offeredUserStickers
        };

        _offerRepo.Add(aucOffer);
        return aucOffer;
    }
}
