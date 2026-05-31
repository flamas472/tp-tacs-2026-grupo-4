namespace Figuritas.Shared.DTO;

public class PatchUserStickerDTO
{
    public bool? CanBeDirectlyExchanged { get; set; }
    public bool? CanBeAuctioned { get; set; }
    public int? Quantity { get; set; }
}
