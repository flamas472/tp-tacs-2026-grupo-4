namespace Figuritas.Shared.DTO.response;

public class ExchangeProposalResponseDTO
{
    public int Id { get; set; }
    public int ProponentUserId { get; set; }
    public int ProposedUserId { get; set; }
    public int RequestedUserStickerId { get; set; }
    public List<int> OfferedUserStickerIds { get; set; } = new();
    public string State { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Enriched sticker info (populated by server)
    public StickerPreviewDTO? RequestedSticker { get; set; }
    public List<StickerPreviewDTO> OfferedStickers { get; set; } = new();
    public string ProponentUsername { get; set; } = string.Empty;
    public string ProposedUsername { get; set; } = string.Empty;
}
