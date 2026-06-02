namespace Figuritas.Shared.DTO.response;

public class StickerPreviewDTO
{
    public int UserStickerId { get; set; }
    public int Number { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
