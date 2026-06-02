namespace Figuritas.Shared.Model;

public class UserSticker
{
    public int Id { get; set; }

    public required Sticker Sticker { get; set; }

    public int UserId { get; set; }

    public bool CanBeDirectlyExchanged { get; set; }

    public bool CanBeAuctioned { get; set; }

    public bool Active { get; set; } = true;

    public int Quantity { get; set; }
}
