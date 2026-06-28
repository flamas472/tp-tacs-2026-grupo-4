using Microsoft.AspNetCore.Mvc;
using Figuritas.Api.Services;
using Figuritas.Shared.DTO;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

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
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDTO loginDto)
    {
        try
        {
            var user = _userService.ValidateCredentials(loginDto.Username, loginDto.Password);
            if (user == null)
                return Unauthorized("Invalid credentials.");

            var response = await _authService.LoginAsync(user);
            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, "Account is banned.");
        }
    }

    [HttpPost("register")]
    [EnableRateLimiting("register")]
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

    [HttpPost("refresh")]
    [EnableRateLimiting("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDTO dto)
    {
        var result = await _authService.RefreshTokensAsync(dto.RefreshToken);
        if (result == null)
            return Unauthorized("Invalid or expired refresh token.");

        return Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDTO dto)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await _authService.RevokeRefreshTokenAsync(dto.RefreshToken, userId);
        return NoContent();
    }
}
