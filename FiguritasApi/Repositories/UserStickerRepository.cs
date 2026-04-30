using System.Collections.Concurrent;
using FiguritasApi.Model;

// Repo for in-memory persistence.
public class UserStickerRepository
{
    private readonly ConcurrentBag<UserSticker> UserStickers = new();
    private int nextId = 1;

    public List<UserSticker> GetAll(Func<UserSticker, bool> predicate)
    {
        return UserStickers.Where(predicate).ToList();
    }

    public void Add(UserSticker userSticker)
    {
        userSticker.Id = Interlocked.Increment(ref nextId) - 1;
        UserStickers.Add(userSticker);
    }

    public UserSticker? GetById(int id) => UserStickers.FirstOrDefault(a => a.Id == id);
}