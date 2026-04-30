namespace FiguritasApi.Model;
public class UserSticker
{
    public int Id { get; set; }

    public required Sticker Sticker { get; set; }

    public int UserId { get; set; }

    public bool CanBeExchanged { get; set; } // true for exchange, false for auction

    public bool Active { get; set; }

    public int Quantity { get; set; }
}
