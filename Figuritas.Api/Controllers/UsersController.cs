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
    public ActionResult<UserSticker> PostUserSticker(int userId, PostUserStickerRequestDTO data)
    {
        try
        {
            var authenticatedUserId = _authService.GetUserIdFromToken(User);
            if (authenticatedUserId != userId)
                return StatusCode(403, "You can only publish stickers for your own account.");

            var userSticker = _userService.CreateUserSticker(userId, data);
            return CreatedAtAction(nameof(GetUserStickerById), new { userId = userId, stickerId = userSticker.Id }, userSticker);
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Equals("User not found")) return NotFound(ex.Message);
            if (ex.Message.Equals("Inventory already registered")) return Conflict(ex.Message);
            if (ex.Message.Equals("Sticker not found in catalog")) return NotFound(ex.Message);
            return StatusCode(500);
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

    [HttpGet("{userId}/stickers")]
    [Obsolete("Deprecated: use GET /api/dashboard/stickers for authenticated access to own sticker inventory.")]
    public ActionResult<List<UserStickerResponseDTO>> GetUserStickers(int userId)
    {
        var authenticatedUserId = _authService.GetUserIdFromToken(User);
        if (authenticatedUserId != userId)
            return StatusCode(StatusCodes.Status410Gone,
                "This endpoint is deprecated. Use GET /api/dashboard/stickers to access your own sticker inventory.");

        var userStickers = _userService.GetAllUserStickers()
            .FindAll(us => us.UserId == userId)
            .Select(us => new UserStickerResponseDTO
            {
                Id = us.Id,
                UserId = us.UserId,
                Quantity = us.Quantity,
                CanBeDirectlyExchanged = us.CanBeDirectlyExchanged,
                CanBeAuctioned = us.CanBeAuctioned,
                Active = us.Active
            })
            .ToList();
        return Ok(userStickers);
    }

    [HttpPatch("{userId}/stickers/{stickerId}")]
    public ActionResult<UserSticker> PatchUserSticker(int userId, int stickerId, PatchUserStickerDTO patchDto)
    {
        try
        {
            if (patchDto.Quantity < 0)
                return BadRequest("Quantity cannot be negative.");

            if (patchDto.CanBeDirectlyExchanged == null && patchDto.CanBeAuctioned == null && patchDto.Quantity == null)
                return BadRequest("At least one field must be provided for update.");

            var userSticker = _userService.UpdateUserSticker(stickerId, patchDto.CanBeDirectlyExchanged, patchDto.CanBeAuctioned, patchDto.Quantity);
            return Ok(userSticker);
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
            _userService.DeleteUserSticker(stickerId);
            return Ok();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("{userID}/ratings")]
    public ActionResult<List<Rate>> GetUserRatings(int userID)
    {
        try
        {
            var userRatings = _userService.GetAllUserRatings(userID);
            return Ok(userRatings);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

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
    public ActionResult<List<UserResponseDTO>> GetUsers()
    {
        var users = _userService.GetAllUsers();
        var response = users.Select(u => new UserResponseDTO
        {
            Id = u.Id,
            Username = u.Username,
            IsAdmin = u.IsAdmin,
            Reputation = u.Reputation
        }).ToList();
        return Ok(response);
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
            IsAdmin = user.IsAdmin,
            Reputation = user.Reputation
        });
    }

    [AllowAnonymous]
    [HttpPost]
    public ActionResult<UserResponseDTO> PostUser(PostUserDTO userDTO)
    {
        try
        {
            var user = _userService.CreateUser(userDTO);
            var response = new UserResponseDTO
            {
                Id = user.Id,
                Username = user.Username,
                IsAdmin = user.IsAdmin,
                Reputation = user.Reputation
            };
            return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("{id}")]
    public ActionResult<UserResponseDTO> PatchUser(int id, PatchUserDTO patchDTO)
    {
        try
        {
            if (patchDTO.Username == null && patchDTO.Password == null)
                return BadRequest("At least one field must be provided for update.");

            var user = _userService.UpdateUser(id, patchDTO);
            var response = new UserResponseDTO
            {
                Id = user.Id,
                Username = user.Username,
                IsAdmin = user.IsAdmin,
                Reputation = user.Reputation
            };
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
