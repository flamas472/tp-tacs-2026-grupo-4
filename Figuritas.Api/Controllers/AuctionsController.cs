using Microsoft.AspNetCore.Mvc;
using Figuritas.Shared.Model;
using Figuritas.Shared.DTO.request;
using Figuritas.Api.Services;
using Microsoft.AspNetCore.Authorization;

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

    [Authorize]
    [HttpPost]
    public ActionResult<Auction> PostAuction(PostAuctionDTO dto)
    {
        try
        {
            var auctioneerId = _authService.GetUserIdFromToken(User);
            var auction = _auctionService.CreateAuction(auctioneerId, dto);
            return CreatedAtAction(nameof(GetAuction), new { id = auction.Id }, auction);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [Authorize]
    [HttpPost("{auctionId}/offers")]
    public ActionResult<AuctionOffer> PostAuctionOffer(int auctionId, PostAuctionOfferDTO dto)
    {
        try
        {
            var bidderId = _authService.GetUserIdFromToken(User);
            var offer = _auctionService.CreateOffer(bidderId, auctionId, dto);
            return CreatedAtAction(nameof(GetAuction), new { id = auctionId }, offer);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
