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

    public AdminController(AdminAnalyticsService analyticsService, AdminService adminService)
    {
        _analyticsService = analyticsService;
        _adminService = adminService;
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
    public ActionResult<UserResponseDTO> CreateAdmin([FromBody] CreateAdminRequestDTO dto)
    {
        try
        {
            var admin = _adminService.CreateAdmin(dto);
            var response = new UserResponseDTO
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
    /// Lists all Admin and SuperAdmin accounts.
    /// Restricted to SuperAdmin.
    /// </summary>
    [HttpGet("admins")]
    [Authorize(Roles = "SuperAdmin")]
    public ActionResult<List<UserResponseDTO>> GetAdmins()
    {
        var admins = _adminService.GetAllAdmins();
        return Ok(admins);
    }

    /// <summary>
    /// Promotes or demotes an admin between Admin and SuperAdmin roles.
    /// Restricted to SuperAdmin.
    /// </summary>
    [HttpPatch("admins/{id}/role")]
    [Authorize(Roles = "SuperAdmin")]
    public ActionResult<UserResponseDTO> PatchAdminRole(int id, [FromBody] PatchAdminRoleRequestDTO dto)
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
}
