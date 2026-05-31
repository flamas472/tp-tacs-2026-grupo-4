namespace Figuritas.Shared.DTO.response;

public class MyPublishedStickerResponseDTO
{
    public int StickerId { get; set; }
    public int Number { get; set; }
    public string Team { get; set; } = string.Empty;
    public string Player { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool CanBeDirectlyExchanged { get; set; }
    public bool CanBeAuctioned { get; set; }
}
