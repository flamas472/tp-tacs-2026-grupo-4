using Figuritas.Shared.Model;
using Figuritas.Shared.Utils;
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
        [FromQuery] string? nationalTeam,
        [FromQuery] string? team,
        [FromQuery] string? category,
        [FromQuery] bool? canBeExchanged
        )
    {
        var results = _inventoryRepo.GetAll(fig =>
            (!number.HasValue || fig.Sticker.Number == number.Value) 
            && nationalTeam.AllWordsAreContainedBy(fig.Sticker.NationalTeam) 
            && team.AllWordsAreContainedBy(fig.Sticker.Team)
            && category.AllWordsAreContainedBy(fig.Sticker.Category)
            && (!canBeExchanged.HasValue || fig.CanBeExchanged == canBeExchanged.Value) 
        );
        return Ok(results);
    }
}