using Microsoft.AspNetCore.Mvc;
using Figuritas.Shared.Model;
using Figuritas.Api.Services;
using Figuritas.Shared.DTO;

namespace Figuritas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly UserService _userService; // Necesitás tu servicio de usuarios

    public AuthController(AuthService authService, UserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] PostUserDTO loginDto)
    {
        // 1. Validar identidad (esto es parte de la lógica que ya debés tener)
        var user = _userService.ValidateCredentials(loginDto);
        
        if (user == null) return Unauthorized("Credenciales inválidas");

        // 2. Generar el "pasaporte" usando el servicio del Paso 3
        var token = _authService.GenerateToken(user);

        // 3. Entregarlo al cliente
        return Ok(new { Token = token });
    }
}
