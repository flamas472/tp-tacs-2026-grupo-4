using System;
using System.Collections.Generic;
using System.Linq;
using Figuritas.Shared.Model;
using MongoDB.Driver;

namespace Figuritas.Api.Repositories;

public class StickerRepository : IStickerRepository
{
    private readonly IMongoCollection<Sticker> _stickers;
    private readonly IIdGenerator _idGenerator;

    public StickerRepository(MongoDbContext context, IIdGenerator idGenerator)
    {
        _stickers = context.Collection<Sticker>("Stickers");
        _idGenerator = idGenerator;
    }

    public List<Sticker> GetAll() => _stickers.Find(_ => true).ToList();

    public IEnumerable<Sticker> Get(Func<Sticker, bool> predicate, int page, int pageSize)
    {
        var stickers = _stickers.Find(_ => true).ToList();
        return stickers.Where(predicate).Skip((page - 1) * pageSize).Take(pageSize);
    }

    public Sticker? GetById(int id) => _stickers.Find(s => s.Id == id).FirstOrDefault();

    public Sticker? FindOne(Func<Sticker, bool> predicate)
    {
        return _stickers.Find(_ => true).ToList().FirstOrDefault(predicate);
    }

    public void Add(Sticker sticker)
    {
        sticker.Id = _idGenerator.GetNextId<Sticker>();
        _stickers.InsertOne(sticker);
    }

    public bool Exists(Sticker sticker)
    {
        return _stickers.Find(s =>
            s.Number == sticker.Number &&
            s.Description == sticker.Description &&
            s.Team == sticker.Team &&
            s.NationalTeam == sticker.NationalTeam &&
            s.Category == sticker.Category
        ).Any();
    }

    public bool Update(int id, Sticker updated)
    {
        var result = _stickers.ReplaceOne(s => s.Id == id, updated);
        return result.MatchedCount > 0;
    }
}
