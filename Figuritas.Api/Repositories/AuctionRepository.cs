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

    public List<Auction> GetAll(int page = 1, int pageSize = 20, int? excludeAuctioneerId = null, string? status = null)
    {
        FilterDefinition<Auction> filter;

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<AuctionStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            // Caller requested a specific status: filter by it.
            filter = Builders<Auction>.Filter.Eq(a => a.Status, parsedStatus);
        }
        else
        {
            // Default marketplace behavior: only Active auctions are browsable.
            filter = Builders<Auction>.Filter.Eq(a => a.Status, AuctionStatus.Active);
        }

        if (excludeAuctioneerId.HasValue)
            filter = Builders<Auction>.Filter.And(
                filter,
                Builders<Auction>.Filter.Ne(a => a.AuctioneerId, excludeAuctioneerId.Value));

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
        // Allow re-claim when AutoClosureClaimedAt was set more than 5 minutes ago.
        // This unblocks auctions whose original claiming worker process died before completing the
        // closure (orphaned claim), which would otherwise leave the auction stuck in Active forever.
        var staleClaimThreshold = DateTime.UtcNow.AddMinutes(-5);

        var filter = Builders<Auction>.Filter.And(
            Builders<Auction>.Filter.Eq(a => a.Id, auctionId),
            Builders<Auction>.Filter.Or(
                Builders<Auction>.Filter.Eq(a => a.AutoClosureClaimedAt, (DateTime?)null),
                Builders<Auction>.Filter.Lt(a => a.AutoClosureClaimedAt, staleClaimThreshold)
            )
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
        return result.MatchedCount == 1;
    }

    /// <inheritdoc/>
    public async Task SetBestCurrentOfferIdAsync(int auctionId, int? bestOfferId)
    {
        var filter = Builders<Auction>.Filter.Eq(a => a.Id, auctionId);
        var update = Builders<Auction>.Update.Set(a => a.BestCurrentOfferId, bestOfferId);
        await _auctions.UpdateOneAsync(filter, update);
    }

    /// <inheritdoc/>
    public async Task<bool> HasActiveAuctionForUserStickerAsync(int userStickerId)
    {
        var filter = Builders<Auction>.Filter.And(
            Builders<Auction>.Filter.Eq(a => a.Status, AuctionStatus.Active),
            Builders<Auction>.Filter.Eq(a => a.UserStickerId, userStickerId)
        );
        return await _auctions.Find(filter).AnyAsync();
    }

    /// <inheritdoc/>
    public async Task<bool> TryMarkFinalizationCompletedAsync(int auctionId)
    {
        var filter = Builders<Auction>.Filter.And(
            Builders<Auction>.Filter.Eq(a => a.Id, auctionId),
            Builders<Auction>.Filter.Eq(a => a.FinalizationCompleted, false)
        );
        var update = Builders<Auction>.Update.Set(a => a.FinalizationCompleted, true);
        var result = await _auctions.UpdateOneAsync(filter, update);
        return result.ModifiedCount == 1;
    }

    /// <inheritdoc/>
    public async Task<bool> TryClearUserSelectedBestOfferAsync(int auctionId)
    {
        var filter = Builders<Auction>.Filter.And(
            Builders<Auction>.Filter.Eq(a => a.Id, auctionId),
            Builders<Auction>.Filter.Eq(a => a.Status, AuctionStatus.Active)
        );
        var update = Builders<Auction>.Update.Unset(a => a.UserSelectedBestOfferId);
        var result = await _auctions.UpdateOneAsync(filter, update);
        return result.MatchedCount == 1;
    }
}