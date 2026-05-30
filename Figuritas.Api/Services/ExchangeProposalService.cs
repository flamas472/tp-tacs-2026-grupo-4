using Figuritas.Api.Repositories;
using Figuritas.Shared.Model;

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

    public ExchangeProposal CreateExchangeProposal(int proponentID, int proposedID, List<int> offeredStickersID, int requestedStickerID)
    {
        var offered = offeredStickersID
            .Select(id => _inventoryRepo.GetById(id))
            .Where(s => s != null)
            .Select(s => s!)
            .ToList();

        var requested = _inventoryRepo.GetById(requestedStickerID)!;

        var proposal = new ExchangeProposal
        {
            ProponentID = proponentID,
            ProposedID = proposedID,
            OfferedStickers = offered,
            RequestedSticker = requested,
            State = ExchangeProposalState.Pending
        };

        if (!proposal.IsValid())
        {
            throw new InvalidOperationException("Proposal is invalid: verify that offered stickers belong to the proponent, are active and exchangeable, and the requested sticker belongs to the proposed user.");
        }

        _exchangePropRepo.Add(proposal);

        return proposal;
    }

    public List<ExchangeProposal> GetAllSentProposals(int userID)
    {
        return _exchangePropRepo.GetAllUserSentProposals(userID);
    }

    public List<ExchangeProposal> GetAllReceivedProposals(int userID)
    {
        return _exchangePropRepo.GetAllUserReceivedProposals(userID);
    }

    public ExchangeProposal? GetProposalByID(int proposalID)
    {
        return _exchangePropRepo.GetById(proposalID);
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
}
