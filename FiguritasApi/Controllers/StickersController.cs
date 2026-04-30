using FiguritasApi.Model;
using Microsoft.AspNetCore.Mvc;

namespace StickersApi.Controllers;

[ApiController]
[Route("api/[controller]")] // .NET replaces [controller] with the class name without "Controller".

// So, the endpoint is: api/Stickers
public class StickersController : ControllerBase
{

    private readonly StickerRepository _repo;

    public StickersController(StickerRepository repo)
    {
        _repo = repo;
    }

    [HttpGet] 
    public ActionResult<List<Sticker>> GetStickers()
    {
        var Stickers = _repo.GetAll();
        return Ok(Stickers);
    }

    [HttpPost]
    public ActionResult<Sticker> PostSticker(Sticker sticker)
    {
        _repo.Add(sticker);
        return CreatedAtAction(nameof(GetStickers), new { id = sticker.Id }, sticker);
    }

    [HttpPut("{id}")]
    public IActionResult PutSticker(int id, [FromBody] Sticker sticker)
    {
        if (id != sticker.Id)
            return BadRequest();

        var existant = _repo.GetById(id);

        if (existant == null)
            return NotFound();

        _repo.Update(id, sticker);

    return NoContent();
}
}