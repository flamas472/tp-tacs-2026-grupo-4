using Figuritas.Shared.Model.Subastas;
using MongoDB.Driver;

namespace Figuritas.Api.Repositories;

public class AuctionWatchlistRepository : IAuctionWatchlistRepository
{
    private readonly IMongoCollection<AuctionWatchlist> _watchlists;
    private readonly IIdGenerator _idGenerator;

    public AuctionWatchlistRepository(MongoDbContext context, IIdGenerator idGenerator)
    {
        _watchlists = context.Collection<AuctionWatchlist>("AuctionWatchlists");
        _idGenerator = idGenerator;
        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        var indexKeys = Builders<AuctionWatchlist>.IndexKeys
            .Ascending(w => w.UserId)
            .Ascending(w => w.AuctionId);

        var indexOptions = new CreateIndexOptions { Unique = true };
        var indexModel = new CreateIndexModel<AuctionWatchlist>(indexKeys, indexOptions);
        _watchlists.Indexes.CreateOne(indexModel);
    }

    public async Task<AuctionWatchlist> AddAsync(AuctionWatchlist entry)
    {
        entry.Id = _idGenerator.GetNextId<AuctionWatchlist>();
        try
        {
            await _watchlists.InsertOneAsync(entry);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
        {
            throw new InvalidOperationException("Already watching this auction.", ex);
        }
        return entry;
    }

    public async Task<bool> RemoveAsync(int userId, int auctionId)
    {
        var result = await _watchlists.DeleteOneAsync(w => w.UserId == userId && w.AuctionId == auctionId);
        return result.DeletedCount > 0;
    }

    public async Task<bool> ExistsAsync(int userId, int auctionId)
    {
        return await _watchlists.Find(w => w.UserId == userId && w.AuctionId == auctionId).AnyAsync();
    }

    public async Task<List<int>> GetWatcherUserIdsAsync(int auctionId)
    {
        var entries = await _watchlists.Find(w => w.AuctionId == auctionId).ToListAsync();
        return entries.Select(w => w.UserId).ToList();
    }

    public async Task<List<AuctionWatchlist>> GetByUserIdAsync(int userId)
    {
        return await _watchlists.Find(w => w.UserId == userId).ToListAsync();
    }
}
