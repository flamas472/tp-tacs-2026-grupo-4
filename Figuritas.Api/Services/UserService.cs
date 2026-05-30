using Figuritas.Shared.DTO;
using Figuritas.Shared.Model;
using Figuritas.Shared.Exceptions;
using System.Security.Cryptography;
using System.Text;

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

    public User GetUserById(int id) 
    {
        var user = _userRepo.GetById(id);
        return user ?? throw new EntityNotFoundException("User not found");
    }

    public User CreateUser(PostUserDTO userDTO)
    {
        if (string.IsNullOrWhiteSpace(userDTO.Username))
            throw new BusinessRuleException("Username is required");
        if (_userRepo.GetAll().Any(u => u.Username == userDTO.Username))
            throw new BusinessRuleException("Username already exists");
        if (string.IsNullOrWhiteSpace(userDTO.Password))
            throw new BusinessRuleException("Password is required");

        var user = new User
        {
            Username = userDTO.Username,
            HashedPassword = HashPassword(userDTO.Password),
            IsAdmin = false
        };

        _userRepo.Add(user);
        return user;
    }

    public User UpdateUser(int userID, PatchUserDTO patchDTO)
    {
        if (patchDTO.Username == null && patchDTO.Password == null)
            throw new BusinessRuleException("Debe proporcionar al menos un campo para actualizar.");

        var user = _userRepo.GetById(userID) ?? throw new EntityNotFoundException("User not found");

        if (patchDTO.Username != null) 
            user.Username = patchDTO.Username;

        if (patchDTO.Password != null) 
            user.HashedPassword = HashPassword(patchDTO.Password); 

        _userRepo.Update(user);
        return user;
    }

    public List<Sticker> GetMissingStickers(int userId)
    {
        var user = _userRepo.GetById(userId) ?? throw new EntityNotFoundException("User not found");
        return user.MissingStickers;
    }

    public void RemoveMissingSticker(int userId, int stickerId)
    {
        var user = _userRepo.GetById(userId) ?? throw new EntityNotFoundException("User not found");
        user.RemoveMissingSticker(stickerId);
        _userRepo.Update(user);
    }

    public Sticker AddMissingStickerToUser(int userId, PostMissingStickerRequestDTO data)
    {
        var user = _userRepo.GetById(userId) ?? throw new EntityNotFoundException("User not found");
        
        // El sticker ahora se asume precargado, lo buscamos en su repositorio
        var missingSticker = _stickerService.GetStickerById(data.StickerID) ?? throw new EntityNotFoundException("Sticker not found in the global catalog");

        if (user.HasMissingSticker(missingSticker)) 
            throw new BusinessRuleException("Missing sticker already registered");

        user.AddMissingSticker(missingSticker);
        _userRepo.Update(user);

        return missingSticker;
    }

    public UserSticker CreateUserSticker(int userId, PostUserStickerRequestDTO data)
    {
        if (!_userRepo.ExistsId(userId)) throw new EntityNotFoundException("User not found");
        
        // Se asume precargado
        var sticker = _stickerService.GetStickerById(data.StickerID) ?? throw new EntityNotFoundException("Sticker not found in the global catalog");

        var userSticker = new UserSticker
        {
            UserId = userId,
            Sticker = sticker,
            PublicationMode = PublicationType.DirectExchange, // O el mapeo desde el DTO si lo tenés
            Quantity = data.Quantity,
            Active = true
        };

        if (_inventoryRepo.Exists(userSticker)) throw new BusinessRuleException("Inventory already registered");

        _inventoryRepo.Add(userSticker);
        return userSticker;
    }

    public List<UserStickerResponseDTO> GetUserStickersPaginated(int userId, int page, int pageSize)
    {
        if (!_userRepo.ExistsId(userId)) throw new EntityNotFoundException("User not found");
        if (page <= 0 || pageSize <= 0) throw new BusinessRuleException("Page and PageSize must be greater than zero.");

        // Delegación de la paginación y el filtrado al repositorio de MongoDB
        var pagedStickers = _inventoryRepo.GetPaginated(page, pageSize, us => us.UserId == userId && us.Quantity > 0);

        return pagedStickers.Select(us => new UserStickerResponseDTO
        {
            Id = us.Id,
            Sticker = us.Sticker,
            UserId = us.UserId,
            PublicationMode = us.PublicationMode,
            Active = us.Active,
            Quantity = us.Quantity
        }).ToList();
    }

    public UserSticker UpdateUserSticker(int userStickerId, PublicationType? mode, int? quantity)
    {
        if (mode == null && quantity == null)
            throw new BusinessRuleException("Debe proporcionar al menos un campo para actualizar.");

        if (quantity.HasValue && quantity.Value < 0)
            throw new BusinessRuleException("La cantidad ingresada no puede ser negativa.");

        var sticker = _inventoryRepo.GetById(userStickerId) ?? throw new EntityNotFoundException("Sticker not found");

        if (mode.HasValue) 
            sticker.PublicationMode = mode.Value;

        if (quantity.HasValue) 
            sticker.Quantity = quantity.Value;

        _inventoryRepo.Update(sticker);
        return sticker;
    }

    public void DeleteUserSticker(int userStickerId)
    {
        var sticker = _inventoryRepo.GetById(userStickerId) ?? throw new EntityNotFoundException("Sticker not found");
        _inventoryRepo.Delete(userStickerId);
    }

    public List<Rate> GetAllUserRatings(int userId)
    {
        var user = _userRepo.GetById(userId) ?? throw new EntityNotFoundException("User not found");
        return user.Ratings;
    }

    public Rate CreateUserRate(int exchangeId, PostRateDTO postRateDTO, int raterId)
    {
    var exchange = _exchangeRepo.GetById(exchangeId) ?? throw new EntityNotFoundException("Exchange not found");
    
    // VALIDACIÓN CORREGIDA: Chequea contra el nuevo Enum
    if (exchange.Status != ExchangeStatus.Completed) 
        throw new BusinessRuleException("Cannot rate an incomplete exchange.");

    if (exchange.ProponentID != raterId && exchange.ProposedID != raterId)
        throw new BusinessRuleException("User did not participate in the exchange");

    int targetUserId = (exchange.ProponentID == raterId) ? exchange.ProposedID : exchange.ProponentID;

    if (raterId == targetUserId)
        throw new BusinessRuleException("You cannot rate yourself.");

    var rate = new Rate
    {
        Score = postRateDTO.Score,
        Comment = postRateDTO.Comment ?? string.Empty,
        ExchangeID = exchangeId,
        RaterID = raterId
    };

    var targetUser = _userRepo.GetById(targetUserId) ?? throw new EntityNotFoundException("Target user not found");
    targetUser.Ratings.Add(rate);
    _userRepo.Update(targetUser);

    return rate;
    }   

    public double GetUserReputation(int userId)
    {
        var user = _userRepo.GetById(userId) ?? throw new EntityNotFoundException("User not found");
        return user.Reputation;
    }

    // Utilidad privada para el hasheo de claves nativo (SHA256)
    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    public User? ValidateCredentials(PostUserDTO userDTO)
    {
        if (string.IsNullOrWhiteSpace(userDTO.Username) || string.IsNullOrWhiteSpace(userDTO.Password))
            return null;

        var user = _userRepo.GetAll().FirstOrDefault(u => u.Username == userDTO.Username);
        
        // Verificamos que exista y que el hash de la contraseña ingresada coincida con el guardado
        if (user == null || user.HashedPassword != HashPassword(userDTO.Password))
            return null; 
            
        return user; 
    }
}