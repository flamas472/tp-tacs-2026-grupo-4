using Figuritas.Shared.Model;
using Figuritas.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Figuritas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NationalTeamController : ControllerBase
{
    private readonly NationalTeamService _nationalTeamService;

    public NationalTeamController(NationalTeamService nationalTeamService)
    {
        _nationalTeamService = nationalTeamService;
    }

    [HttpGet]
    public ActionResult<List<NationalTeam>> GetAll()
    {
        return Ok(_nationalTeamService.GetAllNationalTeams());
    }
}
