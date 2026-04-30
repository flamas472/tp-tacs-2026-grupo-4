using FiguritasApi.Model;
using Microsoft.AspNetCore.Mvc;

namespace StickersApi.Controllers;

[ApiController]
[Route("api/search")]
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
        [FromQuery] NationalTeam? selection,
        [FromQuery] Team? team,
        [FromQuery] Category? category,
        [FromQuery] bool? canBeExchanged,
        [FromQuery] bool? active)
    {
        var results = _inventoryRepo.GetAll(fig =>
            (!number.HasValue || fig.Sticker.Number == number.Value) &&
            (!selection.HasValue || fig.Sticker.NationalTeam == selection.Value) &&
            (!team.HasValue || fig.Sticker.Team == team.Value) &&
            (!category.HasValue || fig.Sticker.Category == category.Value) &&
            (!canBeExchanged.HasValue || fig.CanBeExchanged == canBeExchanged.Value) &&
            (!active.HasValue || fig.Active == active.Value)
        );
        return Ok(results);
    }
}