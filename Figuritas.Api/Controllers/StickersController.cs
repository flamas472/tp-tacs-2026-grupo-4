using Figuritas.Shared.Model;
using Figuritas.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Figuritas.Shared.DTO;

namespace Figuritas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]

public class StickersController(StickerService stickerService) : ControllerBase
{

    private readonly StickerService _stickerService = stickerService;

    [HttpGet]
    public ActionResult<List<Sticker>> GetStickers([FromQuery] GetStickersDTO queryParams)
    {
        if(queryParams.Page <= 0 || queryParams.PageSize <= 0)
        {
            return BadRequest("Page and PageSize must be greater than 0.");
        }
        var stickers = _stickerService.Get(queryParams);

        return Ok(stickers);
    }

    [HttpGet("{id}")]
    public ActionResult<Sticker> GetStickerById(int id)
    {
        var sticker = _stickerService.GetById(id);
        if (sticker == null) return NotFound($"Sticker {id} not found.");
        return Ok(sticker);
    }
}