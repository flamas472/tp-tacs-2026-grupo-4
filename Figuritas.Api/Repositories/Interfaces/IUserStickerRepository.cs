using System.Linq.Expressions;
using Figuritas.Shared.Model;

namespace Figuritas.Api.Repositories;

public interface IUserStickerRepository
{
    List<UserSticker> GetAll();
    /// <param name="sortDescending">When true, results are sorted descending to surface the most recent market listings first.</param>
    List<UserSticker> GetPaginated(int page, int pageSize, Expression<Func<UserSticker, bool>>? filter = null, bool sortDescending = false);
    void Add(UserSticker userSticker);
    UserSticker? GetById(int id);
    List<UserSticker> GetMultipleById(List<int> ids);
    bool Exists(UserSticker userSticker);
    void Update(UserSticker userSticker);
    void Delete(int userStickerId);
    List<UserSticker> GetByStickerIds(List<int> stickerIds, int excludeUserId);
    List<UserSticker> GetByUserId(int userId);
    List<UserSticker> GetByUserIdPaginated(int userId, int page, int pageSize);
}
