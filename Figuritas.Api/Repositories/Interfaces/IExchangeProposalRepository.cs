using Figuritas.Shared.Model.Intercambios;

namespace Figuritas.Api.Repositories;

public interface IExchangeProposalRepository
{
    List<ExchangeProposal> GetAll();
    void Add(ExchangeProposal proposal);
    ExchangeProposal? GetById(int proposalId);
    List<ExchangeProposal> GetAllUserSentProposals(int userId, ExchangeProposalState? state = null, int page = 1, int pageSize = 20);
    List<ExchangeProposal> GetAllUserReceivedProposals(int userId, ExchangeProposalState? state = null, int page = 1, int pageSize = 20);
    void Update(ExchangeProposal proposal);
    ExchangeProposal? AcceptAtomically(int proposalId);
}
