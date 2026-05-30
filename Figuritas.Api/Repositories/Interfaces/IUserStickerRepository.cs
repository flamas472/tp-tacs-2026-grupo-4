using System.Linq.Expressions;
using Figuritas.Shared.Model;

namespace Figuritas.Api.Repositories;

public interface IUserStickerRepository
{
    List<UserSticker> GetAll();
    List<UserSticker> GetPaginated(int page, int pageSize, Expression<Func<UserSticker, bool>>? filter = null);
    void Add(UserSticker userSticker);
    UserSticker? GetById(int id);
    List<UserSticker> GetMultipleById(List<int> ids);
    bool Exists(UserSticker userSticker);
    void Update(UserSticker userSticker);
    void Delete(int userStickerId);
}
