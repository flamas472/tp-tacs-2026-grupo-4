using Microsoft.AspNetCore.Mvc;
using FiguritasApi.Model;
using FiguritasApi.Repositories;
using FiguritasApi.Controllers.DTO;

namespace FiguritasApi.Controllers;

[ApiController]
[Route("[controller]")] // .NET reemplaza [controller] por el nombre de la clase sin “Controller”. 

public class IntercambiosController : ControllerBase
{

    private readonly IntercambiosRepository _repo;

    public IntercambiosController(IntercambiosRepository repo)
    {
        _repo = repo;
    }

    [HttpGet] 
    public ActionResult<List<Intercambio>> GetIntercambios([FromQuery] GetIntercambiosDto dto)
    {
        var Intercambios = _repo.GetAll(dto.ToPredicate());
        return Ok(Intercambios);
    }

    [HttpPost] //Endpoint para testear
    public ActionResult<Intercambio> CreateIntercambio(Intercambio intercambio)
    {
        _repo.Add(intercambio);
        return StatusCode(201, intercambio);
    }

}

