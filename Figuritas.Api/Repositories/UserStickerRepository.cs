using System.Linq.Expressions;
using Figuritas.Shared.Model;
using MongoDB.Driver;

namespace Figuritas.Api.Repositories;

public class UserStickerRepository : IUserStickerRepository
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
        return _userStickers.Find(us => us.Active).ToList();
    }

    public List<UserSticker> GetPaginated(int page, int pageSize, Expression<Func<UserSticker, bool>>? filter = null, bool sortDescending = false)
    {
        if (page < 1 || pageSize < 1)
        {
            throw new ArgumentException("Page and PageSize must be greater than 0");
        }

        var activeFilter = Builders<UserSticker>.Filter.Eq(us => us.Active, true);
        var combinedFilter = filter != null
            ? Builders<UserSticker>.Filter.And(activeFilter, filter)
            : activeFilter;

        var query = _userStickers.Find(combinedFilter);
        if (sortDescending)
            query = query.SortByDescending(us => us.Id);

        return query.Skip((page - 1) * pageSize).Limit(pageSize).ToList();
    }

    public void Add(UserSticker userSticker)
    {
        userSticker.Id = _idGenerator.GetNextId<UserSticker>();
        _userStickers.InsertOne(userSticker);
    }

    public UserSticker? GetById(int id) => _userStickers.Find(us => us.Id == id && us.Active).FirstOrDefault();

    public UserSticker? GetByIdIncludingInactive(int id) => _userStickers.Find(us => us.Id == id).FirstOrDefault();

    public List<UserSticker> GetMultipleById(List<int> ids) => _userStickers.Find(us => ids.Contains(us.Id) && us.Active).ToList();

    public List<UserSticker> GetMultipleByIdIncludingInactive(List<int> ids) => _userStickers.Find(us => ids.Contains(us.Id)).ToList();

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

    public List<UserSticker> GetByStickerIds(List<int> stickerIds, int excludeUserId)
    {
        var filter = Builders<UserSticker>.Filter.And(
            Builders<UserSticker>.Filter.In(us => us.Sticker.Id, stickerIds),
            Builders<UserSticker>.Filter.Ne(us => us.UserId, excludeUserId),
            Builders<UserSticker>.Filter.Gt(us => us.Quantity, 0),
            Builders<UserSticker>.Filter.Eq(us => us.Active, true),
            Builders<UserSticker>.Filter.Eq(us => us.CanBeDirectlyExchanged, true)
        );
        return _userStickers.Find(filter).ToList();
    }

    public List<UserSticker> GetByUserId(int userId) =>
        _userStickers.Find(us => us.UserId == userId && us.Active).ToList();

    public List<UserSticker> GetByUserIdPaginated(int userId, int page, int pageSize)
    {
        var filter = Builders<UserSticker>.Filter.And(
            Builders<UserSticker>.Filter.Eq(us => us.UserId, userId),
            Builders<UserSticker>.Filter.Gt(us => us.Quantity, 0)
        );

        return _userStickers.Find(filter)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToList();
    }
}
