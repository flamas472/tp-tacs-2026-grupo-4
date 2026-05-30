using Figuritas.Shared.Model.Intercambios;
using MongoDB.Driver;

public class ExchangeRepository
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

    public Exchange? GetById(int exchangeId)
    {
        return _exchanges.Find(e => e.Id == exchangeId).FirstOrDefault();
    }
}
