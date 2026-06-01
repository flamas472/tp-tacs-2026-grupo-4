using Microsoft.AspNetCore.Mvc;
using Figuritas.Api.Services;
using Figuritas.Shared.DTO;
using Figuritas.Shared.DTO.response;
using Microsoft.AspNetCore.Authorization;
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

        // El verdadero debate de seguridad: ¿Dónde lo guarda el Frontend? 
        return Ok(new { Token = token });
    }

    [HttpPost("register")]
    public ActionResult<UserResponseDTO> Register([FromBody] PostUserDTO userDTO)
    {
        try
        {
            var user = _userService.CreateUser(userDTO);
            var response = new UserResponseDTO
            {
                Id = user.Id,
                Username = user.Username,
                Reputation = user.Reputation
            };
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        // JWT is stateless — no server-side invalidation required.
        // Clients are responsible for discarding the token on their side.
        return Ok(new { Message = "Logged out successfully." });
    }
}
