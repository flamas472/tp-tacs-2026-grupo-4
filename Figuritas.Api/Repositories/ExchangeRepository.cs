using Figuritas.Shared.Model.Intercambios;
using MongoDB.Driver;

namespace Figuritas.Api.Repositories;

public class ExchangeRepository : IExchangeRepository
{
    private readonly IMongoCollection<Exchange> _exchanges;
    private readonly IIdGenerator _idGenerator;

    public ExchangeRepository(MongoDbContext context, IIdGenerator idGenerator)
    {
        _exchanges = context.Collection<Exchange>("Exchanges");
        _idGenerator = idGenerator;
    }

    public List<Exchange> GetAll()
    {
        return _exchanges.Find(_ => true).ToList();
    }

    public void Add(Exchange exchange)
    {
        exchange.Id = _idGenerator.GetNextId<Exchange>();
        _exchanges.InsertOne(exchange);
    }

    public void Add(Exchange exchange, IClientSessionHandle session)
    {
        exchange.Id = _idGenerator.GetNextId<Exchange>();
        _exchanges.InsertOne(session, exchange);
    }

    public Exchange? GetById(int exchangeId)
    {
        return _exchanges.Find(e => e.Id == exchangeId).FirstOrDefault();
    }

    public Exchange? GetByProposalId(int proposalId)
    {
        return _exchanges.Find(e => e.ExchangeProposalID == proposalId).FirstOrDefault();
    }

    public long CountByUserId(int userId)
    {
        return _exchanges.CountDocuments(e => e.ProponentID == userId || e.ProposedID == userId);
    }
}
