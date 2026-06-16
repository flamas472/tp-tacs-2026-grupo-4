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

    // Rating info: populated server-side for accepted proposals.
    // HasRated is true when the calling user already submitted a rating for this exchange.
    // RatingStars and RatingComment hold the values of that existing rating (read-only display).
    public bool HasRated { get; set; }
    public int? RatingStars { get; set; }
    public string? RatingComment { get; set; }
}
