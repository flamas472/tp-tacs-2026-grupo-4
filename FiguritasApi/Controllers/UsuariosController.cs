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

    public UsuariosController(UsuarioRepository repoUsuarios, FiguritaRepetidaRepository repoFigRep)
    {
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
        return StatusCode(201, usuario);
    }

    // Rutas REST para gestionar las figuritas repetidas del usuario.
      [HttpPost("{usuarioId}/Repetidas")]
    public ActionResult<Figurita> PostFiguritaRepetida(FiguritaRepetida figuritaRepetida)
    {
        _repoFigRep.Add(figuritaRepetida);
        Usuario usuario = _repoUsuarios.GetByID(figuritaRepetida.usuarioID);
        usuario.figuritasRepetidas.Add(figuritaRepetida);

        return StatusCode(201, figuritaRepetida);
    }


}