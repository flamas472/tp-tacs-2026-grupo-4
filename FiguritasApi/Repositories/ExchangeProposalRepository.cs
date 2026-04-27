using System.Collections.Concurrent;
using FiguritasApi.Model;

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
}
