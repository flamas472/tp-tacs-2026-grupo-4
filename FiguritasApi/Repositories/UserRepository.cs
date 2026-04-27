using System.Collections.Concurrent;
using FiguritasApi.Model;

// Repo for in-memory persistence.
public class UserRepository
{
    private readonly ConcurrentBag<User> users = new();
    private int nextId = 1;

    public List<User> GetAll()
    {
        return users.ToList();
    }

    public void Add(User user)
    {
        user.Id = Interlocked.Increment(ref nextId) - 1;
        users.Add(user);
    }

    public User? GetById(int userId)
    {
        return users.FirstOrDefault(u => u.Id == userId);
    }
}
