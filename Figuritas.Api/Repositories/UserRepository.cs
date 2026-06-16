using Figuritas.Shared.Enums;
using Figuritas.Shared.Model;
using MongoDB.Driver;

namespace Figuritas.Api.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _users;
    private readonly IIdGenerator _idGenerator;

    public UserRepository(MongoDbContext context, IIdGenerator idGenerator)
    {
        _users = context.Collection<User>("Users");
        _idGenerator = idGenerator;
        EnsureIndexes();
    }

    public bool ExistsId(int userId)
    {
        return _users.Find(u => u.Id == userId).Any();
    }

    public List<User> GetAll()
    {
        return _users.Find(_ => true).ToList();
    }

    public List<User> GetAllPaginated(int page, int pageSize)
    {
        return _users.Find(_ => true)
            .SortBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToList();
    }

    public void Add(User user)
    {
        user.Id = _idGenerator.GetNextId<User>();
        _users.InsertOne(user);
    }

    public User? GetById(int userId)
    {
        return _users.Find(u => u.Id == userId).FirstOrDefault();
    }

    public User? GetByUsername(string username)
    {
        return _users.Find(u => u.Username == username).FirstOrDefault();
    }

    public List<User> GetByIds(List<int> ids)
    {
        var filter = Builders<User>.Filter.In(u => u.Id, ids);
        return _users.Find(filter).ToList();
    }

    public List<User> GetByRole(UserRole role)
    {
        return _users.Find(u => u.Role == role).ToList();
    }

    public List<User> GetByRoles(List<UserRole> roles, int page, int pageSize)
    {
        var filter = Builders<User>.Filter.In(u => u.Role, roles);
        return _users.Find(filter)
            .SortBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToList();
    }

    public void Update(User user)
    {
        var result = _users.ReplaceOne(u => u.Id == user.Id, user);
        if (!result.IsAcknowledged || result.MatchedCount == 0)
        {
            throw new ArgumentException("User not found");
        }
    }

    /// <summary>
    /// Creates a unique index on the Username field if it does not already exist.
    /// Called once at construction time to prevent concurrent duplicate username inserts
    /// that would bypass the application-level uniqueness check.
    /// </summary>
    private void EnsureIndexes()
    {
        var indexKeys = Builders<User>.IndexKeys.Ascending(u => u.Username);
        var indexOptions = new CreateIndexOptions { Unique = true, Name = "unique_username" };
        var indexModel = new CreateIndexModel<User>(indexKeys, indexOptions);
        _users.Indexes.CreateOne(indexModel);
    }
}
