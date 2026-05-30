using BCrypt.Net;
using Figuritas.Api.Repositories;
using Figuritas.Shared.DTO;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.Model;
using Figuritas.Shared.Utils;

namespace Figuritas.Api.Services;

public class UserService(
    IUserRepository userRepo,
    IUserStickerRepository inventoryRepo,
    StickerService stickerService,
    IExchangeRepository exchangeRepo
    )
{
    private readonly IUserStickerRepository _inventoryRepo = inventoryRepo;
    private readonly IUserRepository _userRepo = userRepo;
    private readonly StickerService _stickerService = stickerService;
    private readonly IExchangeRepository _exchangeRepo = exchangeRepo;

    public List<User> GetAllUsers() => _userRepo.GetAll();

    public User? GetUserById(int id) => _userRepo.GetById(id);

    public User CreateUser(PostUserDTO userDTO)
    {
        var username = userDTO.Username;
        var password = userDTO.Password;

        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required");
        if (_userRepo.GetAll().Any(u => u.Username == username))
            throw new ArgumentException("Username already exists");
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password is required");

        var user = new User
        {
            Username = username,
            HashedPassword = BCrypt.Net.BCrypt.HashPassword(password),
            IsAdmin = false
        };

        _userRepo.Add(user);
        return user;
    }

    public User UpdateUser(int userID, PatchUserDTO patchDTO)
    {
        var user = _userRepo.GetById(userID);
        if (user == null)
            throw new ArgumentException("User not found");

        if (patchDTO.Username != null)
            user.Username = patchDTO.Username;

        if (patchDTO.Password != null)
            user.HashedPassword = BCrypt.Net.BCrypt.HashPassword(patchDTO.Password);

        _userRepo.Update(user);
        return user;
    }

    public User? ValidateCredentials(PostUserDTO userDTO)
    {
        var username = userDTO.Username;
        var password = userDTO.Password;

        var user = _userRepo.GetAll().FirstOrDefault(u => u.Username == username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.HashedPassword))
            return null;
        return user;
    }

    public Sticker AddMissingStickerToUser(int userId, int stickerId)
    {
        User? user = _userRepo.GetById(userId);
        if (user == null) throw new ArgumentException("User not found");

        var sticker = _stickerService.GetById(stickerId);
        if (sticker == null) throw new ArgumentException("Sticker not found in catalog");

        if (user.HasMissingSticker(sticker)) throw new ArgumentException("Missing sticker already registered");

        user.AddMissingSticker(sticker);
        _userRepo.Update(user);

        return sticker;
    }

    public UserSticker CreateUserSticker(int userId, PostUserStickerRequestDTO data)
    {
        if (!_userRepo.ExistsId(userId))
            throw new ArgumentException("User not found");

        var sticker = _stickerService.GetById(data.StickerId);
        if (sticker == null)
            throw new ArgumentException("Sticker not found in catalog");

        var userSticker = new UserSticker
        {
            UserId = userId,
            Sticker = sticker,
            Quantity = data.Quantity,
            CanBeDirectlyExchanged = data.CanBeDirectlyExchanged,
            CanBeAuctioned = data.CanBeAuctioned,
            Active = true
        };

        if (_inventoryRepo.Exists(userSticker))
            throw new ArgumentException("Inventory already registered");

        _inventoryRepo.Add(userSticker);

        return userSticker;
    }

    public List<UserSticker> GetAllUserStickers() => _inventoryRepo.GetAll();

    public UserSticker? GetUserStickerById(int userId, int stickerId)
    {
        var userSticker = _inventoryRepo.GetById(stickerId);
        if (userSticker == null || userSticker.UserId != userId)
            return null;
        return userSticker;
    }

    public List<UserSticker> SearchUserStickers(GetUserStickersDTO queryParams)
    {
        var filter = queryParams.ToPredicate();
        return _inventoryRepo.GetPaginated(queryParams.Page, queryParams.PageSize, filter);
    }

    public UserSticker UpdateUserSticker(int stickerId, bool? canBeDirectlyExchanged, bool? canBeAuctioned, int? quantity)
    {
        var sticker = _inventoryRepo.GetById(stickerId);
        if (sticker == null)
            throw new ArgumentException("Sticker not found");

        if (canBeDirectlyExchanged.HasValue)
            sticker.CanBeDirectlyExchanged = canBeDirectlyExchanged.Value;

        if (canBeAuctioned.HasValue)
            sticker.CanBeAuctioned = canBeAuctioned.Value;

        if (quantity.HasValue)
            sticker.Quantity = quantity.Value;

        _inventoryRepo.Update(sticker);
        return sticker;
    }

    public void DeleteUserSticker(int userStickerId)
    {
        _inventoryRepo.Delete(userStickerId);
    }

    public List<Rate> GetAllUserRatings(int userId)
    {
        User? user = _userRepo.GetById(userId);
        if (user == null) throw new ArgumentException("User not found");

        return user.Ratings;
    }

    public Rate CreateUserRate(int exchangeId, PostRateDTO postRateDTO, int raterId)
    {
        var exchange = _exchangeRepo.GetById(exchangeId);
        if (exchange == null)
            throw new ArgumentException("Exchange not found");
        if (exchange.ProponentID != raterId && exchange.ProposedID != raterId)
            throw new ArgumentException("User did not participate in the exchange");

        var rate = new Rate
        {
            Score = postRateDTO.Score,
            Comment = postRateDTO.Comment ?? string.Empty,
            ExchangeID = exchangeId,
            RaterID = raterId
        };

        if (exchange.ProponentID == raterId)
        {
            var user2 = _userRepo.GetById(exchange.ProposedID)
                ?? throw new ArgumentException("Rated user not found.");
            user2.Ratings.Add(rate);
            _userRepo.Update(user2);
        }
        else
        {
            var user1 = _userRepo.GetById(exchange.ProponentID)
                ?? throw new ArgumentException("Rated user not found.");
            user1.Ratings.Add(rate);
            _userRepo.Update(user1);
        }

        return rate;
    }

    public double GetUserReputation(int userId)
    {
        User? user = _userRepo.GetById(userId);
        if (user == null) throw new ArgumentException("User not found");

        return user.Reputation;
    }
}
