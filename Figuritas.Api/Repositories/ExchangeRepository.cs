using System.Collections.Concurrent;
using Figuritas.Shared.Model;

// Repo for in-memory persistence.
public class ExchangeRepository
{
    private readonly ConcurrentBag<Exchange> exchanges = new();
    private int nextId = 1;

    public List<Exchange> GetAll()
    {
        return exchanges.ToList();
    }

    public void Add(Exchange exchange)
    {
        exchange.Id = Interlocked.Increment(ref nextId) - 1;
        exchanges.Add(exchange);
    }

    public Exchange? GetById(int exchangeId)
    {
        return exchanges.FirstOrDefault(e => e.Id == exchangeId);
    }
}