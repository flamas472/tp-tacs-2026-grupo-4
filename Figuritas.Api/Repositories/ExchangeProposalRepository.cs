using Figuritas.Shared.Model.Intercambios;
using MongoDB.Driver;

namespace Figuritas.Api.Repositories;

public class ExchangeProposalRepository : IExchangeProposalRepository
{
    private readonly IMongoCollection<ExchangeProposal> _proposals;
    private readonly IIdGenerator _idGenerator;

    public ExchangeProposalRepository(MongoDbContext context, IIdGenerator idGenerator)
    {
        _proposals = context.Collection<ExchangeProposal>("ExchangeProposals");
        _idGenerator = idGenerator;
        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        // Index: ProponentID — accelerates sent-proposal queries (dashboard)
        _proposals.Indexes.CreateOne(new CreateIndexModel<ExchangeProposal>(
            Builders<ExchangeProposal>.IndexKeys.Ascending(p => p.ProponentID),
            new CreateIndexOptions { Name = "idx_proposal_proponent" }));

        // Index: ProposedID — accelerates received-proposal queries (dashboard)
        _proposals.Indexes.CreateOne(new CreateIndexModel<ExchangeProposal>(
            Builders<ExchangeProposal>.IndexKeys.Ascending(p => p.ProposedID),
            new CreateIndexOptions { Name = "idx_proposal_proposed" }));

        // Index: State — accelerates state-filtered queries and analytics.
        // No explicit name: lets MongoDB use the auto-generated name "State_1" which may
        // already exist from AnalyticsRepository. Omitting the name ensures idempotency.
        _proposals.Indexes.CreateOne(new CreateIndexModel<ExchangeProposal>(
            Builders<ExchangeProposal>.IndexKeys.Ascending(p => p.State)));

        // Index: CreatedAt — accelerates chronological ordering and rate-limit guard queries
        _proposals.Indexes.CreateOne(new CreateIndexModel<ExchangeProposal>(
            Builders<ExchangeProposal>.IndexKeys.Ascending(p => p.CreatedAt),
            new CreateIndexOptions { Name = "idx_proposal_created_at" }));
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

    public List<ExchangeProposal> GetAllUserSentProposals(int userId, ExchangeProposalState? state = null, int page = 1, int pageSize = 20)
    {
        var filter = Builders<ExchangeProposal>.Filter.Eq(p => p.ProponentID, userId);
        if (state.HasValue)
            filter &= Builders<ExchangeProposal>.Filter.Eq(p => p.State, state.Value);
        return _proposals.Find(filter).Skip((page - 1) * pageSize).Limit(pageSize).ToList();
    }

    public List<ExchangeProposal> GetAllUserReceivedProposals(int userId, ExchangeProposalState? state = null, int page = 1, int pageSize = 20)
    {
        var filter = Builders<ExchangeProposal>.Filter.Eq(p => p.ProposedID, userId);
        if (state.HasValue)
            filter &= Builders<ExchangeProposal>.Filter.Eq(p => p.State, state.Value);
        return _proposals.Find(filter).Skip((page - 1) * pageSize).Limit(pageSize).ToList();
    }

    public void Update(ExchangeProposal proposal)
    {
        _proposals.ReplaceOne(p => p.Id == proposal.Id, proposal);
    }

    public ExchangeProposal? AcceptAtomically(int proposalId)
    {
        var filter = Builders<ExchangeProposal>.Filter.And(
            Builders<ExchangeProposal>.Filter.Eq(p => p.Id, proposalId),
            Builders<ExchangeProposal>.Filter.Eq(p => p.State, ExchangeProposalState.Pending)
        );
        var update = Builders<ExchangeProposal>.Update
            .Set(p => p.State, ExchangeProposalState.Accepted);
        var options = new FindOneAndUpdateOptions<ExchangeProposal>
        {
            ReturnDocument = ReturnDocument.After
        };
        return _proposals.FindOneAndUpdate(filter, update, options);
    }

    public bool HasRecentProposal(int senderUserId, int windowSeconds)
    {
        var threshold = DateTime.UtcNow.AddSeconds(-windowSeconds);
        var filter = Builders<ExchangeProposal>.Filter.And(
            Builders<ExchangeProposal>.Filter.Eq(p => p.ProponentID, senderUserId),
            Builders<ExchangeProposal>.Filter.Gt(p => p.CreatedAt, threshold)
        );
        return _proposals.CountDocuments(filter) > 0;
    }
}
