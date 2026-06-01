namespace Figuritas.Shared.Model.Subastas;

public class AuctionWatchlist
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int AuctionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
