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
    private readonly SuggestionService _suggestionService;

    public MarketController(UserService userService, AuthService authService, SuggestionService suggestionService)
    {
        _userService = userService;
        _authService = authService;
        _suggestionService = suggestionService;
    }

    [HttpGet("stickers")]
    public ActionResult<List<MarketStickerResponseDTO>> GetMarketStickers([FromQuery] GetMarketStickersDTO queryParams)
    {
        var callerUserId = _authService.GetUserIdFromToken(User);
        var results = _userService.SearchMarketStickers(queryParams, callerUserId);
        return Ok(results);
    }

    [HttpGet("suggestions")]
    public async Task<ActionResult<List<ExchangeSuggestionResponseDTO>>> GetSuggestions([FromQuery] GetSuggestionsDTO queryParams)
    {
        var callerUserId = _authService.GetUserIdFromToken(User);
        var suggestions = await _suggestionService.GetSuggestionsAsync(callerUserId, queryParams.Page, queryParams.PageSize);
        return Ok(suggestions);
    }
}
