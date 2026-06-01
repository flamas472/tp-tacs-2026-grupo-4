using Figuritas.Shared.Model;
using Figuritas.Shared.Model.Subastas;
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
        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        // Index: AuctionId — transforms GetByAuctionIdAsync from COLLSCAN to IXSCAN.
        _offers.Indexes.CreateOne(new CreateIndexModel<AuctionOffer>(
            Builders<AuctionOffer>.IndexKeys.Ascending(o => o.AuctionId),
            new CreateIndexOptions { Name = "idx_auctionoffer_auction_id" }));

        // Compound index: AuctionId + BidderId + Status — covers the active-bid lookup per bidder.
        _offers.Indexes.CreateOne(new CreateIndexModel<AuctionOffer>(
            Builders<AuctionOffer>.IndexKeys
                .Ascending(o => o.AuctionId)
                .Ascending(o => o.BidderId)
                .Ascending(o => o.Status),
            new CreateIndexOptions { Name = "idx_auctionoffer_auction_bidder_status" }));
    }

    public List<AuctionOffer> GetAll() => _offers.Find(_ => true).ToList();

    public void Add(AuctionOffer offer)
    {
        offer.Id = _idGenerator.GetNextId<AuctionOffer>();
        _offers.InsertOne(offer);
    }

    public AuctionOffer? GetById(int id) => _offers.Find(o => o.Id == id).FirstOrDefault();

    public void Update(AuctionOffer offer)
    {
        var result = _offers.ReplaceOne(o => o.Id == offer.Id, offer);
        if (!result.IsAcknowledged || result.MatchedCount == 0)
            throw new ArgumentException("AuctionOffer not found");
    }

    public async Task<List<AuctionOffer>> GetByAuctionIdAsync(int auctionId)
    {
        var filter = Builders<AuctionOffer>.Filter.Eq(o => o.AuctionId, auctionId);
        return await _offers.Find(filter).ToListAsync();
    }

    public async Task<AuctionOffer?> GetActiveBidderOfferAsync(int auctionId, int bidderId)
    {
        var filter = Builders<AuctionOffer>.Filter.And(
            Builders<AuctionOffer>.Filter.Eq(o => o.AuctionId, auctionId),
            Builders<AuctionOffer>.Filter.Eq(o => o.BidderId, bidderId),
            Builders<AuctionOffer>.Filter.Eq(o => o.Status, AuctionOfferStatus.Active)
        );
        return await _offers.Find(filter).FirstOrDefaultAsync();
    }
}
