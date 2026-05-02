using System.Collections.Concurrent;
using Figuritas.Shared.Model;

// Repo for in-memory persistence.
public class ExchangeProposalRepository
{
    private readonly ConcurrentBag<ExchangeProposal> proposals = new();
    private int nextId = 1;

    public List<ExchangeProposal> GetAll()
    {
        return proposals.ToList();
    }

    public void Add(ExchangeProposal proposal)
    {
        proposal.Id = Interlocked.Increment(ref nextId) - 1;
        proposals.Add(proposal);
    }

    public ExchangeProposal? GetById(int proposalId)
    {
        return proposals.FirstOrDefault(p => p.Id == proposalId);
    }

    public List<ExchangeProposal> GetAllUserSentProposals(int userId)
    {
        return proposals.Where(p => p.ProponentID == userId).ToList();
    }

    public List<ExchangeProposal> GetAllUserReceivedProposals(int userId)
    {
        return proposals.Where(p => p.ProposedID == userId).ToList();
    }

    public void Update(ExchangeProposal proposal)
    {
        var existingProposal = GetById(proposal.Id);
        if (existingProposal != null)
        {
            proposals.TryTake(out existingProposal);
            proposals.Add(proposal);
        }
    }

}
      
