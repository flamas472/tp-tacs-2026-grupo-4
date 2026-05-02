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

    [HttpGet] // /stickers?team={team} o /stickers?number={number} o /stickers?player={player} ... (y sus combinaciones)
    public ActionResult<List<Sticker>> GetStickers(int? number, Team? team, NationalTeam? nationalTeam, Category? category, string? player)
    {
        var stickers = _stickerService.GetAllStickers();

        if (number.HasValue)
            stickers = stickers.Where(s => s.Number == number.Value).ToList();

        if (team.HasValue)
            stickers = stickers.Where(s => s.Team == team.Value).ToList();

        if (nationalTeam.HasValue)
            stickers = stickers.Where(s => s.NationalTeam == nationalTeam.Value).ToList();

        if (category.HasValue)
            stickers = stickers.Where(s => s.Category == category.Value).ToList();

        if (!string.IsNullOrEmpty(player))
            stickers = stickers.Where(s => s.Player.Equals(player, StringComparison.OrdinalIgnoreCase)).ToList();

        return Ok(stickers);        
    }
}