using FiguritasApi.Model;

namespace FiguritasApi.Services;

public class UserService
{
    private readonly UserRepository _userRepo;
    private readonly InventoryFiguritaRepository _inventoryRepo;

    public UserService(UserRepository userRepo, InventoryFiguritaRepository inventoryRepo)
    {
        _userRepo = userRepo;
        _inventoryRepo = inventoryRepo;
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

    public void AddInventoryFiguritaToUser(int userId, InventoryFigurita inventoryFigurita)
    {
        var user = _userRepo.GetById(userId);
        if (user == null) throw new ArgumentException("User not found");
        _inventoryRepo.Add(inventoryFigurita);
        user.AddInventoryFigurita(inventoryFigurita);
    }

    public void AddMissingFiguritaToUser(int userId, Figurita missingFigurita)
    {
        var user = _userRepo.GetById(userId);
        if (user == null) throw new ArgumentException("User not found");
        user.AddMissingFigurita(missingFigurita);
    }
}