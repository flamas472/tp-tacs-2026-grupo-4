using FiguritasApi.Model;

namespace FiguritasApi.Services;

public class UserService
{
    private readonly UserRepository _userRepo;
    private readonly UserStickerRepository _inventoryRepo;
    private readonly ExchangeProposalRepository _exchangePropRepo;

    public UserService(UserRepository userRepo, UserStickerRepository inventoryRepo, ExchangeProposalRepository exchangePropRepo)
    {
        _userRepo = userRepo;
        _inventoryRepo = inventoryRepo;
        _exchangePropRepo = exchangePropRepo;
    }

    public List<User> GetAllUsers() => _userRepo.GetAll();

    public User? GetUserById(int id) => _userRepo.GetById(id);

    public void CreateUser(User user)
    {
        if (string.IsNullOrWhiteSpace(user.Username))
            throw new ArgumentException("Username is required");
        if (_userRepo.GetAll().Any(u => u.Username == user.Username))
            throw new ArgumentException("Username already exists");
        _userRepo.Add(user);
    }

    public void AddUserStickerToUser(int userId, UserSticker userSticker)
    {
        var user = _userRepo.GetById(userId);
        if (user == null) throw new ArgumentException("User not found");
        _inventoryRepo.Add(userSticker);
        user.AddUserSticker(userSticker);
    }

    public void AddMissingStickerToUser(int userId, Sticker missingSticker)
    {
        var user = _userRepo.GetById(userId);
        if (user == null) throw new ArgumentException("User not found");
        user.AddMissingSticker(missingSticker);
    }

    // ¿Habría que crear services aparte para estas cosas?
    public UserSticker? GetUserStickerById(int id) => _inventoryRepo.GetById(id);

    public void CreateExchangeProposal(ExchangeProposal exchangeProposal)
    {
        _exchangePropRepo.Add(exchangeProposal);
    }

}