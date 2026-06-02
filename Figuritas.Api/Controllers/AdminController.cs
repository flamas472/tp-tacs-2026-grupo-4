using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Figuritas.Api.Services;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Figuritas.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly AdminAnalyticsService _analyticsService;
    private readonly AdminService _adminService;
    private readonly AuthService _authService;

    public AdminController(AdminAnalyticsService analyticsService, AdminService adminService, AuthService authService)
    {
        _analyticsService = analyticsService;
        _adminService = adminService;
        _authService = authService;
    }

    // ─── Analytics ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a full platform analytics summary.
    /// Accessible by Admin and SuperAdmin.
    /// </summary>
    [HttpGet("analytics/summary")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<PlatformSummaryResponseDTO>> GetAnalyticsSummary()
    {
        var summary = await _analyticsService.GetPlatformSummaryAsync();
        return Ok(summary);
    }

    // ─── Admin management (SuperAdmin only) ───────────────────────────────────

    /// <summary>
    /// Creates a new administrator account.
    /// Restricted to SuperAdmin.
    /// </summary>
    [HttpPost("admins")]
    [Authorize(Roles = "SuperAdmin")]
    public ActionResult<AdminUserResponseDTO> CreateAdmin([FromBody] CreateAdminRequestDTO dto)
    {
        try
        {
            var admin = _adminService.CreateAdmin(dto);
            var response = new AdminUserResponseDTO
            {
                Id = admin.Id,
                Username = admin.Username,
                Role = admin.Role,
                Reputation = admin.Reputation
            };
            return StatusCode(201, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Lists all Admin and SuperAdmin accounts, paginated.
    /// Restricted to SuperAdmin.
    /// </summary>
    [HttpGet("admins")]
    [Authorize(Roles = "SuperAdmin")]
    public ActionResult<List<AdminUserResponseDTO>> GetAdmins(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20)
    {
        var admins = _adminService.GetAllAdmins(page, pageSize);
        return Ok(admins);
    }

    /// <summary>
    /// Promotes or demotes an admin between Admin and SuperAdmin roles.
    /// Restricted to SuperAdmin.
    /// </summary>
    [HttpPatch("admins/{id}/role")]
    [Authorize(Roles = "SuperAdmin")]
    public ActionResult<AdminUserResponseDTO> PatchAdminRole(int id, [FromBody] PatchAdminRoleRequestDTO dto)
    {
        try
        {
            var result = _adminService.PatchAdminRole(id, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Revokes admin privileges from an Admin-role user, demoting them to regular User.
    /// Cannot be used against SuperAdmin accounts or the caller themselves.
    /// Restricted to SuperAdmin.
    /// </summary>
    [HttpDelete("admins/{id}/role")]
    [Authorize(Roles = "SuperAdmin")]
    public ActionResult RevokeAdminRole(int id)
    {
        try
        {
            var callerSuperAdminId = _authService.GetUserIdFromToken(User);
            _adminService.RevokeAdmin(id, callerSuperAdminId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // ─── User management ──────────────────────────────────────────────────────

    /// <summary>
    /// Lists all platform users with pagination.
    /// Accessible by Admin and SuperAdmin.
    /// </summary>
    [HttpGet("users")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public ActionResult<List<UserResponseDTO>> GetUsers(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20)
    {
        var users = _adminService.GetAllUsers(page, pageSize);
        return Ok(users);
    }

    /// <summary>
    /// Bans a regular user account.
    /// Accessible by Admin and SuperAdmin.
    /// Cannot ban another administrator or oneself.
    /// </summary>
    [HttpPost("users/{userId}/ban")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public ActionResult BanUser(int userId)
    {
        try
        {
            var callerAdminId = _authService.GetUserIdFromToken(User);
            _adminService.BanUser(userId, callerAdminId);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Removes the ban from a user account, restoring active status.
    /// Accessible by Admin and SuperAdmin.
    /// </summary>
    [HttpPost("users/{userId}/unban")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public ActionResult UnbanUser(int userId)
    {
        try
        {
            _adminService.UnbanUser(userId);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
