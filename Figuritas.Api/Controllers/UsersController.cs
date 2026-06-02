using Figuritas.Shared.Model;
using Figuritas.Shared.DTO;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Figuritas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;
    private readonly AuthService _authService;

    public UsersController(UserService userService, AuthService authService)
    {
        _userService = userService;
        _authService = authService;
    }

    [HttpPost("{userId}/stickers")]
    public async Task<ActionResult<UserStickerResponseDTO>> PostUserSticker(int userId, PostUserStickerRequestDTO data)
    {
        try
        {
            var authenticatedUserId = _authService.GetUserIdFromToken(User);
            if (authenticatedUserId != userId)
                return StatusCode(403, "You can only publish stickers for your own account.");

            var userSticker = await _userService.CreateUserStickerAsync(userId, data);
            var responseDto = new UserStickerResponseDTO
            {
                Id = userSticker.Id,
                UserId = userSticker.UserId,
                Quantity = userSticker.Quantity,
                CanBeDirectlyExchanged = userSticker.CanBeDirectlyExchanged,
                CanBeAuctioned = userSticker.CanBeAuctioned,
                Active = userSticker.Active
            };
            return CreatedAtAction(nameof(GetUserStickerById), new { userId = userId, stickerId = userSticker.Id }, responseDto);
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Equals("User not found")) return NotFound(ex.Message);
            if (ex.Message.Equals("Inventory already registered")) return Conflict(ex.Message);
            if (ex.Message.Equals("Sticker not found in catalog")) return NotFound(ex.Message);
            return StatusCode(500);
        }
    }

    [HttpGet("{userId}/stickers")]
    [AllowAnonymous]
    public ActionResult<List<MarketStickerResponseDTO>> GetUserStickers(int userId, [FromQuery] GetUserStickersDTO dto)
    {
        try
        {
            var stickers = _userService.GetUserStickersByUserId(userId, dto);
            return Ok(stickers);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("{userId}/stickers/{stickerId}")]
    public ActionResult<UserSticker> GetUserStickerById(int userId, int stickerId)
    {
        var userSticker = _userService.GetUserStickerById(userId, stickerId);
        if (userSticker == null) return NotFound("Sticker not found in user inventory.");
        return Ok(userSticker);
    }

    [HttpPost("{userId}/missing-stickers")]
    public async Task<ActionResult<MissingSticker>> PostMissingSticker(int userId, PostMissingStickerRequestDTO data)
    {
        try
        {
            var authenticatedUserId = _authService.GetUserIdFromToken(User);
            if (authenticatedUserId != userId)
                return StatusCode(403, "You can only manage your own missing stickers.");

            var missingSticker = await _userService.AddMissingStickerToUser(userId, data.StickerId);
            return CreatedAtAction(nameof(GetMissingStickers), new { userId = userId }, missingSticker);
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Equals("User not found")) return NotFound(ex.Message);
            if (ex.Message.Equals("Missing sticker already registered")) return Conflict(ex.Message);
            if (ex.Message.Equals("Sticker not found in catalog")) return NotFound(ex.Message);
            return StatusCode(500);
        }
    }

    [HttpGet("{userId}/missing-stickers")]
    public async Task<ActionResult<List<MissingSticker>>> GetMissingStickers(int userId)
    {
        var authenticatedUserId = _authService.GetUserIdFromToken(User);
        if (authenticatedUserId != userId)
            return StatusCode(403, "You can only view your own missing stickers.");

        try
        {
            var missing = await _userService.GetMissingStickers(userId);
            return Ok(missing);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{userId}/missing-stickers/{stickerId}")]
    public async Task<ActionResult> DeleteMissingSticker(int userId, int stickerId)
    {
        var authenticatedUserId = _authService.GetUserIdFromToken(User);
        if (authenticatedUserId != userId)
            return StatusCode(403, "You can only manage your own missing stickers.");

        var deleted = await _userService.RemoveMissingSticker(userId, stickerId);
        if (!deleted) return NotFound("Missing sticker not found.");
        return NoContent();
    }

    [HttpPatch("{userId}/stickers/{stickerId}")]
    public ActionResult<UserSticker> PatchUserSticker(int userId, int stickerId, PatchUserStickerDTO patchDto)
    {
        var authenticatedUserId = _authService.GetUserIdFromToken(User);
        if (authenticatedUserId != userId)
            return Forbid();

        try
        {
            if (patchDto.Quantity < 0)
                return BadRequest("Quantity cannot be negative.");

            if (patchDto.CanBeDirectlyExchanged == null && patchDto.CanBeAuctioned == null && patchDto.Quantity == null)
                return BadRequest("At least one field must be provided for update.");

            var userSticker = _userService.UpdateUserSticker(stickerId, patchDto.CanBeDirectlyExchanged, patchDto.CanBeAuctioned, patchDto.Quantity, authenticatedUserId);
            return Ok(userSticker);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{userId}/stickers/{stickerId}")]
    public ActionResult<UserSticker> DeleteUserSticker(int userId, int stickerId)
    {
        try
        {
            var authenticatedUserId = _authService.GetUserIdFromToken(User);
            _userService.DeleteUserSticker(stickerId, authenticatedUserId);
            return Ok();
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, "You do not own this sticker resource.");
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <remarks>
    /// Public by design: any authenticated user can view another user's rating history
    /// to evaluate reputation before proposing a trade. No ownership check is applied.
    /// </remarks>
    [HttpGet("{userId}/ratings")]
    public ActionResult<List<RatingResponseDTO>> GetUserRatings(int userId, [FromQuery] GetUserRatingsDTO dto)
    {
        try
        {
            var ratings = _userService.GetAllUserRatings(userId, dto.Page, dto.PageSize);
            return Ok(ratings);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [AllowAnonymous]
    [HttpGet("{userId}/reputation")]
    public ActionResult<double> GetUserReputation(int userId)
    {
        try
        {
            var reputation = _userService.GetUserReputation(userId);
            return Ok(reputation);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [AllowAnonymous]
    [HttpGet]
    public ActionResult<UserResponseDTO> GetUserByUsername([FromQuery] string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return BadRequest("The 'username' query parameter is required.");

        var user = _userService.GetUserByUsername(username);
        if (user == null) return NotFound("User not found.");

        return Ok(new UserResponseDTO
        {
            Id = user.Id,
            Username = user.Username,
            Reputation = user.Reputation,
            Banned = user.Banned
        });
    }

    [AllowAnonymous]
    [HttpGet("{id}")]
    public ActionResult<UserResponseDTO> GetUserById(int id)
    {
        var user = _userService.GetUserById(id);
        if (user == null) return NotFound("User not found.");
        return Ok(new UserResponseDTO
        {
            Id = user.Id,
            Username = user.Username,
            Reputation = user.Reputation,
            Banned = user.Banned
        });
    }

    [HttpPatch("{id}")]
    public ActionResult<UserResponseDTO> PatchUser(int id, PatchUserDTO patchDTO)
    {
        var authenticatedUserId = _authService.GetUserIdFromToken(User);
        if (authenticatedUserId != id)
            return StatusCode(403, "You can only modify your own account.");

        try
        {
            if (patchDTO.Username == null && patchDTO.Password == null)
                return BadRequest("At least one field must be provided for update.");

            var user = _userService.UpdateUser(id, patchDTO);
            var response = new UserResponseDTO
            {
                Id = user.Id,
                Username = user.Username,
                Reputation = user.Reputation,
                Banned = user.Banned
            };
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
