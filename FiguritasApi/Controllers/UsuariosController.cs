using Microsoft.AspNetCore.Mvc;
using FiguritasApi.Model;
using FiguritasApi.Repositories;
using FiguritasApi.Controllers.DTO;

namespace FiguritasApi.Controllers;

[ApiController]
[Route("[controller]")]  
public class UsuariosController : ControllerBase
{

    private readonly UsuarioRepository _repoUsuarios;
    private readonly FiguritaRepetidaRepository _repoFigRep;

    private readonly IntercambiosRepository _repoPropuestaIntercambio;

    public UsuariosController(UsuarioRepository repoUsuarios, FiguritaRepetidaRepository repoFigRep, IntercambiosRepository repoPropuestaIntercambio)
    {
        _repoPropuestaIntercambio = repoPropuestaIntercambio;
        _repoUsuarios = repoUsuarios;
        _repoFigRep = repoFigRep;
    }

    // Rutas REST para gestionar usuarios.
    [HttpGet]
    public ActionResult<List<Usuario>> GetUsers()
    {
        var usuarios = _repoUsuarios.GetAll();
        return Ok(usuarios);
    }

    [HttpPost]
    public ActionResult<Figurita> PostUsers(Usuario usuario)
    {
        _repoUsuarios.Add(usuario);
        foreach (var figuritaRepetida in usuario.FiguritasRepetidas)
        {
            _repoFigRep.Add(figuritaRepetida);
        }
        
        return StatusCode(201, usuario);
    }

    // Rutas REST para gestionar las figuritas repetidas del usuario.
      [HttpPost("{usuarioId}/Repetidas")]
    public ActionResult<FiguritaRepetida> PostFiguritaRepetida(int usuarioId, PostFiguritaRepetidaDto figuritaRepetidaDto)
    {
        Console.WriteLine($"Aniadiendo Repetida al Usuario ID: {usuarioId}, figurita: {figuritaRepetidaDto.Figurita.numero}, puedeIntercambiarse: {figuritaRepetidaDto.PuedeIntercambiarse}, activo: {figuritaRepetidaDto.Activo}");
        var usuario = _repoUsuarios.GetByID(usuarioId);
        if(usuario == null) 
        {
            return NotFound("Usuario no encontrado.");
        }
        var figuritaRepetida = figuritaRepetidaDto.ToDomain(usuarioId);
        _repoFigRep.Add(figuritaRepetida);
        usuario.AgregarFiguritaRepetida(figuritaRepetida);
        return StatusCode(201, figuritaRepetida);
    }

    [HttpPost("{usuarioId}/faltantes")]
    public ActionResult<List<Figurita>> PostFiguritaFaltantes(int usuarioId, PostFiguritaFaltanteDto faltanteDto)
    {
        var usuario = _repoUsuarios.GetByID(usuarioId);
        if(usuario == null) 
        {
            return NotFound("Usuario no encontrado.");
        }
        var figuritaFaltante = faltanteDto.ToDomain();
        usuario.AgregarFiguritaFaltante(figuritaFaltante);
        return StatusCode(201, usuario.FiguritasFaltantes);
    }

    [HttpGet("{usuarioId}/faltantes")]
    public ActionResult<List<Figurita>> GetFiguritasFaltantes(int usuarioId)
    {
        var usuario = _repoUsuarios.GetByID(usuarioId);
        if(usuario == null) 
        {
            return NotFound("Usuario no encontrado.");
        }
        return Ok(usuario.FiguritasFaltantes);
    }

    [HttpPost("{usuarioId}/intercambios")]
    public ActionResult<List<Intercambio>> PostFiguritaIntercambio(int usuarioId, PostPropuestaIntercambioDto propuestaIntercambioDto)
    {
        Console.WriteLine($"Usuario Proponente ID: {propuestaIntercambioDto.UsuarioProponenteID}");
        var usuarioProponente = _repoUsuarios.GetByID(propuestaIntercambioDto.UsuarioProponenteID);
        var usuarioPropuesto = _repoUsuarios.GetByID(usuarioId);
        if(usuarioProponente == null ) 
        {
            return NotFound("Usuario proponente no encontrado.");
        }
        if(usuarioPropuesto == null) 
        {
            return NotFound("Usuario propuesto no encontrado.");
        }
        var propuestaIntercambio = propuestaIntercambioDto.ToDomain(usuarioProponente, usuarioPropuesto);
        _repoPropuestaIntercambio.Add(propuestaIntercambio);
        return StatusCode(201, propuestaIntercambio);
    }

    [HttpGet("{usuarioId}/recomendaciones")]
    public ActionResult<List<Figurita>> GetRecomendaciones(int usuarioId, [FromQuery] PaginatedRequestDto pagination)
    {
        var usuario = _repoUsuarios.GetByID(usuarioId);
        if (usuario == null) 
        {
            return NotFound("Usuario no encontrado.");
        }
        List<Figurita> figuritasFaltantes = usuario.FiguritasFaltantes;
        List<FiguritaRepetida> recomendaciones = _repoFigRep.GetAll(
            fr => fr.puedeIntercambiarse
            && fr.activo 
            && figuritasFaltantes.Any(ff => ff.id == fr.figurita.id),
            page: pagination.Page, pageSize: pagination.PageSize);
        return Ok(recomendaciones);
    }

}