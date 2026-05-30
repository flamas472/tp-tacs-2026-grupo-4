using Figuritas.Shared.Model;

namespace Figuritas.Api.Repositories;

public interface IUserRepository
{
    bool ExistsId(int userId);
    List<User> GetAll();
    void Add(User user);
    User? GetById(int userId);
    void Update(User user);
}
