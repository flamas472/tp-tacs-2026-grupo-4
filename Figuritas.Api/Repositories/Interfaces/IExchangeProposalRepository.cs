using Figuritas.Shared.Model;

namespace Figuritas.Api.Repositories;

public interface IExchangeProposalRepository
{
    List<ExchangeProposal> GetAll();
    void Add(ExchangeProposal proposal);
    ExchangeProposal? GetById(int proposalId);
    List<ExchangeProposal> GetAllUserSentProposals(int userId);
    List<ExchangeProposal> GetAllUserReceivedProposals(int userId);
    void Update(ExchangeProposal proposal);
    ExchangeProposal? AcceptAtomically(int proposalId);
}
