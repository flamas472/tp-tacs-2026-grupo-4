using System.Linq.Expressions;
using Figuritas.Shared.Model;
using MongoDB.Driver;

public class UserStickerRepository
{
    private readonly IMongoCollection<UserSticker> _userStickers;
    private readonly IIdGenerator _idGenerator;

    public UserStickerRepository(MongoDbContext context, IIdGenerator idGenerator)
    {
        _userStickers = context.Collection<UserSticker>("UserStickers");
        _idGenerator = idGenerator;
    }

    public List<UserSticker> GetAll()
    {
        return _userStickers.Find(_ => true).ToList();
    }

    public List<UserSticker> GetPaginated(int page, int pageSize, Expression<Func<UserSticker, bool>>? filter = null)
    {
        if (page < 1 || pageSize < 1)
        {
            throw new ArgumentException("Page and PageSize must be grater than 0");
        }

        var find = _userStickers.Find(filter ?? (_ => true));
        return find.Skip((page - 1) * pageSize).Limit(pageSize).ToList();
    }

    public void Add(UserSticker userSticker)
    {
        userSticker.Id = _idGenerator.GetNextId<UserSticker>();
        _userStickers.InsertOne(userSticker);
    }

    public UserSticker? GetById(int id) => _userStickers.Find(a => a.Id == id).FirstOrDefault();

    public List<UserSticker> GetMultipleById(List<int> ids) => _userStickers.Find(us => ids.Contains(us.Id)).ToList();

    public bool Exists(UserSticker userSticker)
    {
        var filter = Builders<UserSticker>.Filter.And(
            Builders<UserSticker>.Filter.Eq(us => us.UserId, userSticker.UserId),
            Builders<UserSticker>.Filter.Eq(us => us.Sticker.Number, userSticker.Sticker.Number),
            Builders<UserSticker>.Filter.Eq(us => us.Sticker.Description, userSticker.Sticker.Description),
            Builders<UserSticker>.Filter.Eq(us => us.Sticker.Team, userSticker.Sticker.Team),
            Builders<UserSticker>.Filter.Eq(us => us.Sticker.NationalTeam, userSticker.Sticker.NationalTeam),
            Builders<UserSticker>.Filter.Eq(us => us.Sticker.Category, userSticker.Sticker.Category)
        );

        return _userStickers.Find(filter).Any();
    }

    public void Update(UserSticker userSticker)
    {
        var result = _userStickers.ReplaceOne(us => us.Id == userSticker.Id, userSticker);
        if (!result.IsAcknowledged || result.MatchedCount == 0)
        {
            throw new ArgumentException("UserSticker not found");
        }
    }

    public void Delete(int userStickerId)
    {
        var result = _userStickers.UpdateOne(
            us => us.Id == userStickerId,
            Builders<UserSticker>.Update.Set(us => us.Active, false));

        if (!result.IsAcknowledged || result.MatchedCount == 0)
        {
            throw new ArgumentException("UserSticker not found");
        }
    }
}
