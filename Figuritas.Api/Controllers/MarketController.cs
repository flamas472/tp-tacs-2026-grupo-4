using Figuritas.Api.Services;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Figuritas.Api.Controllers;

[ApiController]
[Route("api/market")]
[Authorize]
public class MarketController : ControllerBase
{
    private readonly UserService _userService;
    private readonly AuthService _authService;

    public MarketController(UserService userService, AuthService authService)
    {
        _userService = userService;
        _authService = authService;
    }

    [HttpGet("stickers")]
    public ActionResult<List<MarketStickerResponseDTO>> GetMarketStickers([FromQuery] GetMarketStickersDTO queryParams)
    {
        var callerUserId = _authService.GetUserIdFromToken(User);
        var results = _userService.SearchMarketStickers(queryParams, callerUserId);
        return Ok(results);
    }
}
