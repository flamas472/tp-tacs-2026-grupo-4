using Figuritas.Shared.Model;
using MongoDB.Driver;

namespace Figuritas.Api.Repositories;

public class MissingStickerRepository : IMissingStickerRepository
{
    private readonly IMongoCollection<MissingSticker> _collection;
    private readonly IIdGenerator _idGenerator;

    public MissingStickerRepository(MongoDbContext context, IIdGenerator idGenerator)
    {
        _collection = context.Collection<MissingSticker>("MissingStickers");
        _idGenerator = idGenerator;
        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        // Index: UserId — accelerates per-user missing-sticker lookups in SuggestionService
        _collection.Indexes.CreateOne(new CreateIndexModel<MissingSticker>(
            Builders<MissingSticker>.IndexKeys.Ascending(ms => ms.UserId),
            new CreateIndexOptions { Name = "idx_missingsticker_userid" }));

        // Index: StickerId — accelerates cross-user sticker availability lookups (US04 suggestions)
        _collection.Indexes.CreateOne(new CreateIndexModel<MissingSticker>(
            Builders<MissingSticker>.IndexKeys.Ascending(ms => ms.StickerId),
            new CreateIndexOptions { Name = "idx_missingsticker_stickerid" }));

        // Compound unique index: UserId + StickerId — prevents duplicates and covers existence checks
        _collection.Indexes.CreateOne(new CreateIndexModel<MissingSticker>(
            Builders<MissingSticker>.IndexKeys
                .Ascending(ms => ms.UserId)
                .Ascending(ms => ms.StickerId),
            new CreateIndexOptions { Unique = true, Name = "idx_missingsticker_userid_stickerid" }));
    }

    public async Task<List<MissingSticker>> GetByUserIdAsync(int userId) =>
        await _collection.Find(ms => ms.UserId == userId).ToListAsync();

    public async Task AddAsync(MissingSticker missingSticker)
    {
        missingSticker.Id = _idGenerator.GetNextId<MissingSticker>();
        await _collection.InsertOneAsync(missingSticker);
    }

    public async Task<bool> ExistsAsync(int userId, int stickerId) =>
        await _collection.Find(ms => ms.UserId == userId && ms.StickerId == stickerId).AnyAsync();

    public async Task<bool> DeleteAsync(int userId, int stickerId)
    {
        var result = await _collection.DeleteOneAsync(ms => ms.UserId == userId && ms.StickerId == stickerId);
        return result.DeletedCount > 0;
    }

    public async Task<bool> DeleteAsync(int userId, int stickerId, IClientSessionHandle session)
    {
        var result = await _collection.DeleteOneAsync(session, ms => ms.UserId == userId && ms.StickerId == stickerId);
        return result.DeletedCount > 0;
    }

    public async Task<MissingSticker?> GetByIdAsync(int id) =>
        await _collection.Find(ms => ms.Id == id).FirstOrDefaultAsync();

    public async Task<List<int>> GetStickerIdsByUserIdAsync(int userId)
    {
        // Project only StickerId at the MongoDB level — avoids loading the full document.
        return await _collection
            .Find(ms => ms.UserId == userId)
            .Project(ms => ms.StickerId)
            .ToListAsync();
    }

    public async Task<List<MissingSticker>> GetByUserIdsAsync(List<int> userIds)
    {
        var filter = Builders<MissingSticker>.Filter.In(ms => ms.UserId, userIds);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<List<int>> GetUserIdsForStickerAsync(int stickerId)
    {
        return await _collection
            .Find(ms => ms.StickerId == stickerId)
            .Project(ms => ms.UserId)
            .ToListAsync();
    }
}
