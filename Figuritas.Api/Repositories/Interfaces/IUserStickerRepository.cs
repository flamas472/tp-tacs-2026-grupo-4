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

    /// <summary>
    /// Atomically decrements Quantity by 1 only when Quantity > 0 and Active == true.
    /// Returns true if the decrement succeeded (stock was available and reserved).
    /// Returns false if there was no available stock (Quantity == 0 or Active == false),
    /// meaning the caller should abort and not proceed with the operation.
    /// </summary>
    Task<bool> TryReserveOneUnitAsync(int userStickerId);

    /// <summary>
    /// Atomically increments Quantity by 1 and sets Active = true.
    /// Used to release previously-reserved stock when a proposal or offer is cancelled.
    /// </summary>
    Task IncrementQuantityAndActivateAsync(int userStickerId);

    /// <summary>
    /// Sets Active = false if Quantity is 0 or below.
    /// Called after <see cref="TryReserveOneUnitAsync"/> to reflect exhausted stock.
    /// No-op if the sticker still has remaining stock.
    /// </summary>
    Task DeactivateIfEmptyAsync(int userStickerId);
}
