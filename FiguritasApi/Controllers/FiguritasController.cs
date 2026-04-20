using Microsoft.AspNetCore.Mvc;
using FiguritasApi.Model;
using FiguritasApi.Repositories;

namespace FiguritasApi.Controllers;

[ApiController]
[Route("[controller]")] // .NET reemplaza [controller] por el nombre de la clase sin “Controller”. 

// Entonces, teniendo en cuenta lo de arriba, el endpoint es: api/Figurita
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
        return StatusCode(201, figurita);
    }


}