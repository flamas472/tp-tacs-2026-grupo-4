using Figuritas.Api.Repositories;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model;
using Figuritas.Shared.Model.Intercambios;
using Figuritas.Shared.Model.Notificaciones;

namespace Figuritas.Api.Services;

public class ExchangeProposalService
{
    private readonly IUserStickerRepository _inventoryRepo;
    private readonly IExchangeProposalRepository _exchangePropRepo;
    private readonly IMissingStickerRepository _missingStickerRepo;
    private readonly INotificationService _notificationService;
    private readonly IUserRepository _userRepo;
    private readonly IExchangeRepository _exchangeRepo;
    private readonly int _rateLimitWindowSeconds;

    public ExchangeProposalService(
        IUserStickerRepository inventoryRepo,
        IExchangeProposalRepository exchangePropRepo,
        IMissingStickerRepository missingStickerRepo,
        INotificationService notificationService,
        IUserRepository userRepo,
        IExchangeRepository exchangeRepo,
        IConfiguration configuration)
    {
        _inventoryRepo = inventoryRepo;
        _exchangePropRepo = exchangePropRepo;
        _missingStickerRepo = missingStickerRepo;
        _notificationService = notificationService;
        _userRepo = userRepo;
        _exchangeRepo = exchangeRepo;
        _rateLimitWindowSeconds = configuration.GetValue<int>("RateLimit:ExchangeProposalWindowSeconds", defaultValue: 3);
    }

    public async Task<ExchangeProposalResponseDTO> CreateExchangeProposalAsync(int callerUserId, PostExchangeProposalRequestDTO dto)
    {
        if (callerUserId == dto.ProposedUserId)
            throw new InvalidOperationException("A user cannot propose an exchange to themselves.");

        if (dto.OfferedUserStickerIds == null || dto.OfferedUserStickerIds.Count == 0)
            throw new InvalidOperationException("At least one offered sticker is required.");

        var duplicateIds = dto.OfferedUserStickerIds
            .GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Any())
            throw new InvalidOperationException(
                $"Duplicate sticker IDs are not allowed in a single proposal. Repeated ID(s): {string.Join(", ", duplicateIds)}");

        var offeredStickers = _inventoryRepo.GetMultipleById(dto.OfferedUserStickerIds);

        if (offeredStickers.Count != dto.OfferedUserStickerIds.Count)
            throw new InvalidOperationException("One or more offered stickers were not found.");

        foreach (var sticker in offeredStickers)
        {
            if (sticker.UserId != callerUserId)
                throw new InvalidOperationException("All offered stickers must belong to the proponent.");

            if (!sticker.Active || sticker.Quantity <= 0)
                throw new InvalidOperationException("All offered stickers must have quantity greater than zero.");

            if (!sticker.CanBeDirectlyExchanged)
                throw new InvalidOperationException("All offered stickers must be available for direct exchange.");
        }

        var requestedSticker = _inventoryRepo.GetById(dto.RequestedUserStickerId);
        if (requestedSticker == null)
            throw new InvalidOperationException("The requested sticker was not found.");

        if (requestedSticker.UserId != dto.ProposedUserId)
            throw new InvalidOperationException("The requested sticker does not belong to the proposed recipient.");

        if (!requestedSticker.Active)
            throw new InvalidOperationException("The requested sticker is not available for direct exchange.");

        if (!requestedSticker.CanBeDirectlyExchanged)
            throw new InvalidOperationException("The requested sticker is not available for direct exchange.");

        if (_rateLimitWindowSeconds > 0 && _exchangePropRepo.HasRecentProposal(callerUserId, _rateLimitWindowSeconds))
            throw new InvalidOperationException("Please wait a few seconds before submitting another exchange proposal.");

        var proposal = new ExchangeProposal
        {
            ProponentID = callerUserId,
            ProposedID = dto.ProposedUserId,
            RequestedUserStickerId = dto.RequestedUserStickerId,
            OfferedUserStickerIds = dto.OfferedUserStickerIds,
            State = ExchangeProposalState.Pending
        };

        _exchangePropRepo.Add(proposal);

        // Reserve stock: decrement quantity for each offered sticker
        foreach (var userSticker in offeredStickers)
        {
            userSticker.Quantity--;
            if (userSticker.Quantity <= 0)
            {
                userSticker.Active = false;
            }
            _inventoryRepo.Update(userSticker);
        }

        // Notify the recipient about the new proposal
        var callerUser = _userRepo.GetById(callerUserId);
        var callerUsername = callerUser?.Username ?? $"Usuario {callerUserId}";
        await _notificationService.SendNotificationAsync(
            dto.ProposedUserId,
            NotificationType.NewProposal,
            "Nueva propuesta de intercambio",
            $"{callerUsername} te envió una nueva propuesta de intercambio.");

        return MapToResponseDto(proposal);
    }

    public List<ExchangeProposalResponseDTO> GetAllSentProposals(int userID, GetMyProposalsDTO? dto = null)
    {
        int page = dto?.Page ?? 1;
        int pageSize = dto?.PageSize ?? 20;
        var proposals = _exchangePropRepo.GetAllUserSentProposals(userID, dto?.State, page, pageSize);
        return EnrichAndMap(proposals, callerUserId: userID);
    }

    public List<ExchangeProposalResponseDTO> GetAllReceivedProposals(int userID, GetMyProposalsDTO? dto = null)
    {
        int page = dto?.Page ?? 1;
        int pageSize = dto?.PageSize ?? 20;
        var proposals = _exchangePropRepo.GetAllUserReceivedProposals(userID, dto?.State, page, pageSize);
        return EnrichAndMap(proposals, callerUserId: userID);
    }

    /// <summary>
    /// Enriches a list of proposals with sticker and user info, and with rating state for
    /// accepted proposals. For each accepted proposal the matching Exchange record is looked up
    /// and the counterpart's Ratings subdocument is inspected to determine whether
    /// <paramref name="callerUserId"/> has already submitted a rating.
    /// </summary>
    private List<ExchangeProposalResponseDTO> EnrichAndMap(List<ExchangeProposal> proposals, int callerUserId)
    {
        var allStickerIds = proposals
            .SelectMany(p => p.OfferedUserStickerIds.Append(p.RequestedUserStickerId))
            .Distinct().ToList();

        var stickers = _inventoryRepo.GetMultipleByIdIncludingInactive(allStickerIds)
            .ToDictionary(us => us.Id);

        var userIds = proposals.SelectMany(p => new[] { p.ProponentID, p.ProposedID })
            .Distinct().ToList();
        var users = _userRepo.GetByIds(userIds).ToDictionary(u => u.Id);

        // For accepted proposals, fetch the associated Exchange and resolve rating info.
        // Ratings are stored as subdocuments on the rated user, so we look in the counterpart's Ratings list.
        var acceptedProposalIds = proposals
            .Where(p => p.State == ExchangeProposalState.Accepted)
            .Select(p => p.Id)
            .ToList();

        // Build a map: proposalId → (hasRated, stars, comment)
        var ratingInfoByProposalId = new Dictionary<int, (bool HasRated, int? Stars, string? Comment)>();

        foreach (var proposalId in acceptedProposalIds)
        {
            var exchange = _exchangeRepo.GetByProposalId(proposalId);
            if (exchange == null) continue;

            // Determine who the counterpart is from the caller's perspective
            var counterpartId = exchange.ProponentID == callerUserId
                ? exchange.ProposedID
                : exchange.ProponentID;

            // The rating submitted by caller is stored in the counterpart's Ratings list
            users.TryGetValue(counterpartId, out var counterpart);
            var existing = counterpart?.Ratings.FirstOrDefault(r =>
                r.EvaluatorUserId == callerUserId && r.ExchangeId == exchange.Id);

            ratingInfoByProposalId[proposalId] = existing is not null
                ? (true, existing.Stars, existing.Comment)
                : (false, null, null);
        }

        return proposals.Select(p =>
        {
            var dto = MapToResponseDto(p, stickers, users);
            if (ratingInfoByProposalId.TryGetValue(p.Id, out var info))
            {
                dto.HasRated      = info.HasRated;
                dto.RatingStars   = info.Stars;
                dto.RatingComment = info.Comment;
            }
            return dto;
        }).ToList();
    }

    public ExchangeProposal? GetProposalByID(int proposalID)
    {
        return _exchangePropRepo.GetById(proposalID);
    }

    public ExchangeProposalResponseDTO? GetProposalDtoByID(int proposalID)
    {
        var proposal = _exchangePropRepo.GetById(proposalID);
        if (proposal == null) return null;
        return MapToResponseDto(proposal);
    }

    public void ChangeProposalStatus(int proposalID, ExchangeProposalState newState)
    {
        var proposal = _exchangePropRepo.GetById(proposalID);
        if (proposal == null)
            throw new ArgumentException("Proposal not found.");

        // Return stock to proponent when proposal is rejected or cancelled
        if (newState == ExchangeProposalState.Rejected || newState == ExchangeProposalState.Cancelled)
        {
            // Use inclusive query to also find stickers that were deactivated by the reservation
            var offeredStickers = _inventoryRepo.GetMultipleByIdIncludingInactive(proposal.OfferedUserStickerIds);
            foreach (var userSticker in offeredStickers)
            {
                userSticker.Quantity++;
                userSticker.Active = true;
                _inventoryRepo.Update(userSticker);
            }
        }

        proposal.State = newState;
        _exchangePropRepo.Update(proposal);
    }

    public ExchangeProposal AcceptProposalAtomically(int proposalId)
    {
        var accepted = _exchangePropRepo.AcceptAtomically(proposalId);
        if (accepted == null)
            throw new InvalidOperationException("Proposal not found or is no longer pending.");
        return accepted;
    }

    private static ExchangeProposalResponseDTO MapToResponseDto(
        ExchangeProposal proposal,
        Dictionary<int, UserSticker>? stickers = null,
        Dictionary<int, User>? users = null)
    {
        UserSticker? requested = null;
        stickers?.TryGetValue(proposal.RequestedUserStickerId, out requested);

        User? proponent = null;
        User? proposed = null;
        users?.TryGetValue(proposal.ProponentID, out proponent);
        users?.TryGetValue(proposal.ProposedID, out proposed);

        return new ExchangeProposalResponseDTO
        {
            Id = proposal.Id,
            ProponentUserId = proposal.ProponentID,
            ProposedUserId = proposal.ProposedID,
            RequestedUserStickerId = proposal.RequestedUserStickerId,
            OfferedUserStickerIds = proposal.OfferedUserStickerIds,
            State = proposal.State.ToString(),
            CreatedAt = proposal.CreatedAt,
            RequestedSticker = requested == null ? null : new StickerPreviewDTO
            {
                UserStickerId = requested.Id,
                Number = requested.Sticker.Number,
                ImageUrl = requested.Sticker.ImageUrl,
                Description = requested.Sticker.Description
            },
            OfferedStickers = proposal.OfferedUserStickerIds
                .Select(id => stickers != null && stickers.TryGetValue(id, out var us)
                    ? new StickerPreviewDTO
                    {
                        UserStickerId = us.Id,
                        Number = us.Sticker.Number,
                        ImageUrl = us.Sticker.ImageUrl,
                        Description = us.Sticker.Description
                    }
                    : null)
                .Where(s => s != null)
                .Select(s => s!)
                .ToList(),
            ProponentUsername = proponent?.Username ?? string.Empty,
            ProposedUsername = proposed?.Username ?? string.Empty
        };
    }
}
