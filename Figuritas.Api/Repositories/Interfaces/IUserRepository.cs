using Figuritas.Shared.Enums;
using Figuritas.Shared.Model;

namespace Figuritas.Api.Repositories;

public interface IUserRepository
{
    bool ExistsId(int userId);
    List<User> GetAll();
    void Add(User user);
    User? GetById(int userId);
    User? GetByUsername(string username);
    List<User> GetByIds(List<int> ids);
    List<User> GetByRole(UserRole role);
    List<User> GetByRoles(List<UserRole> roles, int page, int pageSize);
    void Update(User user);
}
