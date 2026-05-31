using Figuritas.Api.Services;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Figuritas.Api.Controllers;

[ApiController]
[Route("api/ratings")]
[Authorize]
public class RatingsController : ControllerBase
{
    private readonly UserService _userService;
    private readonly AuthService _authService;

    public RatingsController(UserService userService, AuthService authService)
    {
        _userService = userService;
        _authService = authService;
    }

    [HttpPost]
    public ActionResult<RatingResponseDTO> PostRating(PostRatingRequestDTO dto)
    {
        try
        {
            var raterId = _authService.GetUserIdFromToken(User);
            var rate = _userService.CreateUserRate(dto, raterId);

            var response = new RatingResponseDTO
            {
                Id = rate.Id,
                ExchangeId = rate.ExchangeId,
                EvaluatorUserId = rate.EvaluatorUserId,
                TargetUserId = rate.TargetUserId,
                Stars = rate.Stars,
                Comment = rate.Comment,
                CreatedAt = rate.CreatedAt
            };

            return StatusCode(201, response);
        }
        catch (ArgumentException ex)
        {
            return ex.Message switch
            {
                "Self-rating is not allowed" => BadRequest(ex.Message),
                "Not participant" => StatusCode(403, ex.Message),
                "Exchange not found" => NotFound(ex.Message),
                "Already rated" => Conflict(ex.Message),
                _ => BadRequest(ex.Message)
            };
        }
    }
}
