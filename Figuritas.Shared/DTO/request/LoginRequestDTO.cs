namespace Figuritas.Shared.DTO.request;

/// <summary>
/// DTO used exclusively for the login endpoint. Does not enforce password complexity
/// rules so that existing users with legacy passwords are not locked out.
/// </summary>
public class LoginRequestDTO
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}
