using System.Collections.Concurrent;
using FiguritasApi.Shared.Model;

public class AuctionRepository
{
    private readonly ConcurrentBag<Auction> auctions = new();
    private int nextId = 1;

    public List<Auction> GetAll() => auctions.ToList();

    public void Add(Auction auction)
    {
        auction.Id = Interlocked.Increment(ref nextId) - 1;
        auctions.Add(auction);
    }

    public Auction? GetById(int id) => auctions.FirstOrDefault(a => a.Id == id);
}