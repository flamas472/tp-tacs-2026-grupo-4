using Microsoft.AspNetCore.Mvc;
using FiguritasApi.Model;
using FiguritasApi.Repositories;
using FiguritasApi.Controllers.DTO;

namespace FiguritasApi.Controllers;

[ApiController]
[Route("[controller]")] // .NET reemplaza [controller] por el nombre de la clase sin “Controller”. 

// Entonces, teniendo en cuenta lo de arriba, el endpoint es: api/Subasta
public class SubastasController : ControllerBase
{

    private readonly SubastasRepository _repo;

    public SubastasController(SubastasRepository repo)
    {
        _repo = repo;
    }

    [HttpGet] 
    public ActionResult<List<Subasta>> GetSubastas([FromQuery] GetSubastasDto dto)
    {
        var subastas = _repo.GetAll(dto.ToPredicate());
        return Ok(subastas);
    }

    [HttpPost] //Endpoint para testear
    public ActionResult<Subasta> CreateSubasta(Subasta subasta)
    {
        _repo.Add(subasta);
        return StatusCode(201, subasta);
    }

}