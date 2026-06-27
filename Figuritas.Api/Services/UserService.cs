using BCrypt.Net;
using Figuritas.Api.Exceptions;
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
    INotificationService notificationService,
    IExchangeProposalRepository exchangeProposalRepo,
    IAuctionRepository auctionRepo
    )
{
    private readonly IUserStickerRepository _inventoryRepo = inventoryRepo;
    private readonly IUserRepository _userRepo = userRepo;
    private readonly StickerService _stickerService = stickerService;
    private readonly IExchangeRepository _exchangeRepo = exchangeRepo;
    private readonly IMissingStickerRepository _missingStickerRepo = missingStickerRepo;
    private readonly INotificationService _notificationService = notificationService;
    private readonly IExchangeProposalRepository _exchangeProposalRepo = exchangeProposalRepo;
    private readonly IAuctionRepository _auctionRepo = auctionRepo;

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
            HashedPassword = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTime.UtcNow
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

        // Upsert: if the user already has this catalog sticker, increment quantity instead of creating.
        var existing = await _inventoryRepo.GetByUserIdAndCatalogIdIncludingInactiveAsync(userId, data.StickerId);
        if (existing != null)
        {
            // Retry loop for optimistic concurrency (same pattern as UpdateUserSticker).
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                var fresh = await _inventoryRepo.GetByUserIdAndCatalogIdIncludingInactiveAsync(userId, data.StickerId)
                    ?? throw new ArgumentException("Sticker not found in inventory");

                fresh.Quantity += data.Quantity;
                fresh.Active = true;
                fresh.CanBeDirectlyExchanged = data.CanBeDirectlyExchanged;
                fresh.CanBeAuctioned = data.CanBeAuctioned;

                try
                {
                    _inventoryRepo.Update(fresh);
                    // Send notifications only if the sticker was previously inactive (reactivated).
                    if (!existing.Active)
                    {
                        var reactivatedInterestedUserIds = await _missingStickerRepo.GetUserIdsForStickerAsync(data.StickerId);
                        foreach (var interestedUserId in reactivatedInterestedUserIds)
                        {
                            if (interestedUserId == userId) continue;
                            await _notificationService.SendNotificationAsync(
                                interestedUserId,
                                NotificationType.MissingStickerAvailable,
                                "Figurita faltante disponible",
                                $"Alguien publicó como repetida la figurita #{sticker.Number} ({sticker.Description}) que te falta. ¡No dejes pasar la oportunidad de proponer un intercambio!",
                                referenceId: fresh.Id);
                        }
                    }
                    return fresh;
                }
                catch (OptimisticConcurrencyException) when (attempt < 3) { }
            }
            throw new OptimisticConcurrencyException($"UserSticker could not be updated after 3 attempts.");
        }

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
                "Figurita faltante disponible",
                $"Alguien publicó como repetida la figurita #{sticker.Number} ({sticker.Description}) que te falta. ¡No dejes pasar la oportunidad de proponer un intercambio!",
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
                StickerId = us.Sticker.Id,
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
        List<int>? allowedOwnerIds = null;
        if (dto.MinReputation != null)
        {
            allowedOwnerIds = _userRepo.GetAll()
                .Where(u => u.Reputation >= dto.MinReputation.Value)
                .Select(u => u.Id)
                .ToList();
        }

        var filter = dto.ToPredicate(callerUserId, allowedOwnerIds);
        var userStickers = _inventoryRepo.GetPaginated(dto.Page, dto.PageSize, filter, sortDescending: true);

        // Resolve all owner data in a single batch query to avoid N+1 calls.
        var ownerIds = userStickers.Select(us => us.UserId).Distinct().ToList();
        var ownerMap = _userRepo.GetByIds(ownerIds)
            .ToDictionary(u => u.Id);

        return userStickers.Select(us =>
        {
            ownerMap.TryGetValue(us.UserId, out var owner);
            return new MarketStickerResponseDTO
            {
                UserStickerId = us.Id,
                OwnerId = us.UserId,
                StickerId = us.Sticker.Id,
                StickerNumber = us.Sticker.Number,
                StickerNationalTeam = us.Sticker.NationalTeam,
                StickerTeam = us.Sticker.Team,
                StickerCategory = us.Sticker.Category,
                StickerDescription = us.Sticker.Description,
                StickerImageUrl = us.Sticker.ImageUrl,
                Quantity = us.Quantity,
                CanBeDirectlyExchanged = us.CanBeDirectlyExchanged,
                CanBeAuctioned = us.CanBeAuctioned,
                OwnerUsername = owner?.Username ?? string.Empty,
                OwnerReputation = owner?.Reputation ?? 0
            };
        }).ToList();
    }

    public async Task<UserSticker> UpdateUserSticker(int stickerId, bool? canBeDirectlyExchanged, bool? canBeAuctioned, int? quantity, int authenticatedUserId)
    {
        var sticker = _inventoryRepo.GetById(stickerId);
        if (sticker == null)
            throw new ArgumentException("Sticker not found");

        if (sticker.UserId != authenticatedUserId)
            throw new UnauthorizedAccessException("You do not own this sticker resource.");

        // TAREA 7: Guard quantity reduction when the sticker has active reservations.
        if (quantity.HasValue)
        {
            if (await _exchangeProposalRepo.HasActivePendingProposalForOfferedStickerAsync(stickerId))
                throw new InvalidOperationException(
                    "Cannot modify quantity: sticker is committed to a pending exchange proposal.");

            if (await _auctionRepo.HasActiveAuctionForUserStickerAsync(stickerId))
                throw new InvalidOperationException(
                    "Cannot modify quantity: sticker is listed in an active auction.");
        }

        // TAREA 8: Optimistic concurrency retry loop — up to 3 attempts.
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var fresh = _inventoryRepo.GetById(stickerId)
                ?? throw new ArgumentException("Sticker not found");

            if (fresh.UserId != authenticatedUserId)
                throw new UnauthorizedAccessException("You do not own this sticker resource.");

            if (canBeDirectlyExchanged.HasValue)
                fresh.CanBeDirectlyExchanged = canBeDirectlyExchanged.Value;

            if (canBeAuctioned.HasValue)
                fresh.CanBeAuctioned = canBeAuctioned.Value;

            if (quantity.HasValue)
                fresh.Quantity = quantity.Value;

            try
            {
                _inventoryRepo.Update(fresh);
                return fresh;
            }
            catch (OptimisticConcurrencyException) when (attempt < 3)
            {
                // Concurrent write detected — re-read and retry.
            }
        }

        throw new OptimisticConcurrencyException(
            $"UserSticker {stickerId} could not be updated after 3 attempts due to concurrent modifications.");
    }

    public async Task DeleteUserSticker(int userStickerId, int authenticatedUserId)
    {
        var sticker = _inventoryRepo.GetById(userStickerId);
        if (sticker == null)
            throw new ArgumentException("Sticker not found");

        if (sticker.UserId != authenticatedUserId)
            throw new UnauthorizedAccessException("You do not own this sticker resource.");

        // TAREA 7: Prevent deletion while sticker has active reservations.
        if (await _exchangeProposalRepo.HasActivePendingProposalForOfferedStickerAsync(userStickerId))
            throw new InvalidOperationException(
                "Cannot delete sticker: it is committed to a pending exchange proposal.");

        if (await _auctionRepo.HasActiveAuctionForUserStickerAsync(userStickerId))
            throw new InvalidOperationException(
                "Cannot delete sticker: it is listed in an active auction.");

        _inventoryRepo.Delete(userStickerId);
    }

    public List<RatingResponseDTO> GetAllUserRatings(int userId, int page, int pageSize)
    {
        User? user = _userRepo.GetById(userId);
        if (user == null) throw new ArgumentException("User not found");

        var pageRatings = user.Ratings
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var evaluatorIds = pageRatings.Select(r => r.EvaluatorUserId).Distinct().ToList();
        var evaluatorMap = _userRepo.GetByIds(evaluatorIds)
            .ToDictionary(u => u.Id, u => u.Username);

        return pageRatings
            .Select(r => new RatingResponseDTO
            {
                Id = r.Id,
                ExchangeId = r.ExchangeId,
                EvaluatorUserId = r.EvaluatorUserId,
                TargetUserId = r.TargetUserId,
                Stars = r.Stars,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt,
                EvaluatorUsername = evaluatorMap.GetValueOrDefault(r.EvaluatorUserId, string.Empty)
            })
            .ToList();
    }

    public async Task<Rate> CreateUserRate(PostRatingRequestDTO dto, int raterId)
    {
        // Guard 1: Anti-self-rating
        if (raterId == dto.TargetUserId)
            throw new ArgumentException("Self-rating is not allowed");

        var exchange = _exchangeRepo.GetById(dto.ExchangeId);

        // Guard 2 (also covers not found): Exchange must exist
        if (exchange == null)
            throw new ArgumentException("Exchange not found");

        // Guard 5: Rater must be a participant in the exchange
        if (exchange.ProponentID != raterId && exchange.ProposedID != raterId)
            throw new ArgumentException("Not participant");

        // Guard 6: TargetUserId must be the other participant
        var expectedTargetId = exchange.ProponentID == raterId ? exchange.ProposedID : exchange.ProponentID;
        if (dto.TargetUserId != expectedTargetId)
            throw new ArgumentException("Not participant");

        // Guard: target user must exist
        if (_userRepo.GetById(dto.TargetUserId) == null)
            throw new ArgumentException("Rated user not found");

        var rate = new Rate
        {
            Stars = dto.Stars,
            Comment = dto.Comment,
            ExchangeId = dto.ExchangeId,
            EvaluatorUserId = raterId,
            TargetUserId = dto.TargetUserId,
            CreatedAt = DateTime.UtcNow
        };

        // TAREA 3: Atomic $push conditioned on absence of (raterUserId, exchangeId) pair.
        // Guards against concurrent duplicate ratings without a read-modify-write race.
        var added = await _userRepo.TryAddRatingAsync(dto.TargetUserId, rate, raterId, dto.ExchangeId);
        if (!added)
            throw new ArgumentException("Already rated");

        return rate;
    }

    public double GetUserReputation(int userId)
    {
        User? user = _userRepo.GetById(userId);
        if (user == null) throw new ArgumentException("User not found");

        return user.Reputation;
    }

    public long GetCompletedExchangesCount(int userId)
    {
        if (!_userRepo.ExistsId(userId))
            throw new ArgumentException("User not found");

        return _exchangeRepo.CountByUserId(userId);
    }
}
