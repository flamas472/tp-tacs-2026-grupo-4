namespace Figuritas.Shared.DTO.request;

using System;
using System.Collections.Generic;

public class PostAuctionDTO
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<int> MinimumOfferStickerIds { get; set; } = new();
    public int AuctionedStickerId { get; set; }
}
