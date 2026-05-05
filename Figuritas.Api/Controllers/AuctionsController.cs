using Microsoft.AspNetCore.Mvc;
using Figuritas.Shared.Model;
using Figuritas.Shared.DTO.request;
using System.Security.Claims;
using Figuritas.Api.Services;

namespace Figuritas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuctionsController : ControllerBase
{
    private readonly AuctionService _auctionService;
  
    private readonly AuthService _authService;

    public AuctionsController(AuctionService auctionService, AuthService authService)
    {
        _auctionService = auctionService;
        _authService = authService;
    }

    [HttpGet]
    public ActionResult<List<Auction>> GetAuctions()
    {
        try
        {
            var auctions = _auctionService.GetAuctions();
            return Ok(auctions);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("{id}")]
    public ActionResult<Auction> GetAuction(int id)
    {
        try
        {
            var auction = _auctionService.GetAuction(id);
            if (auction == null) return NotFound("Auction not found");
            return Ok(auction);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost]
    public ActionResult<Auction> PostAuction(PostAuctionDTO dto)
    {
        try
        {
            var auctioneerId = _authService.GetUserIdFromToken(User);
            var auction = _auctionService.CreateAuction(auctioneerId, dto);
            return CreatedAtAction(nameof(GetAuctions), new { id = auction.Id }, auction);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }


    [HttpPost("{auctionId}/offers")]
    public ActionResult<AuctionOffer> PostAuctionOffer(int auctionId, PostAuctionOfferDTO dto)
    {
        try
        {
            var bidderId = _authService.GetUserIdFromToken(User);
            var offer = _auctionService.CreateOffer(bidderId, auctionId, dto);
            return CreatedAtAction(nameof(GetAuctions), new { id = offer.Id }, offer);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }
}