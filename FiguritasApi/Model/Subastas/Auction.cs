namespace FiguritasApi.Model;

public class Auction
{
    public int Id { get; set; }

    public required User Auctioneer { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public required List<Figurita> MinimumOffer { get; set; }

    public required Figurita AuctionedFigurita { get; set; }

    public AuctionOffer? BestCurrentOffer { get; set; }
}
