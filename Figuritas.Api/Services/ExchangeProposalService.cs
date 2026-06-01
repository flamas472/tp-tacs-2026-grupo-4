using Figuritas.Api.Repositories;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model.Intercambios;
using Figuritas.Shared.Model.Notificaciones;

namespace Figuritas.Api.Services;

public class ExchangeProposalService
{
    private readonly IUserStickerRepository _inventoryRepo;
    private readonly IExchangeProposalRepository _exchangePropRepo;
    private readonly IMissingStickerRepository _missingStickerRepo;
    private readonly INotificationService _notificationService;

    public ExchangeProposalService(
        IUserStickerRepository inventoryRepo,
        IExchangeProposalRepository exchangePropRepo,
        IMissingStickerRepository missingStickerRepo,
        INotificationService notificationService)
    {
        _inventoryRepo = inventoryRepo;
        _exchangePropRepo = exchangePropRepo;
        _missingStickerRepo = missingStickerRepo;
        _notificationService = notificationService;
    }

    public async Task<ExchangeProposalResponseDTO> CreateExchangeProposalAsync(int callerUserId, PostExchangeProposalRequestDTO dto)
    {
        if (callerUserId == dto.ProposedUserId)
            throw new InvalidOperationException("A user cannot propose an exchange to themselves.");

        if (dto.OfferedUserStickerIds == null || dto.OfferedUserStickerIds.Count == 0)
            throw new InvalidOperationException("At least one offered sticker is required.");

        var offeredStickers = _inventoryRepo.GetMultipleById(dto.OfferedUserStickerIds);

        if (offeredStickers.Count != dto.OfferedUserStickerIds.Count)
            throw new InvalidOperationException("One or more offered stickers were not found.");

        foreach (var sticker in offeredStickers)
        {
            if (sticker.UserId != callerUserId)
                throw new InvalidOperationException("All offered stickers must belong to the proponent.");

            if (sticker.Quantity <= 0)
                throw new InvalidOperationException("All offered stickers must have quantity greater than zero.");

            if (!sticker.CanBeDirectlyExchanged)
                throw new InvalidOperationException("All offered stickers must be available for direct exchange.");
        }

        var requestedSticker = _inventoryRepo.GetById(dto.RequestedUserStickerId);
        if (requestedSticker == null)
            throw new InvalidOperationException("The requested sticker was not found.");

        if (requestedSticker.UserId != dto.ProposedUserId)
            throw new InvalidOperationException("The requested sticker does not belong to the proposed recipient.");

        if (!requestedSticker.CanBeDirectlyExchanged)
            throw new InvalidOperationException("The requested sticker is not available for direct exchange.");

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
        await _notificationService.SendNotificationAsync(
            dto.ProposedUserId,
            NotificationType.NewProposal,
            "New Exchange Proposal",
            $"User {callerUserId} sent you a new exchange proposal.");

        return MapToResponseDto(proposal);
    }

    public List<ExchangeProposalResponseDTO> GetAllSentProposals(int userID, GetMyProposalsDTO? dto = null)
    {
        int page = dto?.Page ?? 1;
        int pageSize = dto?.PageSize ?? 20;

        return _exchangePropRepo.GetAllUserSentProposals(userID, dto?.State, page, pageSize)
                                .Select(MapToResponseDto)
                                .ToList();
    }

    public List<ExchangeProposalResponseDTO> GetAllReceivedProposals(int userID, GetMyProposalsDTO? dto = null)
    {
        int page = dto?.Page ?? 1;
        int pageSize = dto?.PageSize ?? 20;

        return _exchangePropRepo.GetAllUserReceivedProposals(userID, dto?.State, page, pageSize)
                                .Select(MapToResponseDto)
                                .ToList();
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

    private static ExchangeProposalResponseDTO MapToResponseDto(ExchangeProposal proposal)
    {
        return new ExchangeProposalResponseDTO
        {
            Id = proposal.Id,
            ProponentUserId = proposal.ProponentID,
            ProposedUserId = proposal.ProposedID,
            RequestedUserStickerId = proposal.RequestedUserStickerId,
            OfferedUserStickerIds = proposal.OfferedUserStickerIds,
            State = proposal.State.ToString(),
            CreatedAt = proposal.CreatedAt
        };
    }
}
