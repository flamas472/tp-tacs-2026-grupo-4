using Figuritas.Shared.Model;
using Figuritas.Shared.Utils;
using Figuritas.Shared.DTO;
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


    //* ENDPOINT_US01
    [HttpPost("{userId}/stickers")]
    public ActionResult<UserSticker> PostUserSticker(int userId, PostUserStickerRequestDTO data)
    {
        try
        {
            var userSticker = _userService.CreateUserSticker(userId, data);

            return CreatedAtAction( nameof(PostUserSticker), userSticker );
        }
        catch (ArgumentException ex)
        {
            if(ex.Message.Equals("User not found")) return NotFound(ex.Message);
            if(ex.Message.Equals("Inventory already registered")) return Conflict(ex.Message);
            
            return StatusCode(500);
        }
    }


    //* ENDPOINT_US02
    [HttpPost("{userId}/missing-stickers")]
    public ActionResult<List<Sticker>> PostMissingSticker(int userId, PostMissingStickerRequestDTO data)
    {
        try
        {
            Sticker sticker = data.Sticker.ToDomain();
            var missingSticker = _userService.AddMissingStickerToUser(userId, sticker);

            return CreatedAtAction( nameof(PostMissingSticker), missingSticker );
        }
        catch (ArgumentException ex)
        {
            if(ex.Message.Equals("User not found")) return NotFound(ex.Message);
            if(ex.Message.Equals("Missing sticker already registered")) return Conflict(ex.Message);
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
   

    //* ENDPOINT_US10
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

    // Rutas REST para gestión de usuarios.
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
            var user = _userService.CreateUser(userDTO);
            return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, user);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("/{id}")]
    public ActionResult<User> PatchUser(int userId, PatchUserDTO patchDTO)
    {
        try
        {
            if(patchDTO.Username == null && patchDTO.Password == null)
                return BadRequest("Debe proporcionar al menos un campo para actualizar.");
            
            var user = _userService.UpdateUser(userId, patchDTO);
            return Ok(user);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

}

