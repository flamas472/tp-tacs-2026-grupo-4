using Figuritas.Shared.Model;
using Figuritas.Shared.Utils;
using Figuritas.Shared.DTO;
using Figuritas.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Figuritas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;

    public UsersController(UserService userService)
    {
        _userService = userService;
    }

    [HttpPost("{userId}/stickers")]
    public ActionResult<UserSticker> PostUserSticker(int userId, PostUserStickerRequestDTO data)
    {
        try
        {
            var userSticker = _userService.CreateUserSticker(userId, data);
            return CreatedAtAction(nameof(GetUserStickers), new { userId = userId }, userSticker);
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Equals("User not found")) return NotFound(ex.Message);
            if (ex.Message.Equals("Inventory already registered")) return Conflict(ex.Message);
            return StatusCode(500);
        }
    }

    [HttpPost("{userId}/missing-stickers")]
    public ActionResult<List<Sticker>> PostMissingSticker(int userId, PostMissingStickerRequestDTO data)
    {
        try
        {
            Sticker sticker = data.Sticker.ToDomain();
            var missingSticker = _userService.AddMissingStickerToUser(userId, sticker);
            return CreatedAtAction(nameof(GetUserStickers), new { userId = userId }, missingSticker);
        }
        catch (ArgumentException ex)
        {
            if (ex.Message.Equals("User not found")) return NotFound(ex.Message);
            if (ex.Message.Equals("Missing sticker already registered")) return Conflict(ex.Message);
            return StatusCode(500);
        }
    }

    [HttpGet("{userId}/stickers")]
    public ActionResult<List<UserSticker>> GetUserStickers(int userId)
    {
        var userStickers = _userService.GetAllUserStickers().FindAll(us => us.UserId == userId);
        return Ok(userStickers);
    }

    [HttpPatch("{userId}/stickers/{stickerId}")]
    public ActionResult<UserSticker> PatchUserSticker(int userId, int stickerId, PatchUserStickerDTO patchDto)
    {
        try
        {
            if (patchDto.Quantity < 0)
                return BadRequest("Quantity cannot be negative.");

            if (patchDto.CanBeExchanged == null && patchDto.Quantity == null)
                return BadRequest("At least one field must be provided for update.");

            var userSticker = _userService.UpdateUserSticker(stickerId, patchDto.CanBeExchanged, patchDto.Quantity);
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

    [HttpGet]
    public ActionResult<List<User>> GetUsers()
    {
        var users = _userService.GetAllUsers();
        return Ok(users);
    }

    [HttpGet("{id}")]
    public ActionResult<User> GetUserById(int id)
    {
        var user = _userService.GetUserById(id);
        if (user == null) return NotFound("User not found.");
        return Ok(user);
    }

    [HttpPost]
    public ActionResult<User> PostUser(PostUserDTO userDTO)
    {
        try
        {
            var user = _userService.CreateUser(userDTO);
            return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("{id}")]
    public ActionResult<User> PatchUser(int id, PatchUserDTO patchDTO)
    {
        try
        {
            if (patchDTO.Username == null && patchDTO.Password == null)
                return BadRequest("At least one field must be provided for update.");

            var user = _userService.UpdateUser(id, patchDTO);
            return Ok(user);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
