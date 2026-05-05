using System.Collections.Concurrent;
using Figuritas.Shared.Model;

// Repo for in-memory persistence.
public class UserRepository
{
    private readonly ConcurrentBag<User> users = [];
    private int nextId = 1;

    
    public bool ExistsId(int userId)
    {
        return users.Any(u => u.Id == userId);
    }

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
     
    public void Update(User user)
    {
        var existingUser = GetById(user.Id);
        if (existingUser == null) throw new ArgumentException("User not found");

        existingUser.Username = user.Username;
        existingUser.HashedPassword = user.HashedPassword;
    }
}
