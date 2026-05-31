using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Api.Services;

namespace Figuritas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
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
    [AllowAnonymous]
    public ActionResult<List<AuctionResponseDTO>> GetAuctions()
    {
        var auctions = _auctionService.GetAuctions();
        return Ok(auctions);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public ActionResult<AuctionResponseDTO> GetAuction(int id)
    {
        var auction = _auctionService.GetAuction(id);
        if (auction == null)
            return NotFound("Auction not found.");
        return Ok(auction);
    }

    [HttpPost]
    public ActionResult<AuctionResponseDTO> PostAuction([FromBody] PostAuctionRequestDTO dto)
    {
        try
        {
            var callerUserId = _authService.GetUserIdFromToken(User);
            var auction = _auctionService.CreateAuction(callerUserId, dto);
            return CreatedAtAction(nameof(GetAuction), new { id = auction.Id }, auction);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{auctionId}/offers")]
    public ActionResult<AuctionOfferResponseDTO> PostAuctionOffer(int auctionId, [FromBody] PostAuctionOfferRequestDTO dto)
    {
        try
        {
            var bidderId = _authService.GetUserIdFromToken(User);
            var offer = _auctionService.CreateOffer(bidderId, auctionId, dto);
            return CreatedAtAction(nameof(GetAuction), new { id = auctionId }, offer);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
