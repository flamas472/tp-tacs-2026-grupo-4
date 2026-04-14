using Microsoft.AspNetCore.Mvc;
using FiguritasApi.Model;

namespace FiguritasApi.Controllers;

[ApiController]
[Route("[controller]")]  

// Entonces, teniendo en cuenta lo de arriba, el endpoint es: api/Usuario

// Para cargar una figurita repetida del usuario vamos a tener una ruta del estilo: POST /api/Usuarios/{usuarioId}/Repetidas
public class UsuariosController : ControllerBase
{

    private readonly UsuarioRepository _repoUsuarios;
    private readonly FiguritaRepetidaRepository _repoFigRep;

    private readonly PropuestaIntercambioRepository _repoPropuestaIntercambio;

    public UsuariosController(UsuarioRepository repoUsuarios, FiguritaRepetidaRepository repoFigRep, PropuestaIntercambioRepository repoPropuestaIntercambio)
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
        foreach (var figuritaRepetida in usuario.figuritasRepetidas)
        {
            _repoFigRep.Add(figuritaRepetida);
        }
        
        return StatusCode(201, usuario);
    }

    // Rutas REST para gestionar las figuritas repetidas del usuario.
      [HttpPost("{usuarioId}/Repetidas")]
    public ActionResult<FiguritaRepetida> PostFiguritaRepetida(int usuarioId, PostRepetidaDto figuritaRepetidaDto)
    {
        var usuario = _repoUsuarios.GetByID(usuarioId);
        var figuritaRepetida = figuritaRepetidaDto.toDomain();
        _repoFigRep.Add(figuritaRepetida);
        usuario.AgregarFiguritaRepetida(figuritaRepetida);
        return StatusCode(201, figuritaRepetida);
    }

    [HttpPost("{usuarioId}/faltantes")]
    public ActionResult<List<Figurita>> PostFiguritaFaltantes(int usuarioId, PostFaltanteDto faltanteDto)
    {
        var usuario = _repoUsuarios.GetByID(usuarioId);
        var figuritaFaltante = faltanteDto.toDomain();
        usuario.AgregarFiguritaFaltante(figuritaFaltante);
        return StatusCode(201, usuario.figuritasFaltantes);
    }

    [HttpPost("{usuarioId}/intercambios")]
    public ActionResult<List<PropuestaIntercambio>> PostFiguritaIntercambio(int usuarioId, PostPropuestaIntercambioDto propuestaIntercambioDto)
    {
        Console.WriteLine($"Usuario Proponente ID: {propuestaIntercambioDto.usuarioProponenteID}");
        var usuarioProponente = _repoUsuarios.GetByID(propuestaIntercambioDto.usuarioProponenteID);
        var usuarioPropuesto = _repoUsuarios.GetByID(usuarioId);
        if(usuarioProponente == null ) 
        {
            return NotFound("Usuario proponente no encontrado.");
        }
        if(usuarioPropuesto == null) 
        {
            return NotFound("Usuario propuesto no encontrado.");
        }
        var propuestaIntercambio = propuestaIntercambioDto.toDomain(usuarioProponente, usuarioPropuesto);
        _repoPropuestaIntercambio.Add(propuestaIntercambio);
        return StatusCode(201, propuestaIntercambio);
    }

}

public class PostPropuestaIntercambioDto
{

    public required int usuarioProponenteID {get; set; }

    public required List<FiguritaRepetida> figuritasOfrecidas {get; set; }

    public required List<Figurita> figuritasARecibir {get; set; }

    public PropuestaIntercambio toDomain(Usuario proponente, Usuario propuesto) 
    {
        return new PropuestaIntercambio {
            id = 0, // El ID se asigna automáticamente al agregarlo a la base de datos.
            proponente = proponente,
            propuesto= propuesto,
            figuritasOfrecidas = this.figuritasOfrecidas,
            figuritasARecibir = this.figuritasARecibir,
            estado = EstadoPropuestaIntercambio.Pendiente
        };
    }

}

public class PostRepetidaDto
{
    public required Figurita figurita {get; set;}
    public bool puedeIntercambiarse {get; set; } // 0 --> Para subasta, 1 --> Para intercambios
    public bool activo {get; set; }

    public FiguritaRepetida toDomain() 
    {
        return new FiguritaRepetida {
            id = 0, // El ID se asigna automáticamente al agregarlo a la base de datos.
            figurita = this.figurita,
            puedeIntercambiarse = this.puedeIntercambiarse,
            activo = this.activo
        };
    }
    
}

public class PostFaltanteDto
{
    public required Figurita figurita {get; set;}

    public Figurita toDomain() 
    {
        return new Figurita {
            id = 0, // El ID se asigna automáticamente al agregarlo a la base de datos.
            seleccion = this.figurita.seleccion,
            equipo = this.figurita.equipo,
            categoria = this.figurita.categoria,
            numero = this.figurita.numero
        };
    }
    
}