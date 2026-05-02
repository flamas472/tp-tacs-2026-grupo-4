namespace FiguritasApi.Shared.Model;

public class AuctionOffer
{
    public int Id { get; set; }

    public required User Bidder { get; set; }

    public required List<UserSticker> Offer { get; set; }

    public required Auction Auction { get; set; }
}
