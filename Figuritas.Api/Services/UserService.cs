using BCrypt.Net;
using Figuritas.Api.Repositories;
using Figuritas.Shared.DTO;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model;
using Figuritas.Shared.Model.Notificaciones;
using Figuritas.Shared.Utils;
using MongoDB.Driver;

namespace Figuritas.Api.Services;


public class UserService(
    IUserRepository userRepo,
    IUserStickerRepository inventoryRepo,
    StickerService stickerService,
    IExchangeRepository exchangeRepo,
    IMissingStickerRepository missingStickerRepo,
    INotificationService notificationService
    )
{
    private readonly IUserStickerRepository _inventoryRepo = inventoryRepo;
    private readonly IUserRepository _userRepo = userRepo;
    private readonly StickerService _stickerService = stickerService;
    private readonly IExchangeRepository _exchangeRepo = exchangeRepo;
    private readonly IMissingStickerRepository _missingStickerRepo = missingStickerRepo;
    private readonly INotificationService _notificationService = notificationService;

    public User? GetUserById(int id) => _userRepo.GetById(id);

    public User? GetUserByUsername(string username) => _userRepo.GetByUsername(username);

    public User CreateUser(PostUserDTO userDTO)
    {
        var username = userDTO.Username;
        var password = userDTO.Password;

        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required");
        if (_userRepo.GetByUsername(username) != null)
            throw new ArgumentException("Username already exists");
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password is required");

        var user = new User
        {
            Username = username,
            HashedPassword = BCrypt.Net.BCrypt.HashPassword(password)
        };

        try
        {
            _userRepo.Add(user);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new ArgumentException("Username already exists");
        }

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

        var user = _userRepo.GetByUsername(username);
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

    public async Task<UserSticker> CreateUserStickerAsync(int userId, PostUserStickerRequestDTO data)
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

        // Notify users who have this catalog sticker as missing (US01 trigger)
        var interestedUserIds = await _missingStickerRepo.GetUserIdsForStickerAsync(data.StickerId);
        foreach (var interestedUserId in interestedUserIds)
        {
            if (interestedUserId == userId)
                continue;

            await _notificationService.SendNotificationAsync(
                interestedUserId,
                NotificationType.MissingStickerAvailable,
                "Missing Sticker Now Available",
                $"Sticker #{sticker.Number} ({sticker.Description}) is now available for exchange.",
                referenceId: userSticker.Id);
        }

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

    public List<MarketStickerResponseDTO> GetUserStickersByUserId(int userId, GetUserStickersDTO dto)
    {
        if (!_userRepo.ExistsId(userId))
            throw new ArgumentException("User not found");

        var filter = dto.ToPredicate(userId);
        return _inventoryRepo.GetPaginated(dto.Page, dto.PageSize, filter)
            .Select(us => new MarketStickerResponseDTO
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

    public UserSticker UpdateUserSticker(int stickerId, bool? canBeDirectlyExchanged, bool? canBeAuctioned, int? quantity, int authenticatedUserId)
    {
        var sticker = _inventoryRepo.GetById(stickerId);
        if (sticker == null)
            throw new ArgumentException("Sticker not found");

        if (sticker.UserId != authenticatedUserId)
            throw new UnauthorizedAccessException("You do not own this sticker resource.");

        if (canBeDirectlyExchanged.HasValue)
            sticker.CanBeDirectlyExchanged = canBeDirectlyExchanged.Value;

        if (canBeAuctioned.HasValue)
            sticker.CanBeAuctioned = canBeAuctioned.Value;

        if (quantity.HasValue)
            sticker.Quantity = quantity.Value;

        _inventoryRepo.Update(sticker);
        return sticker;
    }

    public void DeleteUserSticker(int userStickerId, int authenticatedUserId)
    {
        var sticker = _inventoryRepo.GetById(userStickerId);
        if (sticker == null)
            throw new ArgumentException("Sticker not found");

        if (sticker.UserId != authenticatedUserId)
            throw new UnauthorizedAccessException("You do not own this sticker resource.");

        _inventoryRepo.Delete(userStickerId);
    }

    public List<RatingResponseDTO> GetAllUserRatings(int userId, int page, int pageSize)
    {
        User? user = _userRepo.GetById(userId);
        if (user == null) throw new ArgumentException("User not found");

        return user.Ratings
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RatingResponseDTO
            {
                Id = r.Id,
                ExchangeId = r.ExchangeId,
                EvaluatorUserId = r.EvaluatorUserId,
                TargetUserId = r.TargetUserId,
                Stars = r.Stars,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            })
            .ToList();
    }

    public Rate CreateUserRate(PostRatingRequestDTO dto, int raterId)
    {
        // Guard 1: Anti-self-rating
        if (raterId == dto.TargetUserId)
            throw new ArgumentException("Self-rating is not allowed");

        var exchange = _exchangeRepo.GetById(dto.ExchangeId);

        // Guard 2 (also covers not found): Exchange must exist
        if (exchange == null)
            throw new ArgumentException("Exchange not found");

        // Guard 3 (state): Exchange entity only exists once a proposal has been accepted and
        // the trade was completed — there is no State field because the record itself is proof
        // of finalization. No additional state check is required; reaching this point means
        // the exchange is finalized by definition.

        // Guard 5: Rater must be a participant in the exchange
        if (exchange.ProponentID != raterId && exchange.ProposedID != raterId)
            throw new ArgumentException("Not participant");

        // Guard 6: TargetUserId must be the other participant
        var expectedTargetId = exchange.ProponentID == raterId ? exchange.ProposedID : exchange.ProponentID;
        if (dto.TargetUserId != expectedTargetId)
            throw new ArgumentException("Not participant");

        var targetUser = _userRepo.GetById(dto.TargetUserId)
            ?? throw new ArgumentException("Rated user not found");

        // Guard 7: Anti-duplicate — one rating per rater per exchange
        bool alreadyRated = targetUser.Ratings.Any(r =>
            r.EvaluatorUserId == raterId && r.ExchangeId == dto.ExchangeId);
        if (alreadyRated)
            throw new ArgumentException("Already rated");

        var rate = new Rate
        {
            Stars = dto.Stars,
            Comment = dto.Comment,
            ExchangeId = dto.ExchangeId,
            EvaluatorUserId = raterId,
            TargetUserId = dto.TargetUserId,
            CreatedAt = DateTime.UtcNow
        };

        targetUser.Ratings.Add(rate);
        _userRepo.Update(targetUser);

        return rate;
    }

    public double GetUserReputation(int userId)
    {
        User? user = _userRepo.GetById(userId);
        if (user == null) throw new ArgumentException("User not found");

        return user.Reputation;
    }
}
