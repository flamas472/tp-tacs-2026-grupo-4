using Microsoft.AspNetCore.Mvc;
using FiguritasApi.Model;

namespace FiguritasApi.Controllers;

[ApiController]
[Route("[controller]")] // .NET reemplaza [controller] por el nombre de la clase sin “Controller”. 

// Entonces, teniendo en cuenta lo de arriba, el endpoint es: api/Figurita
public class FiguritasRepetidasController : ControllerBase
{

    private readonly FiguritaRepetidaRepository _repo;

    public FiguritasRepetidasController(FiguritaRepetidaRepository repo)
    {
        _repo = repo;
    }

    [HttpGet] 
    public ActionResult<List<FiguritaRepetida>> GetFiguritasRepetidas([FromQuery] GetFiguritasRepetidasDto dto)
    {
        var figuritas = _repo.GetAll(dto.toPredicate());
        return Ok(figuritas);
    }

    [HttpPost] //Endpoint para testear
    public ActionResult<FiguritaRepetida> CreateFiguritaRepetida(FiguritaRepetida figuritaRepetida)
    {
        _repo.Add(figuritaRepetida);
        return StatusCode(201, figuritaRepetida);
    }

}

public class GetFiguritasRepetidasDto
{
    public int? numero {get; set; }
    public int? seleccionId {get; set; }
    public int? equipoId {get; set; }
    public int? categoriaId {get; set; }

    public Func<FiguritaRepetida, bool> toPredicate()
    {
        return figurita => 
            (!numero.HasValue || figurita.figurita.numero == numero) &&
            (!seleccionId.HasValue || figurita.figurita.seleccion == (Seleccion)seleccionId) &&
            (!equipoId.HasValue || figurita.figurita.equipo == (Equipo)equipoId) &&
            (!categoriaId.HasValue || figurita.figurita.categoria == (Categoria)categoriaId);
    }
}