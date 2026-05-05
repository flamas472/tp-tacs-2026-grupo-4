using Figuritas.Shared.DTO;
using Figuritas.Shared.Model;
using Figuritas.Shared.Utils;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Figuritas.Api.Services;

public class UserService(
    UserRepository userRepo, 
    UserStickerRepository inventoryRepo, 
    StickerService stickerService,
    ExchangeRepository exchangeRepo
    )
{
    private readonly UserStickerRepository _inventoryRepo = inventoryRepo;
    private readonly UserRepository _userRepo = userRepo;
    private readonly StickerService _stickerService = stickerService;
    private readonly ExchangeRepository _exchangeRepo = exchangeRepo;

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
            HashedPassword = password, // TODO: Hashear la password
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

        // Solo actualizamos si el parámetro NO es nulo
        if (patchDTO.Username != null) 
            user.Username = patchDTO.Username;

        if (patchDTO.Password != null) 
            user.HashedPassword = patchDTO.Password; // TODO: Hashear la password

        _userRepo.Update(user);
        return user;
    }

    public User? ValidateCredentials(PostUserDTO userDTO)
    {
        var username = userDTO.Username;
        var password = userDTO.Password;

        var user = _userRepo.GetAll().FirstOrDefault(u => u.Username == username);
        if (user == null || user.HashedPassword != password) // TODO: Hash de la clave
            return null; // Credenciales inválidas
        return user; // Credenciales válidas
    }

    public Sticker AddMissingStickerToUser(int userId, Sticker missingSticker)
    {
        User? user = _userRepo.GetById(userId);
        
        if (user == null) throw new ArgumentException("User not found");
        if (user.HasMissingSticker(missingSticker)) throw new ArgumentException("Missing sticker already registered");

        _stickerService.CreateIfNonExistent(missingSticker);


        user.AddMissingSticker(missingSticker);

        return missingSticker;
    }

    public UserSticker CreateUserSticker(int userId, PostUserStickerRequestDTO data)
    {
        if(!_userRepo.ExistsId(userId)) throw new ArgumentException("User not found");

        Sticker sticker = data.Sticker.ToDomain();

        _stickerService.CreateIfNonExistent(sticker);

        var userSticker = data.ToDomain(userId);

        if (_inventoryRepo.Exists(userSticker)) throw new ArgumentException("Inventory already registered");

        _inventoryRepo.Add(userSticker);

        return userSticker;
    }

    public List<UserSticker> GetAllUserStickers() => _inventoryRepo.GetAll();

    public UserSticker UpdateUserSticker(int stickerId, bool? canBeExchanged, int? quantity)
    {
        var sticker = _inventoryRepo.GetById(stickerId);
        if (sticker == null) 
            throw new ArgumentException("Sticker not found");

        // Solo actualizamos si el parámetro NO es nulo
        if (canBeExchanged.HasValue) 
            sticker.CanBeExchanged = canBeExchanged.Value;

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

    // TODO: Ver si se puede mejorar esta solución
    public Rate CreateUserRate(int exchangeId, PostRateDTO postRateDTO, int raterId)
    {
        var exchange = _exchangeRepo.GetById(exchangeId);
        if (exchange == null) 
            throw new ArgumentException("Exchange not found");
        if(exchange.User1ID != raterId || exchange.User2ID != raterId)
            throw new ArgumentException("User did not participate in the exchange");

        var rate = new Rate
        {
            Score = postRateDTO.Score,
            Comment = postRateDTO.Comment ?? string.Empty,
            ExchangeID = exchangeId,
            RaterID = raterId
        };

        if(exchange.User1ID == raterId)
        {
            var user2 = _userRepo.GetById(exchange.User2ID); // El usuario a ratear es el otro que participó del intercambio
            user2.Ratings.Add(rate);
            _userRepo.Update(user2);
        }
        else
        {
            var user1 = _userRepo.GetById(exchange.User1ID); // El usuario a ratear es el otro que participó del intercambio
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