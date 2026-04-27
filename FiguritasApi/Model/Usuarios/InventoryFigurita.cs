namespace FiguritasApi.Model;
public class InventoryFigurita
{
    public int Id { get; set; }

    public required Figurita Figurita { get; set; }

    public int UserId { get; set; }

    public bool CanBeExchanged { get; set; } // true for exchange, false for auction

    public bool Active { get; set; }

    public int Quantity { get; set; }
}
