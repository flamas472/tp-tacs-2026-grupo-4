using FiguritasApi.Shared.Model;

namespace FiguritasApi.Services;

public class ExchangeProposalService
{
    private readonly UserStickerRepository _inventoryRepo;

    private readonly ExchangeProposalRepository _exchangePropRepo;

    public ExchangeProposalService(UserStickerRepository inventoryRepo, ExchangeProposalRepository exchangePropRepo)
    {
        _inventoryRepo = inventoryRepo;
        _exchangePropRepo = exchangePropRepo;
    }

    public ExchangeProposal CreateExchangeProposal(int proponentID, int proposedID, List<int> offeredStickersID, int requestedStickerID)
    {
        var offered = offeredStickersID
            .Select(id => _inventoryRepo.GetById(id))
            .ToList();

        var requested = _inventoryRepo.GetById(requestedStickerID);   

        var proposal = new ExchangeProposal
        {
            Id = 0,
            ProponentID = proponentID,
            ProposedID = proposedID,
            OfferedStickers = offered,
            RequestedSticker = requested,
            State = ExchangeProposalState.Pending
        };

        if (!proposal.IsValid())
        {
            return null;
        }

        _exchangePropRepo.Add(proposal);

        return proposal;
    }

    public List<ExchangeProposal> GetAllSentProposals(int userID)
    {
        return _exchangePropRepo.GetAllUserSentProposals(userID)
                                .Where(p => p.State == ExchangeProposalState.Pending).ToList();
    }

    public List<ExchangeProposal> GetAllReceivedProposals(int userID)
    {
        return _exchangePropRepo.GetAllUserReceivedProposals(userID)
                                .Where(p => p.State == ExchangeProposalState.Pending).ToList();
    }

    public ExchangeProposal GetProposalByID(int proposalID)
    {
        return _exchangePropRepo.GetById(proposalID);
    }

    public void ChangeProposalStatus(int proposalID, ExchangeProposalState newState)
    {
        var proposal = _exchangePropRepo.GetById(proposalID);
        if (proposal == null)
            throw new ArgumentException("Propuesta no encontrada");

        proposal.State = newState;
        _exchangePropRepo.Update(proposal);
    }
}



