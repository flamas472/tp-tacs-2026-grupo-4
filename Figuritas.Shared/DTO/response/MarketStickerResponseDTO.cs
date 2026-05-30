namespace Figuritas.Shared.DTO.response;

public class MarketStickerResponseDTO
{
    public int UserStickerId { get; set; }
    public int OwnerId { get; set; }
    public int StickerNumber { get; set; }
    public string StickerNationalTeam { get; set; } = string.Empty;
    public string StickerTeam { get; set; } = string.Empty;
    public string StickerCategory { get; set; } = string.Empty;
    public string StickerDescription { get; set; } = string.Empty;
    public string StickerImageUrl { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool CanBeDirectlyExchanged { get; set; }
    public bool CanBeAuctioned { get; set; }
}
