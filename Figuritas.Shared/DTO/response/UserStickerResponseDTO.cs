namespace Figuritas.Shared.DTO.response;

public class UserStickerResponseDTO
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public bool CanBeDirectlyExchanged { get; set; }
    public bool CanBeAuctioned { get; set; }
    public bool Active { get; set; }
    public int Quantity { get; set; }
}
