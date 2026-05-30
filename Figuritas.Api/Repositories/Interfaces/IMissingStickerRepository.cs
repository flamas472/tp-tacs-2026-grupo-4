using Figuritas.Shared.Model;

namespace Figuritas.Api.Repositories;

public interface IMissingStickerRepository
{
    Task<List<MissingSticker>> GetByUserIdAsync(int userId);
    Task AddAsync(MissingSticker missingSticker);
    Task<bool> ExistsAsync(int userId, int stickerId);
    Task<bool> DeleteAsync(int userId, int stickerId);
    Task<MissingSticker?> GetByIdAsync(int id);
}
