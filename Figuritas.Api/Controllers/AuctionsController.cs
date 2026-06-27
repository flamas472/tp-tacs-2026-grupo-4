using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Api.Services;
using Figuritas.Shared.Model.Subastas;

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
    public async Task<ActionResult<List<AuctionResponseDTO>>> GetAuctions(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        [FromQuery] string? status = null)
    {
        var callerUserId = _authService.GetUserIdFromToken(User);
        var auctions = await _auctionService.GetAuctions(page, pageSize, excludeAuctioneerId: callerUserId, status: status);
        return Ok(auctions);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<AuctionResponseDTO>> GetAuction(int id)
    {
        var auction = await _auctionService.GetAuction(id);
        if (auction == null)
            return NotFound("Auction not found.");
        return Ok(auction);
    }

    [HttpPost]
    public async Task<ActionResult<AuctionResponseDTO>> PostAuction([FromBody] PostAuctionRequestDTO dto)
    {
        try
        {
            var callerUserId = _authService.GetUserIdFromToken(User);
            var auction = await _auctionService.CreateAuction(callerUserId, dto);
            return CreatedAtAction(nameof(GetAuction), new { id = auction.Id }, auction);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// [LEGACY] POST /api/auctions/{id}/close
    /// Preserved for backward compatibility. New consumers should use:
    ///   - PATCH /api/auctions/{auctionId}/selected-offer  — to pre-select a winner.
    ///   - POST /api/auctions/{auctionId}/offers/{offerId}/accept — to accept and close with a specific offer.
    ///
    /// When WinningOfferId is null: cancels the auction and restores all reserved stock
    ///   via <see cref="AuctionService.TryCancelAuctionAtomicallyAsync"/>.
    /// When WinningOfferId is provided: closes the auction and delegates to
    ///   <see cref="AuctionService.AcceptOfferAsync"/> for unified finalization logic.
    /// </summary>
    [HttpPost("{id}/close")]
    public async Task<ActionResult<AuctionResponseDTO>> CloseAuction(int id, [FromBody] CloseAuctionRequestDTO dto)
    {
        try
        {
            var callerUserId = _authService.GetUserIdFromToken(User);

            if (dto.WinningOfferId.HasValue)
            {
                // Delegate to AcceptOfferAsync — unified finalization path.
                var auction = await _auctionService.AcceptOfferAsync(id, dto.WinningOfferId.Value, callerUserId);
                return Ok(auction);
            }
            else
            {
                // Cancellation path: no winner, restore stock via the legacy CloseAuction method.
                var auction = await _auctionService.CloseAuction(id, winningOfferId: null, callerUserId);
                return Ok(auction);
            }
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

    [HttpDelete("{auctionId}/offers/{offerId}")]
    public async Task<ActionResult<AuctionOfferResponseDTO>> CancelOffer(int auctionId, int offerId)
    {
        try
        {
            var callerUserId = _authService.GetUserIdFromToken(User);
            var offer = await _auctionService.CancelOfferAsync(auctionId, offerId, callerUserId);
            return Ok(offer);
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

    [HttpPatch("{auctionId}/offers/{offerId}")]
    public async Task<ActionResult<AuctionOfferResponseDTO>> UpdateOffer(
        int auctionId, int offerId, [FromBody] UpdateAuctionOfferRequestDTO dto)
    {
        try
        {
            var callerUserId = _authService.GetUserIdFromToken(User);
            var offer = await _auctionService.UpdateOfferAsync(auctionId, offerId, dto, callerUserId);
            return Ok(offer);
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

    [HttpDelete("{auctionId}/selected-offer")]
    public async Task<ActionResult<AuctionResponseDTO>> ClearSelectedOffer(int auctionId)
    {
        try
        {
            var callerUserId = _authService.GetUserIdFromToken(User);
            var auction = await _auctionService.ClearBestOfferAsync(auctionId, callerUserId);
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

    /// <summary>
    /// PATCH /api/auctions/{auctionId}/selected-offer
    /// Allows the auctioneer to pre-select a preferred offer without closing the auction.
    /// The selected offer will be used as the winner when the auction closes.
    /// Can be replaced by a different valid Pending offer at any time while the auction is Active.
    /// </summary>
    [HttpPatch("{auctionId}/selected-offer")]
    public async Task<ActionResult<AuctionResponseDTO>> SelectBestOffer(
        int auctionId, [FromBody] SelectBestOfferRequestDTO dto)
    {
        try
        {
            var callerUserId = _authService.GetUserIdFromToken(User);
            var auction = await _auctionService.SelectBestOfferAsync(auctionId, dto.OfferId, callerUserId);
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
