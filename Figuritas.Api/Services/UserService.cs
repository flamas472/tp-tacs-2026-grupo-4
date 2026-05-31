using BCrypt.Net;
using Figuritas.Api.Repositories;
using Figuritas.Shared.DTO;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model;
using Figuritas.Shared.Utils;

namespace Figuritas.Api.Services;

public class UserService(
    IUserRepository userRepo,
    IUserStickerRepository inventoryRepo,
    StickerService stickerService,
    IExchangeRepository exchangeRepo,
    IMissingStickerRepository missingStickerRepo
    )
{
    private readonly IUserStickerRepository _inventoryRepo = inventoryRepo;
    private readonly IUserRepository _userRepo = userRepo;
    private readonly StickerService _stickerService = stickerService;
    private readonly IExchangeRepository _exchangeRepo = exchangeRepo;
    private readonly IMissingStickerRepository _missingStickerRepo = missingStickerRepo;

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

    public async Task<MissingSticker> AddMissingStickerToUser(int userId, int stickerId)
    {
        if (!_userRepo.ExistsId(userId))
            throw new ArgumentException("User not found");

        var sticker = _stickerService.GetById(stickerId);
        if (sticker == null)
            throw new ArgumentException("Sticker not found in catalog");

        if (await _missingStickerRepo.ExistsAsync(userId, stickerId))
            throw new ArgumentException("Missing sticker already registered");

        var missingSticker = new MissingSticker
        {
            UserId = userId,
            StickerId = stickerId,
            RegisteredAt = DateTime.UtcNow
        };

        await _missingStickerRepo.AddAsync(missingSticker);
        return missingSticker;
    }

    public async Task<List<MissingSticker>> GetMissingStickers(int userId)
    {
        if (!_userRepo.ExistsId(userId))
            throw new ArgumentException("User not found");
        return await _missingStickerRepo.GetByUserIdAsync(userId);
    }

    public async Task<bool> RemoveMissingSticker(int userId, int stickerId)
    {
        if (!_userRepo.ExistsId(userId))
            throw new ArgumentException("User not found");
        return await _missingStickerRepo.DeleteAsync(userId, stickerId);
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

    public List<MyPublishedStickerResponseDTO> GetMyPublishedStickers(GetMyStickersDTO dto, int callerUserId)
    {
        return _inventoryRepo.GetByUserIdPaginated(callerUserId, dto.Page, dto.PageSize)
            .Select(us => new MyPublishedStickerResponseDTO
            {
                StickerId = us.Id,
                Number = us.Sticker.Number,
                Team = us.Sticker.Team,
                Player = us.Sticker.Description,
                Quantity = us.Quantity,
                CanBeDirectlyExchanged = us.CanBeDirectlyExchanged,
                CanBeAuctioned = us.CanBeAuctioned
            })
            .ToList();
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

    public List<MarketStickerResponseDTO> SearchMarketStickers(GetMarketStickersDTO dto, int callerUserId)
    {
        var filter = dto.ToPredicate(callerUserId);
        var userStickers = _inventoryRepo.GetPaginated(dto.Page, dto.PageSize, filter, sortDescending: true);

        return userStickers.Select(us => new MarketStickerResponseDTO
        {
            UserStickerId = us.Id,
            OwnerId = us.UserId,
            StickerNumber = us.Sticker.Number,
            StickerNationalTeam = us.Sticker.NationalTeam,
            StickerTeam = us.Sticker.Team,
            StickerCategory = us.Sticker.Category,
            StickerDescription = us.Sticker.Description,
            StickerImageUrl = us.Sticker.ImageUrl,
            Quantity = us.Quantity,
            CanBeDirectlyExchanged = us.CanBeDirectlyExchanged,
            CanBeAuctioned = us.CanBeAuctioned
        }).ToList();
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
