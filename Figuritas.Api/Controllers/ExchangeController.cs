using Figuritas.Shared.Model;
using Figuritas.Shared.Utils;
using Figuritas.Shared.DTO;
using Figuritas.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Figuritas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]

public class ExchangeController : ControllerBase
{

    private readonly UserService _userService;
    private readonly AuthService _authService;


    public ExchangeController(UserService userService, AuthService authService)
    {
        _userService = userService;
        _authService = authService;
    }

   
    // ENDPOINT_US10
    [HttpPost("{exchangeID}/rate")]
    public ActionResult<Rate> PostUserRate(int exchangeID, PostRateDTO postRateDTO)
    {
        try
        {
            var raterId = _authService.GetUserIdFromToken(User);
            var rate = _userService.CreateUserRate(exchangeID, postRateDTO, raterId);
            return CreatedAtAction(nameof(PostUserRate), rate);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

}

