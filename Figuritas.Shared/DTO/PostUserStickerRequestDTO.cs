using System.ComponentModel.DataAnnotations;

namespace Figuritas.Shared.DTO.request;

public class PostUserStickerRequestDTO
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Sticker ID must be greater than 0.")]
    public required int StickerId { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than 0.")]
    public required int Quantity { get; set; }

    public required bool CanBeDirectlyExchanged { get; set; }

    public required bool CanBeAuctioned { get; set; }
}
