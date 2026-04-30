using System.Collections.Concurrent;
using FiguritasApi.Model;

// Repo for in-memory persistence.
public class InventoryFiguritaRepository
{
    private readonly ConcurrentBag<InventoryFigurita> inventoryFiguritas = new();
    private int nextId = 1;

    public List<InventoryFigurita> GetAll(Func<InventoryFigurita, bool> predicate)
    {
        return inventoryFiguritas.Where(predicate).ToList();
    }

    public void Add(InventoryFigurita inventoryFigurita)
    {
        inventoryFigurita.Id = Interlocked.Increment(ref nextId) - 1;
        inventoryFiguritas.Add(inventoryFigurita);
    }
}