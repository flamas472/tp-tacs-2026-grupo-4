using FiguritasApi.Model;

namespace FiguritasApi.Services;

public class UserService
{
    private readonly UserStickerRepository _inventoryRepo;
    private readonly UserRepository _userRepo;
    private readonly StickerRepository _stickerRepo;

    public UserService(UserRepository userRepo, UserStickerRepository inventoryRepo, StickerRepository stickerRepo)
    {
        _inventoryRepo = inventoryRepo;
        _userRepo = userRepo;
        _stickerRepo = stickerRepo;
    }

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

    /* Ya no haría falta. Los user sticker se crean directamente asociados al usuario.
    public void AddUserStickerToUser(int userId, UserSticker userSticker)
    {
        var user = _userRepo.GetById(userId);
        if (user == null) throw new ArgumentException("User not found");
        _inventoryRepo.Add(userSticker);
    }
    */
    public void AddMissingStickerToUser(int userId, Sticker missingSticker)
    {
        var user = _userRepo.GetById(userId);
        if (user == null) throw new ArgumentException("User not found");
    }

    public UserSticker CreateUserSticker(int userId, int stickerId, bool canBeExchanged)
    {
        var sticker = _stickerRepo.GetById(stickerId);
        if (sticker == null) throw new ArgumentException("Sticker not found");

        var userSticker = new UserSticker
        {
            Id = 0,
            UserId = userId,
            Sticker = sticker,
            CanBeExchanged = canBeExchanged
        };

        _inventoryRepo.Add(userSticker);
        return userSticker;
    }

    public List<UserSticker> GetAllUserStickers() => _inventoryRepo.GetAll();

    public UserSticker UpdateUserSticker(int stickerId, bool? canBeExchanged, int? quantity)
    {
        var sticker = _inventoryRepo.GetById(stickerId);
        if (sticker == null) 
            throw new ArgumentException("Figurita no encontrada");

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