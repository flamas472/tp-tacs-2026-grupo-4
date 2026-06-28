using Figuritas.Shared.Enums;
using Figuritas.Shared.Model;

namespace Figuritas.Api.Repositories;

public interface IUserRepository
{
    bool ExistsId(int userId);
    List<User> GetAll();
    List<User> GetAllPaginated(int page, int pageSize);
    void Add(User user);
    User? GetById(int userId);
    User? GetByUsername(string username);
    List<User> GetByIds(List<int> ids);
    List<User> GetByRole(UserRole role);
    List<User> GetByRoles(List<UserRole> roles, int page, int pageSize);
    void Update(User user);

    /// <summary>
    /// Atomically pushes <paramref name="rate"/> into the target user's Ratings array only if
    /// no rating from <paramref name="raterUserId"/> for <paramref name="exchangeId"/> already exists.
    /// The filter uses <c>$not / $elemMatch</c> to prevent concurrent duplicate submissions.
    /// Returns true if the rating was added (ModifiedCount == 1).
    /// Returns false if the guard condition was not met (duplicate already present).
    /// </summary>
    Task<bool> TryAddRatingAsync(int targetUserId, Rate rate, int raterUserId, int exchangeId);
}
