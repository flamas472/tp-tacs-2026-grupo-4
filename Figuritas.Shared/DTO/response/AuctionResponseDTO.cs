namespace Figuritas.Shared.DTO.response;

public class AuctionResponseDTO
{
    public int Id { get; set; }
    public int AuctioneerId { get; set; }
    public int UserStickerId { get; set; }
    public List<int> MinimumOfferStickerIds { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime EndsAt { get; set; }
    public int? BestCurrentOfferId { get; set; }
    public int? UserSelectedBestOfferId { get; set; }

    // Sticker info (populated by server to avoid N+1 on the client)
    public int StickerNumber { get; set; }
    public string StickerDescription { get; set; } = string.Empty;
    public string StickerNationalTeam { get; set; } = string.Empty;
    public string StickerTeam { get; set; } = string.Empty;
    public string StickerImageUrl { get; set; } = string.Empty;
    public string AuctioneerUsername { get; set; } = string.Empty;

    /// <summary>Total number of offers/bids received by this auction.</summary>
    public int OfferCount { get; set; }
}
