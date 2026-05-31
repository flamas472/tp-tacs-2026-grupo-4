using Figuritas.Shared.Model;
using MongoDB.Driver;

namespace Figuritas.Api.Repositories;

public class AuctionOfferRepository : IAuctionOfferRepository
{
    private readonly IMongoCollection<AuctionOffer> _offers;
    private readonly IIdGenerator _idGenerator;

    public AuctionOfferRepository(MongoDbContext context, IIdGenerator idGenerator)
    {
        _offers = context.Collection<AuctionOffer>("AuctionOffers");
        _idGenerator = idGenerator;
    }

    public List<AuctionOffer> GetAll() => _offers.Find(_ => true).ToList();

    public void Add(AuctionOffer offer)
    {
        offer.Id = _idGenerator.GetNextId<AuctionOffer>();
        _offers.InsertOne(offer);
    }

    public AuctionOffer? GetById(int id) => _offers.Find(o => o.Id == id).FirstOrDefault();

    public async Task<List<AuctionOffer>> GetByAuctionIdAsync(int auctionId)
    {
        var filter = Builders<AuctionOffer>.Filter.Eq(o => o.AuctionId, auctionId);
        return await _offers.Find(filter).ToListAsync();
    }
}
