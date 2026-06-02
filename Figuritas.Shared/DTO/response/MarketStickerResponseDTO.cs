namespace Figuritas.Shared.DTO.response;

public class MarketStickerResponseDTO
{
    public int UserStickerId { get; set; }
    public int OwnerId { get; set; }
    /// <summary>Catalog sticker ID (Sticker.Id), used to match against auction minimum offer requirements.</summary>
    public int StickerId { get; set; }
    public int StickerNumber { get; set; }
    public string StickerNationalTeam { get; set; } = string.Empty;
    public string StickerTeam { get; set; } = string.Empty;
    public string StickerCategory { get; set; } = string.Empty;
    public string StickerDescription { get; set; } = string.Empty;
    public string StickerImageUrl { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool CanBeDirectlyExchanged { get; set; }
    public bool CanBeAuctioned { get; set; }
    /// <summary>Username of the sticker owner, populated server-side to avoid N+1 on the client.</summary>
    public string OwnerUsername { get; set; } = string.Empty;
}
