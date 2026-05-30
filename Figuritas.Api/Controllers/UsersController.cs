using Figuritas.Shared.Model;
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

    [HttpPost("{userId}/ratings")]
    public ActionResult<Rate> PostUserRating(int userId, [FromQuery] int exchangeId, PostRateDTO data)
    {
        var rate = _userService.CreateUserRate(exchangeId, data, userId);
        return Ok(rate);
    }

    [HttpGet("{userId}/missing-stickers")]
    public ActionResult<List<Sticker>> GetMissingStickers(int userId)
    {
        var missing = _userService.GetMissingStickers(userId);
        return Ok(missing);
    }

    [HttpDelete("{userId}/missing-stickers/{stickerId}")]
    public ActionResult DeleteMissingSticker(int userId, int stickerId)
    {
        _userService.RemoveMissingSticker(userId, stickerId);
        return NoContent();
    }

    [HttpPost("{userId}/stickers")]
    public ActionResult<UserSticker> PostUserSticker(int userId, PostUserStickerRequestDTO data)
    {
        var userSticker = _userService.CreateUserSticker(userId, data);
        return CreatedAtAction(nameof(PostUserSticker), userSticker);
    }

    [HttpPost("{userId}/missing-stickers")]
    public ActionResult<List<Sticker>> PostMissingSticker(int userId, PostMissingStickerRequestDTO data)
    {
        var missingSticker = _userService.AddMissingStickerToUser(userId, data);
        return CreatedAtAction(nameof(GetMissingStickers), new { userId = userId }, missingSticker);
    }
    
    [HttpGet("{userId}/stickers")]
    public ActionResult<List<UserStickerResponseDTO>> GetUserStickers(int userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var userStickers = _userService.GetUserStickersPaginated(userId, page, pageSize);
        return Ok(userStickers);
    }

    [HttpPatch("{userId}/stickers/{userStickerId}")]
    public ActionResult<UserSticker> PatchUserSticker(int userId, int userStickerId, [FromQuery] PublicationType? mode, [FromQuery] int? quantity)
    {
        var userSticker = _userService.UpdateUserSticker(userStickerId, mode, quantity);
        return Ok(userSticker);
    }

    [HttpDelete("{userId}/stickers/{userStickerId}")]
    public ActionResult DeleteUserSticker(int userId, int userStickerId)
    {
        _userService.DeleteUserSticker(userStickerId);
        return Ok();
    }
   
    [HttpGet("{userId}/ratings")]
    public ActionResult<List<Rate>> GetUserRatings(int userId)
    {
        var userRatings = _userService.GetAllUserRatings(userId);
        return Ok(userRatings);
    }

    [HttpGet("{userId}/reputation")]
    public ActionResult<double> GetUserReputation(int userId)
    {
        var reputation = _userService.GetUserReputation(userId);
        return Ok(reputation);
    }

    [HttpGet]
    public ActionResult<List<User>> GetUsers()
    {
        var users = _userService.GetAllUsers();
        return Ok(users);
    }

    [HttpPost]
    public ActionResult<User> PostUser(PostUserDTO userDTO)
    {
        var user = _userService.CreateUser(userDTO);
        return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, user);
    }

    [HttpPatch("{id}")] 
    public ActionResult<User> PatchUser(int id, PatchUserDTO patchDTO)
    {
        var user = _userService.UpdateUser(id, patchDTO);
        return Ok(user);
    }
}