using System.ComponentModel.DataAnnotations;
using Figuritas.Api.Services;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Figuritas.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly UserService _userService;
    private readonly ExchangeProposalService _proposalService;
    private readonly AuctionService _auctionService;
    private readonly AuthService _authService;
    private readonly INotificationService _notificationService;

    public DashboardController(
        UserService userService,
        ExchangeProposalService proposalService,
        AuctionService auctionService,
        AuthService authService,
        INotificationService notificationService)
    {
        _userService = userService;
        _proposalService = proposalService;
        _auctionService = auctionService;
        _authService = authService;
        _notificationService = notificationService;
    }

    [HttpGet("stickers")]
    public ActionResult<List<MyPublishedStickerResponseDTO>> GetMyStickers([FromQuery] GetMyStickersDTO dto)
    {
        var callerId = _authService.GetUserIdFromToken(User);
        var stickers = _userService.GetMyPublishedStickers(dto, callerId);
        return Ok(stickers);
    }

    [HttpGet("proposals/sent")]
    public ActionResult<List<ExchangeProposalResponseDTO>> GetMySentProposals([FromQuery] GetMyProposalsDTO dto)
    {
        var callerId = _authService.GetUserIdFromToken(User);
        var proposals = _proposalService.GetAllSentProposals(callerId, dto);
        return Ok(proposals);
    }

    [HttpGet("proposals/received")]
    public ActionResult<List<ExchangeProposalResponseDTO>> GetMyReceivedProposals([FromQuery] GetMyProposalsDTO dto)
    {
        var callerId = _authService.GetUserIdFromToken(User);
        var proposals = _proposalService.GetAllReceivedProposals(callerId, dto);
        return Ok(proposals);
    }

    [HttpGet("auctions")]
    public async Task<ActionResult<List<AuctionResponseDTO>>> GetMyAuctions([FromQuery] GetMyAuctionsDTO dto)
    {
        var callerId = _authService.GetUserIdFromToken(User);
        var auctions = await _auctionService.GetMyAuctions(dto, callerId);
        return Ok(auctions);
    }

    /// <summary>
    /// GET /api/dashboard/bids
    /// Returns a paged list of all bids placed by the authenticated user on auctions
    /// created by other users. Each item includes sticker info, offer state, and a flag
    /// indicating whether the bid is currently the leading (winning) offer.
    /// </summary>
    [HttpGet("bids")]
    public async Task<ActionResult<List<MyBidResponseDTO>>> GetMyBids(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20)
    {
        var callerId = _authService.GetUserIdFromToken(User);
        var bids = await _auctionService.GetMyBidsAsync(callerId, page, pageSize);
        return Ok(bids);
    }

    [HttpPut("preferences")]
    public ActionResult UpdatePreferences([FromBody] UpdatePreferencesDTO dto)
    {
        try
        {
            var callerId = _authService.GetUserIdFromToken(User);
            _notificationService.UpdatePreferences(callerId, dto);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("notifications")]
    public async Task<ActionResult<List<NotificationResponseDTO>>> GetMyNotifications([FromQuery] GetMyNotificationsDTO dto)
    {
        var callerId = _authService.GetUserIdFromToken(User);
        var notifications = await _notificationService.GetMyNotificationsAsync(callerId, dto);
        return Ok(notifications);
    }

    [HttpPatch("notifications/{id}/read")]
    public async Task<ActionResult> MarkNotificationAsRead(int id)
    {
        try
        {
            var callerId = _authService.GetUserIdFromToken(User);
            await _notificationService.MarkAsReadAsync(id, callerId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, "This notification does not belong to you.");
        }
    }
}
