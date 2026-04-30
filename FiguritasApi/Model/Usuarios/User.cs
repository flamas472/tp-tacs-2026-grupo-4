namespace FiguritasApi.Model;
public class User
{
    public int Id { get; set; }

    public required string Username { get; set; }

    public List<InventoryFigurita> InventoryFiguritas { get; set; } = new();

    public List<Figurita> MissingFiguritas { get; set; } = new();

    public void AddInventoryFigurita(InventoryFigurita figurita)
    {
        InventoryFiguritas.Add(figurita);
    }

    public void AddMissingFigurita(Figurita figurita)
    {
        MissingFiguritas.Add(figurita);
    }
}
