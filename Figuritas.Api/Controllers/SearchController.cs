using Figuritas.Shared.Model;
using Microsoft.AspNetCore.Mvc;

namespace Figuritas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly UserStickerRepository _inventoryRepo;

    public SearchController(UserStickerRepository inventoryRepo)
    {
        _inventoryRepo = inventoryRepo;
    }

    [HttpGet("inventory-stickers")]
    public ActionResult<List<UserSticker>> SearchInventoryStickers(
        [FromQuery] int? number,
        [FromQuery] int? selection,
        [FromQuery] string? team,
        [FromQuery] int? category,
        [FromQuery] bool? canBeExchanged,
        [FromQuery] bool? active)
    {
        var results = _inventoryRepo.GetAll(fig =>
            (!number.HasValue || fig.Sticker.Number == number.Value) &&
            (!selection.HasValue || fig.Sticker.NationalTeam.Id == selection.Value) &&
            (string.IsNullOrEmpty(team) || fig.Sticker.Team.Description.Equals(team, StringComparison.OrdinalIgnoreCase)) &&
            (!category.HasValue || fig.Sticker.Category.Id == category.Value) &&
            (!canBeExchanged.HasValue || fig.CanBeExchanged == canBeExchanged.Value) &&
            (!active.HasValue || fig.Active == active.Value)
        );
        return Ok(results);
    }
}