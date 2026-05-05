namespace Figuritas.Shared.DTO.request;

using System.Collections.Generic;

public class PostAuctionOfferDTO
{
    public int BidderId { get; set; }
    public List<int> UserStickerIds { get; set; } = new();
}
