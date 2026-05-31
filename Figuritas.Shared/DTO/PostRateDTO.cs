using Figuritas.Shared.Model;

namespace Figuritas.Shared.DTO;

public class PostRateDTO
{
    public required int Score { get; set; }

    public string? Comment { get; set; }
}