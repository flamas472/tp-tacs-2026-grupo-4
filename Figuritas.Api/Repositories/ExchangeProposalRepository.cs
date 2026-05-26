using Figuritas.Shared.Model;
using MongoDB.Driver;

public class ExchangeProposalRepository
{
    private readonly IMongoCollection<ExchangeProposal> _proposals;
    private readonly IIdGenerator _idGenerator;

    public ExchangeProposalRepository(MongoDbContext context, IIdGenerator idGenerator)
    {
        _proposals = context.Collection<ExchangeProposal>("ExchangeProposals");
        _idGenerator = idGenerator;
    }

    public List<ExchangeProposal> GetAll()
    {
        return _proposals.Find(_ => true).ToList();
    }

    public void Add(ExchangeProposal proposal)
    {
        proposal.Id = _idGenerator.GetNextId<ExchangeProposal>();
        _proposals.InsertOne(proposal);
    }

    public ExchangeProposal? GetById(int proposalId)
    {
        return _proposals.Find(p => p.Id == proposalId).FirstOrDefault();
    }

    public List<ExchangeProposal> GetAllUserSentProposals(int userId)
    {
        return _proposals.Find(p => p.ProponentID == userId).ToList();
    }

    public List<ExchangeProposal> GetAllUserReceivedProposals(int userId)
    {
        return _proposals.Find(p => p.ProposedID == userId).ToList();
    }

    public void Update(ExchangeProposal proposal)
    {
        _proposals.ReplaceOne(p => p.Id == proposal.Id, proposal);
    }
}
