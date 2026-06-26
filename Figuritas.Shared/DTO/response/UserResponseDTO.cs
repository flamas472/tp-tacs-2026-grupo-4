using System.Text.Json.Serialization;
using Figuritas.Shared.Enums;

namespace Figuritas.Shared.DTO.response;

public class UserResponseDTO
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public double Reputation { get; set; }
    public bool Banned { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>
    /// Rol del usuario. Solo lo populan los endpoints del área de administración;
    /// los endpoints públicos lo dejan en null.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UserRole? Role { get; set; }
}
