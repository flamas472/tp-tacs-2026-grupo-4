using Figuritas.Shared.DTO;
using Figuritas.Shared.Model;
using Figuritas.Shared.Utils;
using Microsoft.AspNetCore.Mvc;

namespace Figuritas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryStickersController : ControllerBase
{
    private readonly UserStickerRepository _inventoryRepo;

    public InventoryStickersController(UserStickerRepository inventoryRepo)
    {
        _inventoryRepo = inventoryRepo;
    }


    //* ENDPOINT_US03
    [HttpGet]
    public ActionResult<List<UserSticker>> SearchInventoryStickers(
        [FromQuery] GetUserStickersDto queryParams
    )
    {
        try
        {
            Func<UserSticker, bool> filter = queryParams.ToPredicate();
            var results = _inventoryRepo.GetPaginated(queryParams.Page, queryParams.PageSize, filter);
            return Ok(results);
        }
        catch (ArgumentException ex)
        {
            if(ex.Message == "Page and PageSize must be grater than 0") return BadRequest(ex.Message);
            return StatusCode(500);
        }
    }
}