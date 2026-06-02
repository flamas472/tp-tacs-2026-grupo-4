namespace Figuritas.Shared.DTO.response;

public class AuctionWatchlistResponseDTO
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int AuctionId { get; set; }
    public DateTime CreatedAt { get; set; }
}
