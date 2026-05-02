using Figuritas.Shared.Model;
using Figuritas.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Figuritas.Api.Controllers;

[ApiController]
[Route("api/[controller]")]

// Endpoints: api/users
public class UsersController : ControllerBase
{

    private readonly UserService _userService;


    public UsersController(UserService userService)
    {
        _userService = userService;
    }


    // REST routes for managing users.
    [HttpGet]
    public ActionResult<List<User>> GetUsers()
    {
        var users = _userService.GetAllUsers();
        return Ok(users);
    }

    [HttpPost]
    public ActionResult<User> PostUser(PostUserDTO userDTO)
    {
        try
        {
            var user = _userService.CreateUser(userDTO.Username, userDTO.Password);
            return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, user);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // REST routes for managing user's Stickers.
    [HttpPost("{userId}/stickers")]
    public ActionResult<UserSticker> PostUserSticker(int userId, PostUserStickerDto inventoryDto)
    {
        try
        {
            var userSticker = _userService.CreateUserSticker(userId, inventoryDto.stickerID, inventoryDto.canBeExchanged);
            return Ok();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("{userId}/stickers")]
    public ActionResult<List<UserSticker>> GetUserStickers(int userId)
    {
        var userStickers = _userService.GetAllUserStickers().FindAll(us => us.UserId == userId);
        return Ok(userStickers);
    }

    [HttpPatch("{userId}/stickers/{stickerId}")]
    public ActionResult<UserSticker> PatchUserSticker(int userId, int userStickerId, PatchUserStickerDto patchDto)
    {
        try
        {
            if(patchDto.quantity < 0)
                return BadRequest("La cantidad ingresada no puede ser negativa.");

            if(patchDto.canBeExchanged == null && patchDto.quantity == null)
                return BadRequest("Debe proporcionar al menos un campo para actualizar.");
            
            var userSticker = _userService.UpdateUserSticker(userStickerId, patchDto.canBeExchanged, patchDto.quantity);
            return Ok(userSticker);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{userId}/stickers/{stickerId}")]
    public ActionResult<UserSticker> DeleteUserSticker(int userId, int userStickerId)
    {
        try
        {
            _userService.DeleteUserSticker(userStickerId);
            return Ok();
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /* TODO: Analizar cómo habría que modelar los missing stickers.
    [HttpPost("{userId}/missing")]
    public ActionResult<List<Sticker>> PostMissingSticker(int userId, PostMissingDto missingDto)
    {
        try
        {
            var missingSticker = missingDto.ToDomain();
            _userService.AddMissingStickerToUser(userId, missingSticker);
            var user = _userService.GetUserById(userId);
            return CreatedAtAction(nameof(GetUsers), new { id = missingSticker.Id }, user?.MissingStickers);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }
    */
}

public class PostUserStickerDto
{
    public required int stickerID { get; set; }

    public required bool canBeExchanged { get; set; }
}

public class PostUserDTO
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}

public class PatchUserStickerDto
{
    public bool? canBeExchanged { get; set; }

    public int? quantity { get; set; }
}