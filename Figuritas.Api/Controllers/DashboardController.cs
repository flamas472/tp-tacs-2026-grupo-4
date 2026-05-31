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

    public DashboardController(
        UserService userService,
        ExchangeProposalService proposalService,
        AuctionService auctionService,
        AuthService authService)
    {
        _userService = userService;
        _proposalService = proposalService;
        _auctionService = auctionService;
        _authService = authService;
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
    public ActionResult<List<AuctionResponseDTO>> GetMyAuctions([FromQuery] GetMyAuctionsDTO dto)
    {
        var callerId = _authService.GetUserIdFromToken(User);
        var auctions = _auctionService.GetMyAuctions(dto, callerId);
        return Ok(auctions);
    }
}
