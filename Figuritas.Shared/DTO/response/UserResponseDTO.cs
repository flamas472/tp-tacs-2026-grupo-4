namespace Figuritas.Shared.DTO.response;

public class UserResponseDTO
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public double Reputation { get; set; }
    public bool Banned { get; set; }
}
