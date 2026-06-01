using System.Text.Json.Serialization;
using Figuritas.Shared.Enums;

namespace Figuritas.Shared.DTO.response;

public class UserResponseDTO
{
    public int Id { get; set; }
    public required string Username { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UserRole Role { get; set; }

    public double Reputation { get; set; }
}
