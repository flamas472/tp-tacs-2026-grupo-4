using System.ComponentModel.DataAnnotations;

namespace Figuritas.Shared.DTO.request;

public class PostRatingRequestDTO
{
    public required int ExchangeId { get; set; }
    public required int TargetUserId { get; set; }

    [Range(1, 5, ErrorMessage = "Stars must be between 1 and 5")]
    public required int Stars { get; set; }

    [MaxLength(500)]
    public string? Comment { get; set; }
}
