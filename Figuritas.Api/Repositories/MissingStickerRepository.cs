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

    public async Task<MissingSticker?> GetByIdAsync(int id) =>
        await _collection.Find(ms => ms.Id == id).FirstOrDefaultAsync();

    public async Task<List<int>> GetStickerIdsByUserIdAsync(int userId)
    {
        var results = await _collection.Find(ms => ms.UserId == userId).ToListAsync();
        return results.Select(ms => ms.StickerId).ToList();
    }

    public async Task<List<MissingSticker>> GetByUserIdsAsync(List<int> userIds)
    {
        var filter = Builders<MissingSticker>.Filter.In(ms => ms.UserId, userIds);
        return await _collection.Find(filter).ToListAsync();
    }
}
