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

        // Compound index: AuctionId + State — accelerates the server-side filter in
        // GetByAuctionIdAsync (Pending offers only) and closure bulk updates.
        _offers.Indexes.CreateOne(new CreateIndexModel<AuctionOffer>(
            Builders<AuctionOffer>.IndexKeys
                .Ascending(o => o.AuctionId)
                .Ascending(o => o.State),
            new CreateIndexOptions { Name = "idx_auctionoffer_auction_state" }));

        // Index: BidderId — accelerates GetByBidderIdAsync (my bids dashboard).
        _offers.Indexes.CreateOne(new CreateIndexModel<AuctionOffer>(
            Builders<AuctionOffer>.IndexKeys.Ascending(o => o.BidderId),
            new CreateIndexOptions { Name = "idx_auctionoffer_bidder_id" }));
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

    /// <inheritdoc/>
    public async Task<List<AuctionOffer>> GetByAuctionIdAsync(int auctionId)
    {
        // Server-side filter: only Pending offers. Backed by idx_auctionoffer_auction_state.
        var filter = Builders<AuctionOffer>.Filter.And(
            Builders<AuctionOffer>.Filter.Eq(o => o.AuctionId, auctionId),
            Builders<AuctionOffer>.Filter.Eq(o => o.State, AuctionOfferState.Pending)
        );
        return await _offers.Find(filter).ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<List<AuctionOffer>> GetAllByAuctionIdAsync(int auctionId)
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

    /// <inheritdoc/>
    public async Task<List<AuctionOffer>> GetByBidderIdAsync(int bidderId, int page, int pageSize)
    {
        var filter = Builders<AuctionOffer>.Filter.Eq(o => o.BidderId, bidderId);
        return await _offers.Find(filter)
            .SortByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<Dictionary<int, int>> CountByAuctionIdsAsync(List<int> auctionIds)
    {
        if (auctionIds.Count == 0)
            return new Dictionary<int, int>();

        var filter = Builders<AuctionOffer>.Filter.In(o => o.AuctionId, auctionIds);
        var offers = await _offers.Find(filter)
            .Project(o => new { o.AuctionId })
            .ToListAsync();

        return offers
            .GroupBy(o => o.AuctionId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <inheritdoc/>
    public async Task<bool> TryCancelOfferAtomicallyAsync(int offerId)
    {
        // Guard: only transition Pending → Cancelled.
        // If the offer is already in Won, Lost, or Cancelled state, ModifiedCount == 0
        // and the caller should abort without releasing stock.
        var filter = Builders<AuctionOffer>.Filter.And(
            Builders<AuctionOffer>.Filter.Eq(o => o.Id, offerId),
            Builders<AuctionOffer>.Filter.Eq(o => o.State, AuctionOfferState.Pending)
        );
        var update = Builders<AuctionOffer>.Update.Set(o => o.State, AuctionOfferState.Cancelled);
        var result = await _offers.UpdateOneAsync(filter, update);
        return result.ModifiedCount == 1;
    }

    /// <inheritdoc/>
    public async Task<bool> AppendOfferedStickersAsync(int offerId, List<int> additionalStickerIds)
    {
        // Guard: only append while the offer is still Pending.
        // If the auction closed concurrently, ModifiedCount == 0 and the caller aborts
        // without reserving stock — no inconsistency (Escenario D).
        // Using $push/$each guarantees that two concurrent PATCH requests both land in
        // MongoDB sequentially without either overwriting the other (Escenario B).
        var filter = Builders<AuctionOffer>.Filter.And(
            Builders<AuctionOffer>.Filter.Eq(o => o.Id, offerId),
            Builders<AuctionOffer>.Filter.Eq(o => o.State, AuctionOfferState.Pending)
        );
        var update = Builders<AuctionOffer>.Update.PushEach(o => o.OfferedUserStickerIds, additionalStickerIds);
        var result = await _offers.UpdateOneAsync(filter, update);
        return result.ModifiedCount == 1;
    }
}
