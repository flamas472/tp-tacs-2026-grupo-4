using Figuritas.Shared.DTO;
using Figuritas.Shared.Model;
using Figuritas.Shared.Utils;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Figuritas.Api.Services;

public class UserService(
    UserRepository userRepo, 
    UserStickerRepository inventoryRepo, 
    StickerService stickerService
    )
{
    private readonly UserStickerRepository _inventoryRepo = inventoryRepo;
    private readonly UserRepository _userRepo = userRepo;
    private readonly StickerService _stickerService = stickerService;


    public List<User> GetAllUsers() => _userRepo.GetAll();

    public User? GetUserById(int id) => _userRepo.GetById(id);

    public User CreateUser(string username, string password)
    {
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
            isAdmin = false
        };

        _userRepo.Add(user);
        return user;
    }

    public User? ValidateCredentials(string username, string password)
    {
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

}