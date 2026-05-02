using System.Collections.Concurrent;
using FiguritasApi.Model;

// Repo for in-memory persistence.
public class UserStickerRepository
{
    private readonly ConcurrentBag<UserSticker> UserStickers = new();
    private int nextId = 1;

    public List<UserSticker> GetAll()
    {
        return UserStickers.ToList();
    }

    public void Add(UserSticker userSticker)
    {
        userSticker.Id = Interlocked.Increment(ref nextId) - 1;
        UserStickers.Add(userSticker);
    }

    public UserSticker? GetById(int id) => UserStickers.FirstOrDefault(a => a.Id == id);

    public void Update(UserSticker userSticker)
    {
        var existingUserSticker = GetById(userSticker.Id);
        if (existingUserSticker != null)
        {
            UserStickers.TryTake(out existingUserSticker);
            UserStickers.Add(userSticker);
        }
    }

    public void Delete(int userStickerId)
    {
        var userSticker = GetById(userStickerId);
        if (userSticker != null)
        {
            var deletedUserSticker = new UserSticker
            {
                Id = userSticker.Id,
                UserId = userSticker.UserId,
                Sticker = userSticker.Sticker,
                CanBeExchanged = userSticker.CanBeExchanged,
                Quantity = 0,
                Active = false
            };
            UserStickers.TryTake(out userSticker);
            UserStickers.Add(deletedUserSticker);
        }
    }
}