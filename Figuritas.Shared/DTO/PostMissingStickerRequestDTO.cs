using System.ComponentModel.DataAnnotations;

namespace Figuritas.Shared.DTO;

public class PostMissingStickerRequestDTO
{
    [Range(1, int.MaxValue)]
    public required int StickerId { get; set; }
}
