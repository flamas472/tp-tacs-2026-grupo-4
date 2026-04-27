using Microsoft.AspNetCore.Mvc;
using FiguritasApi.Model;
using FiguritasApi.Services;

namespace FiguritasApi.Controllers;

[ApiController]
[Route("api/[controller]")]

// Endpoints: api/users

// To add an inventory figurita for a user: POST /api/users/{userId}/inventory
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

    // REST routes for managing user's inventory figuritas.
    [HttpPost("{userId}/inventory")]
    public ActionResult<InventoryFigurita> PostInventoryFigurita(int userId, PostInventoryDto inventoryDto)
    {
        try
        {
            var inventoryFigurita = inventoryDto.ToDomain();
            inventoryFigurita.UserId = userId;
            _userService.AddInventoryFiguritaToUser(userId, inventoryFigurita);
            return CreatedAtAction(nameof(GetUsers), new { id = inventoryFigurita.Id }, inventoryFigurita);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("{userId}/missing")]
    public ActionResult<List<Figurita>> PostMissingFigurita(int userId, PostMissingDto missingDto)
    {
        try
        {
            var missingFigurita = missingDto.ToDomain();
            _userService.AddMissingFiguritaToUser(userId, missingFigurita);
            var user = _userService.GetUserById(userId);
            return CreatedAtAction(nameof(GetUsers), new { id = missingFigurita.Id }, user?.MissingFiguritas);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
    }
}

public class PostInventoryDto
{
    public required Figurita Figurita { get; set; }
    public bool CanBeExchanged { get; set; } // true for exchange, false for auction
    public bool Active { get; set; }

    public InventoryFigurita ToDomain()
    {
        return new InventoryFigurita {
            Id = 0, // ID assigned automatically
            Figurita = this.Figurita,
            CanBeExchanged = this.CanBeExchanged,
            Active = this.Active,
            Quantity = 1 // default
        };
    }
}

public class PostMissingDto
{
    public required Figurita Figurita { get; set; }

    public Figurita ToDomain()
    {
        return new Figurita {
            Id = 0, // ID assigned automatically
            Selection = this.Figurita.Selection,
            Team = this.Figurita.Team,
            Category = this.Figurita.Category,
            Number = this.Figurita.Number
        };
    }
}
