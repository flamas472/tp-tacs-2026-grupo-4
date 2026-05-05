using Figuritas.Shared.DTO;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.Model;
using Figuritas.Shared.Utils;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Figuritas.Api.Services;

public class AuctionService
{
    private readonly UserStickerRepository _userStickerRepo;
    private readonly UserRepository _userRepository;
    private readonly StickerRepository _stickerRepo;
    private readonly AuctionRepository _auctionRepo;
    private readonly AuctionOfferRepository _offerRepo;

    public AuctionService(UserStickerRepository userStickerRepo, UserRepository userRepo, StickerRepository stickerRepo, AuctionRepository auctionRepo, AuctionOfferRepository offerRepo)
    {
        _userStickerRepo = userStickerRepo;
        _userRepository = userRepo;
        _stickerRepo = stickerRepo;
        _auctionRepo = auctionRepo;
        _offerRepo = offerRepo;
    }

    public List<Auction> GetAuctions() {
        return _auctionRepo.GetAll();
    }

    public Auction? GetAuction(int id) {
        return _auctionRepo.GetById(id);
    }

    public Auction CreateAuction(int userId, PostAuctionDTO dto) {
        // 1. Validaciones de existencia
        var userSticker = _userStickerRepo.GetById(dto.AuctionedStickerId);
        if (userSticker == null) throw new ArgumentException("La figurita no está en tu inventario.");
        if(userSticker.UserId != userId) throw new ArgumentException("La figurita no pertenece al usuario.");

        if (dto.StartDate >= dto.EndDate) throw new ArgumentException("La fecha de inicio debe ser anterior a la de fin.");

        // 2. Mapeo de DTO a Entidad
        var auction = new Auction {
            Auctioneer = _userRepository.GetById(userId),
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            AuctionedSticker = userSticker,
            MinimumOffer = dto.MinimumOfferStickerIds.Select(id => _stickerRepo.GetById(id)).ToList()
        };

        _auctionRepo.Add(auction);
  
        return auction;
    }

    public AuctionOffer CreateOffer(int bidderId, int auctionId, PostAuctionOfferDTO dto) {
        var auction = _auctionRepo.GetById(auctionId);
        if (auction == null) throw new KeyNotFoundException("Subasta no encontrada.");
        if (DateTime.Now > auction.EndDate) throw new InvalidOperationException("La subasta ya finalizó.");
        if (auctionId < 1) throw new ArgumentException("Id de subasta inválida.");

        var aucOffer = new AuctionOffer {
            Bidder = _userRepository.GetById(bidderId),
            Auction = _auctionRepo.GetById(auctionId),
            Offer = _userStickerRepo.GetMultipleById(dto.UserStickerIds) // Las figuritas que el postulante ofrece
        };

        _offerRepo.Add(aucOffer);
        return aucOffer;
    }
}
