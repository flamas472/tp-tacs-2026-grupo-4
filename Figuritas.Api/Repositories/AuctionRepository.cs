using Figuritas.Shared.Model;
using Figuritas.Shared.Model.Subastas;
using MongoDB.Driver;

namespace Figuritas.Api.Repositories;

public class AuctionRepository : IAuctionRepository
{
    private readonly IMongoCollection<Auction> _auctions;
    private readonly IIdGenerator _idGenerator;

    public AuctionRepository(MongoDbContext context, IIdGenerator idGenerator)
    {
        _auctions = context.Collection<Auction>("Auctions");
        _idGenerator = idGenerator;
        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        // Index: Status — accelerates worker queries for active auctions and analytics filtering.
        // No explicit name: lets MongoDB use the auto-generated name "Status_1" which may
        // already exist from AnalyticsRepository. Omitting the name guarantees idempotency
        // because MongoDB matches by key spec when no name is provided.
        _auctions.Indexes.CreateOne(new CreateIndexModel<Auction>(
            Builders<Auction>.IndexKeys.Ascending(a => a.Status)));

        // Index: EndsAt — accelerates worker range queries for auctions ending soon / expired.
        // Explicit name avoids collision with any future index on this field.
        _auctions.Indexes.CreateOne(new CreateIndexModel<Auction>(
            Builders<Auction>.IndexKeys.Ascending(a => a.EndsAt),
            new CreateIndexOptions { Name = "idx_auction_ends_at" }));

        // Compound index: Status + EndsAt — covers the most frequent worker filter:
        // Status == Active AND EndsAt < threshold (or EndsAt < now for expired auctions).
        _auctions.Indexes.CreateOne(new CreateIndexModel<Auction>(
            Builders<Auction>.IndexKeys
                .Ascending(a => a.Status)
                .Ascending(a => a.EndsAt),
            new CreateIndexOptions { Name = "idx_auction_status_ends_at" }));
    }

    public List<Auction> GetAll(int page = 1, int pageSize = 20, int? excludeAuctioneerId = null)
    {
        var filter = excludeAuctioneerId.HasValue
            ? Builders<Auction>.Filter.Ne(a => a.AuctioneerId, excludeAuctioneerId.Value)
            : Builders<Auction>.Filter.Empty;

        return _auctions.Find(filter)
            .SortByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToList();
    }

    public void Add(Auction auction)
    {
        auction.Id = _idGenerator.GetNextId<Auction>();
        _auctions.InsertOne(auction);
    }

    public Auction? GetById(int id) => _auctions.Find(a => a.Id == id).FirstOrDefault();

    public void Update(Auction auction)
    {
        var result = _auctions.ReplaceOne(a => a.Id == auction.Id, auction);
        if (!result.IsAcknowledged || result.MatchedCount == 0)
            throw new ArgumentException("Auction not found");
    }

    public List<Auction> GetByAuctioneerId(int auctioneerId, string? status, int page, int pageSize)
    {
        var filter = Builders<Auction>.Filter.Eq(a => a.AuctioneerId, auctioneerId);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<AuctionStatus>(status, ignoreCase: true, out var parsedStatus))
            filter = Builders<Auction>.Filter.And(filter, Builders<Auction>.Filter.Eq(a => a.Status, parsedStatus));

        return _auctions.Find(filter)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToList();
    }

    public async Task<List<Auction>> GetActiveAuctionsEndingBeforeAsync(DateTime threshold)
    {
        var filter = Builders<Auction>.Filter.And(
            Builders<Auction>.Filter.Eq(a => a.Status, AuctionStatus.Active),
            Builders<Auction>.Filter.Lte(a => a.EndsAt, threshold),
            Builders<Auction>.Filter.Gt(a => a.EndsAt, DateTime.UtcNow),
            Builders<Auction>.Filter.Eq(a => a.AuctionEndingNotificationSent, false)
        );
        return await _auctions.Find(filter).ToListAsync();
    }

    public async Task<bool> TryClaimEndingNotificationAsync(int auctionId)
    {
        var filter = Builders<Auction>.Filter.And(
            Builders<Auction>.Filter.Eq(a => a.Id, auctionId),
            Builders<Auction>.Filter.Eq(a => a.AuctionEndingNotificationSent, false)
        );
        var update = Builders<Auction>.Update.Set(a => a.AuctionEndingNotificationSent, true);
        var result = await _auctions.UpdateOneAsync(filter, update);
        return result.ModifiedCount == 1;
    }

    public async Task<List<Auction>> GetExpiredActiveAuctionsAsync(DateTime now)
    {
        var filter = Builders<Auction>.Filter.And(
            Builders<Auction>.Filter.Eq(a => a.Status, AuctionStatus.Active),
            Builders<Auction>.Filter.Lt(a => a.EndsAt, now)
        );
        return await _auctions.Find(filter).ToListAsync();
    }

    public async Task<bool> TryClaimAutomaticClosureAsync(int auctionId)
    {
        // DEUDA TÉCNICA [C-05]
        // Claim huérfano posible.
        // Si el worker finaliza entre el claim y el cierre efectivo,
        // la subasta puede quedar Active con AutoClosureClaimedAt seteado.
        // Futuro endurecimiento:
        // permitir re-claim cuando AutoClosureClaimedAt supere un TTL.

        // Atomically set AutoClosureClaimedAt only if it is currently null.
        // MongoDB FindOneAndUpdate guarantees that exactly one process wins the race.
        var filter = Builders<Auction>.Filter.And(
            Builders<Auction>.Filter.Eq(a => a.Id, auctionId),
            Builders<Auction>.Filter.Eq(a => a.AutoClosureClaimedAt, (DateTime?)null)
        );
        var update = Builders<Auction>.Update.Set(a => a.AutoClosureClaimedAt, DateTime.UtcNow);
        var result = await _auctions.UpdateOneAsync(filter, update);
        return result.ModifiedCount == 1;
    }

    /// <inheritdoc/>
    public async Task<bool> TryCloseAuctionAtomicallyAsync(int auctionId)
    {
        // Guard: only transition from Active → Closed.
        // If another process (e.g., the expiration worker) already closed this auction,
        // ModifiedCount == 0 and the caller should abort gracefully.
        var filter = Builders<Auction>.Filter.And(
            Builders<Auction>.Filter.Eq(a => a.Id, auctionId),
            Builders<Auction>.Filter.Eq(a => a.Status, AuctionStatus.Active)
        );
        var update = Builders<Auction>.Update.Set(a => a.Status, AuctionStatus.Closed);
        var result = await _auctions.UpdateOneAsync(filter, update);
        return result.ModifiedCount == 1;
    }

    /// <inheritdoc/>
    public async Task<bool> TryCancelAuctionAtomicallyAsync(int auctionId)
    {
        var filter = Builders<Auction>.Filter.And(
            Builders<Auction>.Filter.Eq(a => a.Id, auctionId),
            Builders<Auction>.Filter.Eq(a => a.Status, AuctionStatus.Active)
        );
        var update = Builders<Auction>.Update.Set(a => a.Status, AuctionStatus.Cancelled);
        var result = await _auctions.UpdateOneAsync(filter, update);
        return result.ModifiedCount == 1;
    }

    /// <inheritdoc/>
    public async Task<bool> TrySetUserSelectedBestOfferAsync(int auctionId, int offerId)
    {
        // Partial $set update — only writes UserSelectedBestOfferId, never touches Status or
        // any other field.  The Status == Active guard ensures we do not overwrite a document
        // that a concurrent worker has already moved to Closed/Cancelled.
        var filter = Builders<Auction>.Filter.And(
            Builders<Auction>.Filter.Eq(a => a.Id, auctionId),
            Builders<Auction>.Filter.Eq(a => a.Status, AuctionStatus.Active)
        );
        var update = Builders<Auction>.Update.Set(a => a.UserSelectedBestOfferId, offerId);
        var result = await _auctions.UpdateOneAsync(filter, update);
        return result.ModifiedCount == 1;
    }
}