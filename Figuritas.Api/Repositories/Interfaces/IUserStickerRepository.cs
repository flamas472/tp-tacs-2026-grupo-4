using System.Linq.Expressions;
using Figuritas.Shared.Model;
using MongoDB.Driver;

namespace Figuritas.Api.Repositories;

public interface IUserStickerRepository
{
    List<UserSticker> GetAll();
    /// <param name="sortDescending">When true, results are sorted descending to surface the most recent market listings first.</param>
    List<UserSticker> GetPaginated(int page, int pageSize, Expression<Func<UserSticker, bool>>? filter = null, bool sortDescending = false);
    void Add(UserSticker userSticker);
    void Add(UserSticker userSticker, IClientSessionHandle session);
    UserSticker? GetById(int id);
    UserSticker? GetByIdIncludingInactive(int id);
    UserSticker? GetByIdIncludingInactive(int id, IClientSessionHandle session);
    List<UserSticker> GetMultipleById(List<int> ids);
    List<UserSticker> GetMultipleByIdIncludingInactive(List<int> ids);
    List<UserSticker> GetMultipleByIdIncludingInactive(List<int> ids, IClientSessionHandle session);
    bool Exists(UserSticker userSticker);
    void Update(UserSticker userSticker);
    void Update(UserSticker userSticker, IClientSessionHandle session);
    void Delete(int userStickerId);
    List<UserSticker> GetByStickerIds(List<int> stickerIds, int excludeUserId);
    List<UserSticker> GetByUserId(int userId);
    List<UserSticker> GetByUserId(int userId, IClientSessionHandle session);
    Task<UserSticker?> GetByUserIdAndCatalogIdIncludingInactiveAsync(int userId, int catalogStickerId);
    List<UserSticker> GetByUserIdPaginated(int userId, int page, int pageSize);
}
