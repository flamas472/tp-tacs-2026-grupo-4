using System.Collections.Concurrent;
using FiguritasApi.Model;

public class AuctionOfferRepository
{
    private readonly ConcurrentBag<AuctionOffer> offers = new();
    private int nextId = 1;

    public List<AuctionOffer> GetAll() => offers.ToList();

    public void Add(AuctionOffer offer)
    {
        offer.Id = Interlocked.Increment(ref nextId) - 1;
        offers.Add(offer);
    }

    public AuctionOffer? GetById(int id) => offers.FirstOrDefault(o => o.Id == id);
}