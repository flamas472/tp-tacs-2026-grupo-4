using Figuritas.Shared.Model;

namespace Figuritas.Api.Repositories;

public interface IStickerRepository
{
    List<Sticker> GetAll();
    IEnumerable<Sticker> Get(Func<Sticker, bool> predicate, int page, int pageSize);
    Sticker? GetById(int id);
    Sticker? FindOne(Func<Sticker, bool> predicate);
    void Add(Sticker sticker);
    bool Exists(Sticker sticker);
    bool Update(int id, Sticker updated);
}
