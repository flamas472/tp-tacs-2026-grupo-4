using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Api.Services;
using Figuritas.Shared.Model;

namespace Figuritas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuctionsController : ControllerBase
{
    private readonly AuctionService _auctionService;
    private readonly AuctionWatchlistService _auctionWatchlistService;
    private readonly AuthService _authService;

    public AuctionsController(
        AuctionService auctionService,
        AuctionWatchlistService auctionWatchlistService,
        AuthService authService)
    {
        _auctionService = auctionService;
        _auctionWatchlistService = auctionWatchlistService;
        _authService = authService;
    }

    [HttpGet]
    public ActionResult<List<AuctionResponseDTO>> GetAuctions(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20)
    {
        var callerUserId = _authService.GetUserIdFromToken(User);
        var auctions = _auctionService.GetAuctions(page, pageSize, excludeAuctioneerId: callerUserId);
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

    [HttpPost("{id}/close")]
    public async Task<ActionResult<AuctionResponseDTO>> CloseAuction(int id, [FromBody] CloseAuctionRequestDTO dto)
    {
        try
        {
            var callerUserId = _authService.GetUserIdFromToken(User);
            var auction = await _auctionService.CloseAuction(id, dto.WinningOfferId, callerUserId);
            return Ok(auction);
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

    [HttpGet("{auctionId}/offers")]
    [AllowAnonymous]
    public async Task<ActionResult<List<AuctionOfferResponseDTO>>> GetAuctionOffers(int auctionId)
    {
        var offers = await _auctionService.GetOffersForAuctionAsync(auctionId);
        return Ok(offers);
    }

    [HttpPost("{auctionId}/offers")]
    public async Task<ActionResult<AuctionOfferResponseDTO>> PostAuctionOffer(int auctionId, [FromBody] PostAuctionOfferRequestDTO dto)
    {
        try
        {
            var bidderId = _authService.GetUserIdFromToken(User);
            var offer = await _auctionService.CreateOfferAsync(bidderId, auctionId, dto);
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

    [HttpPost("{auctionId}/offers/{offerId}/accept")]
    public async Task<ActionResult<AuctionResponseDTO>> AcceptOffer(int auctionId, int offerId)
    {
        try
        {
            var callerUserId = _authService.GetUserIdFromToken(User);
            var auction = await _auctionService.AcceptOfferAsync(auctionId, offerId, callerUserId);
            return Ok(auction);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/watch")]
    public async Task<ActionResult<AuctionWatchlistResponseDTO>> WatchAuction(int id)
    {
        try
        {
            var userId = _authService.GetUserIdFromToken(User);
            var entry = await _auctionWatchlistService.AddWatchAsync(userId, id);
            return Ok(entry);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpDelete("{id}/watch")]
    public async Task<ActionResult> UnwatchAuction(int id)
    {
        try
        {
            var userId = _authService.GetUserIdFromToken(User);
            await _auctionWatchlistService.RemoveWatchAsync(userId, id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("watchlist")]
    public async Task<ActionResult<List<AuctionWatchlistResponseDTO>>> GetMyWatchlist()
    {
        var userId = _authService.GetUserIdFromToken(User);
        var watchlist = await _auctionWatchlistService.GetMyWatchlistAsync(userId);
        return Ok(watchlist);
    }
}
