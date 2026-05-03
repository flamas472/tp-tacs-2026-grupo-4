using Figuritas.Shared.Model;
using Figuritas.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Figuritas.Api.Controllers;

[ApiController]
[Route("api/[controller]")] // .NET replaces [controller] with the class name without "Controller".

// So, the endpoint is: api/Stickers
public class StickersController : ControllerBase
{

    private readonly StickerService _stickerService;

    public StickersController(StickerService stickerService)
    {
        _stickerService = stickerService;
    }

    [HttpGet] // /stickers?team={team} o /stickers?number={number} o /stickers?nationalTeamId={nationalTeamId} ... (y sus combinaciones)
    public ActionResult<List<Sticker>> GetStickers(int? number, string? team, int? nationalTeamId, int? categoryId, string? description)
    {
        var stickers = _stickerService.GetAllStickers();

        if (number.HasValue)
            stickers = stickers.Where(s => s.Number == number.Value).ToList();

        if (!string.IsNullOrEmpty(team))
            stickers = stickers.Where(s => s.Team.Description.Equals(team, StringComparison.OrdinalIgnoreCase)).ToList();

        if (nationalTeamId.HasValue)
            stickers = stickers.Where(s => s.NationalTeam.Id == nationalTeamId.Value).ToList();

        if (categoryId.HasValue)
            stickers = stickers.Where(s => s.Category.Id == categoryId.Value).ToList();

        if (!string.IsNullOrEmpty(description))
            stickers = stickers.Where(s => s.Description.Equals(description, StringComparison.OrdinalIgnoreCase)).ToList();

        return Ok(stickers);        
    }
}