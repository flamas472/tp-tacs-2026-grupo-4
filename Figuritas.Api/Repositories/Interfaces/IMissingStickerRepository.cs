using Figuritas.Shared.Model;
using MongoDB.Driver;

namespace Figuritas.Api.Repositories;

public interface IMissingStickerRepository
{
    Task<List<MissingSticker>> GetByUserIdAsync(int userId);
    Task AddAsync(MissingSticker missingSticker);
    Task<bool> ExistsAsync(int userId, int stickerId);
    Task<bool> DeleteAsync(int userId, int stickerId);
    Task<bool> DeleteAsync(int userId, int stickerId, IClientSessionHandle session);
    Task<MissingSticker?> GetByIdAsync(int id);
    Task<List<int>> GetStickerIdsByUserIdAsync(int userId);
    Task<List<MissingSticker>> GetByUserIdsAsync(List<int> userIds);
    Task<List<int>> GetUserIdsForStickerAsync(int stickerId);
}
