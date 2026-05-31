using Figuritas.Shared.Model;

namespace Figuritas.Api.Repositories;

public interface IUserRepository
{
    bool ExistsId(int userId);
    List<User> GetAll();
    void Add(User user);
    User? GetById(int userId);
    List<User> GetByIds(List<int> ids);
    void Update(User user);
}
