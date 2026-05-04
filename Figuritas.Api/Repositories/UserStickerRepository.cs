using System.Collections.Concurrent;
using Figuritas.Shared.Model;

// Repo for in-memory persistence.
public class UserStickerRepository
{
    private readonly ConcurrentBag<UserSticker> UserStickers = new();
    private int nextId = 1;

    public List<UserSticker> GetAll()
    {
        
        return UserStickers.ToList();
    
    }
    public List<UserSticker> GetPaginated(int page, int pageSize, Func<UserSticker, bool>? filter = null)
    {

        if(page < 1 || pageSize < 1)
        {
            throw new ArgumentException("Page and PageSize must be grater than 0");
        }

        if(filter == null)
        {
            return UserStickers.Skip((page-1)*pageSize).Take(pageSize).ToList();
        }

        return UserStickers.Where(filter).Skip((page-1)*pageSize).Take(pageSize).ToList();

    }


    public void Add(UserSticker userSticker)
    {
        userSticker.Id = Interlocked.Increment(ref nextId) - 1;
        UserStickers.Add(userSticker);
    }

    public UserSticker? GetById(int id) => UserStickers.FirstOrDefault(a => a.Id == id);

    public bool Exists(UserSticker userSticker)
    {
        return UserStickers.Any(us => us.Sticker.Equals(userSticker.Sticker) && us.UserId == userSticker.UserId);
    }

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