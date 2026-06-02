namespace Figuritas.Shared.DTO.response;

public class ExchangeSuggestionResponseDTO
{
    public int SuggestedUserId { get; set; }
    public string SuggestedUsername { get; set; } = string.Empty;
    public int TheirUserStickerId { get; set; }
    public int StickerId { get; set; }
    public int StickerNumber { get; set; }
    public string StickerNationalTeam { get; set; } = string.Empty;
    public string StickerTeam { get; set; } = string.Empty;
    public string StickerCategory { get; set; } = string.Empty;
    public string StickerDescription { get; set; } = string.Empty;
    public string StickerImageUrl { get; set; } = string.Empty;
    public int TheirQuantity { get; set; }
    public bool IsPerfectMatch { get; set; }
    public List<StickerPreviewDTO> CallerMatchedStickers { get; set; } = new();
}
