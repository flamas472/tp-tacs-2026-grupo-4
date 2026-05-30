using Figuritas.Shared.DTO;
using Figuritas.Shared.Model;
using Microsoft.AspNetCore.Mvc;
using Figuritas.Api.Services;

namespace Figuritas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryStickersController : ControllerBase
{
    private readonly UserService _userService;

    public InventoryStickersController(UserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public ActionResult<List<UserSticker>> SearchInventoryStickers(
        [FromQuery] GetUserStickersDTO queryParams
    )
    {
        try
        {
            var results = _userService.SearchUserStickers(queryParams);
            return Ok(results);
        }
        catch (ArgumentException ex)
        {
            if (ex.Message == "Page and PageSize must be greater than 0") return BadRequest(ex.Message);
            return StatusCode(500);
        }
    }
}
