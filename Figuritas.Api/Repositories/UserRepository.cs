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
    }

    public bool ExistsId(int userId)
    {
        return _users.Find(u => u.Id == userId).Any();
    }

    public List<User> GetAll()
    {
        return _users.Find(_ => true).ToList();
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

    public List<User> GetByIds(List<int> ids)
    {
        var filter = Builders<User>.Filter.In(u => u.Id, ids);
        return _users.Find(filter).ToList();
    }

    public void Update(User user)
    {
        var result = _users.ReplaceOne(u => u.Id == user.Id, user);
        if (!result.IsAcknowledged || result.MatchedCount == 0)
        {
            throw new ArgumentException("User not found");
        }
    }
}
