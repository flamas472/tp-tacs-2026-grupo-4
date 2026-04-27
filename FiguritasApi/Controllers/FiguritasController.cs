using Microsoft.AspNetCore.Mvc;
using FiguritasApi.Model;

namespace FiguritasApi.Controllers;

[ApiController]
[Route("api/[controller]")] // .NET replaces [controller] with the class name without "Controller".

// So, the endpoint is: api/figuritas
public class FiguritasController : ControllerBase
{

    private readonly FiguritaRepository _repo;

    public FiguritasController(FiguritaRepository repo)
    {
        _repo = repo;
    }

    [HttpGet] 
    public ActionResult<List<Figurita>> GetFiguritas()
    {
        var figuritas = _repo.GetAll();
        return Ok(figuritas);
    }

    [HttpPost]
    public ActionResult<Figurita> PostFigurita(Figurita figurita)
    {
        _repo.Add(figurita);
        return CreatedAtAction(nameof(GetFiguritas), new { id = figurita.Id }, figurita);
    }
}