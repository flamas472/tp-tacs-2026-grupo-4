using System.ComponentModel.DataAnnotations;

namespace Figuritas.Shared.DTO.request;

public class CreateAdminRequestDTO
{
    public required string Username { get; set; }

    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).+$",
        ErrorMessage = "Password must contain at least one uppercase letter and one number.")]
    public required string Password { get; set; }
}
