using Figuritas.Api.Repositories;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model.Intercambios;

namespace Figuritas.Api.Services;

public class ExchangeProposalService
{
    private readonly IUserStickerRepository _inventoryRepo;
    private readonly IExchangeProposalRepository _exchangePropRepo;

    public ExchangeProposalService(IUserStickerRepository inventoryRepo, IExchangeProposalRepository exchangePropRepo)
    {
        _inventoryRepo = inventoryRepo;
        _exchangePropRepo = exchangePropRepo;
    }

    public ExchangeProposalResponseDTO CreateExchangeProposal(int callerUserId, PostExchangeProposalRequestDTO dto)
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

            if (!sticker.CanBeExchanged)
                throw new InvalidOperationException("All offered stickers must be available for direct exchange.");
        }

        var requestedSticker = _inventoryRepo.GetById(dto.RequestedUserStickerId);
        if (requestedSticker == null)
            throw new InvalidOperationException("The requested sticker was not found.");

        if (!requestedSticker.CanBeExchanged)
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

        return MapToResponseDto(proposal);
    }

    public List<ExchangeProposalResponseDTO> GetAllSentProposals(int userID)
    {
        return _exchangePropRepo.GetAllUserSentProposals(userID)
                                .Where(p => p.State == ExchangeProposalState.Pending)
                                .Select(MapToResponseDto)
                                .ToList();
    }

    public List<ExchangeProposalResponseDTO> GetAllReceivedProposals(int userID)
    {
        return _exchangePropRepo.GetAllUserReceivedProposals(userID)
                                .Where(p => p.State == ExchangeProposalState.Pending)
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
