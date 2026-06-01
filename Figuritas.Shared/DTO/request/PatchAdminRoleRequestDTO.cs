using Figuritas.Shared.Enums;

namespace Figuritas.Shared.DTO.request;

public class PatchAdminRoleRequestDTO
{
    public required UserRole Role { get; set; }
}
