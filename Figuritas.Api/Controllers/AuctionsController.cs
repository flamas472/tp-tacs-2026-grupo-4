using Microsoft.AspNetCore.Mvc;
using Figuritas.Shared.Model;
using System.Security.Claims;

namespace Figuritas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuctionsController : ControllerBase
{
    private readonly AuctionRepository _auctionRepo;
    private readonly AuctionOfferRepository _offerRepo;

    public AuctionsController(AuctionRepository auctionRepo, AuctionOfferRepository offerRepo)
    {
        _auctionRepo = auctionRepo;
        _offerRepo = offerRepo;
    }

    [HttpGet]
    public ActionResult<List<Auction>> GetAuctions()
    {
        var auctions = _auctionRepo.GetAll();
        return Ok(auctions);
    }

    [HttpPost]
    public ActionResult<Auction> PostAuction(Auction auction)
    {
        _auctionRepo.Add(auction);
        return CreatedAtAction(nameof(GetAuctions), new { id = auction.Id }, auction);
    }

    [HttpPost("{auctionId}/offers")]
    public ActionResult<AuctionOffer> PostOffer(int auctionId, AuctionOffer offer)
    {
        var auction = _auctionRepo.GetById(auctionId);
        if (auction == null) return NotFound("Auction not found");
        _offerRepo.Add(offer);
        return CreatedAtAction(nameof(GetAuctions), new { id = offer.Id }, offer);
    }
}