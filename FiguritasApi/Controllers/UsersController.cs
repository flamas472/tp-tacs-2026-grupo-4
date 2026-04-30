using FiguritasApi.Model;
using FiguritasApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace StickersApi.Controllers;

[ApiController]
[Route("api/[controller]")]

// Endpoints: api/users

// To add an inventory Sticker for a user: POST /api/users/{userId}/inventory
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
    public ActionResult<User> PostUser(User user)
    {
        try
        {
            _userService.CreateUser(user);
            return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, user);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // REST routes for managing user's inventory Stickers.
    [HttpPost("{userId}/inventory")]
    public ActionResult<UserSticker> PostUserSticker(int userId, PostInventoryDto inventoryDto)
    {
        try
        {
            var userSticker = inventoryDto.ToDomain();
            userSticker.UserId = userId;
            _userService.AddUserStickerToUser(userId, userSticker);
            return CreatedAtAction(nameof(GetUsers), new { id = userSticker.Id }, userSticker);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

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

    [HttpPost("{userId}/exchangeProposal")]
    public ActionResult<ExchangeProposal> PostExchangeProposal(PostExchangeProposalDTO exchangeProposalDTO)
    {
        try
        {
            // 1. Validations
            if (exchangeProposalDTO.ProponentUserID == exchangeProposalDTO.ProposedUserID)
                return BadRequest("No podés proponerte un intercambio a vos mismo.");

            if (!exchangeProposalDTO.OfferedStickersID.Any() || !exchangeProposalDTO.RequestedStickersID.Any())
                return BadRequest("Debe haber al menos una figurita ofrecida y una solicitada.");  

             // 2. Search usesr
            var proponent = _userService.GetUserById(exchangeProposalDTO.ProponentUserID);
            var proposed = _userService.GetUserById(exchangeProposalDTO.ProposedUserID);

            if (proponent == null || proposed == null)
                return NotFound("Uno o ambos usuarios no existen.");    

            // 3. Buscar stickers
            var offered = exchangeProposalDTO.OfferedStickersID
                .Select(id => _userService.GetUserStickerById(id))
                .ToList();

            var requested = exchangeProposalDTO.RequestedStickersID
                .Select(id => _userService.GetUserStickerById(id))
                .ToList();

            if (offered.Any(s => s == null) || requested.Any(s => s == null))
                return BadRequest("Algunas figuritas no existen.");

            // 5. Check ownership
            if (offered.Any(s => s!.UserId != proponent.Id))
                return BadRequest("Estás ofreciendo figuritas que no son tuyas.");

            if (requested.Any(s => s!.UserId != proposed.Id))
                return BadRequest("Estás solicitando figuritas que el otro usuario no tiene.");

            // 6. Create entity
            var proposal = new ExchangeProposal
            {
                Id = 0,
                Proponent = proponent,
                Proposed = proposed,
                OfferedStickers = offered,
                RequestedStickers = requested,
                State = ExchangeProposalState.Pending
            };

            // 7. Persist entity
            _userService.CreateExchangeProposal(proposal);

            // 8. Response
            // ToDo
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }
}

public class PostInventoryDto
{
    public required Sticker Sticker { get; set; }
    public bool CanBeExchanged { get; set; } // true for exchange, false for auction
    public bool Active { get; set; }

    public UserSticker ToDomain()
    {
        return new UserSticker {
            Id = 0, // ID assigned automatically
            Sticker = this.Sticker,
            CanBeExchanged = this.CanBeExchanged,
            Active = this.Active,
            Quantity = 1 // default
        };
    }
}

public class PostMissingDto
{
    public required Sticker Sticker { get; set; }

    public Sticker ToDomain()
    {
        return new Sticker {
            Id = 0, // ID assigned automatically
            NationalTeam = this.Sticker.NationalTeam,
            Team = this.Sticker.Team,
            Category = this.Sticker.Category,
            Number = this.Sticker.Number
        };
    }
}

public class PostExchangeProposalDTO
{
    public required List<int> OfferedStickersID { get; set; } // IDs de UserSticker

    public required List<int> RequestedStickersID { get; set; } // IDs de UserSticker

    public required int ProponentUserID {get; set; }

    public required int ProposedUserID {get; set; }
}
