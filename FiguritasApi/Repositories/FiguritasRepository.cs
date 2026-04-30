using System.Collections.Concurrent;
using FiguritasApi.Model;

// Repo for in-memory persistence.
public class FiguritaRepository
{
    private readonly ConcurrentBag<Figurita> figuritas = new();
    private int nextId = 1;

    public List<Figurita> GetAll()
    {
        return figuritas.ToList();
    }

    public void Add(Figurita figurita)
    {
        figurita.Id = Interlocked.Increment(ref nextId) - 1;
        figuritas.Add(figurita);
    }
}