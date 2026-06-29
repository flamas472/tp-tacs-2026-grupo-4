using System.ComponentModel.DataAnnotations;

namespace Figuritas.Shared.DTO;

public class PatchUserDTO
{
    public string? Username { get; set; }

    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d).+$",
        ErrorMessage = "Password must contain at least one uppercase letter and one number.")]
    public string? Password { get; set; }
}