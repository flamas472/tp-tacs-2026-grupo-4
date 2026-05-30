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
    private readonly UserService _userService;

    public AuthController(AuthService authService, UserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] PostUserDTO loginDto)
    {
        var user = _userService.ValidateCredentials(loginDto);

        if (user == null) return Unauthorized("Invalid credentials.");

        var token = _authService.GenerateToken(user);

        return Ok(new { Token = token });
    }
}
