using Figuritas.Shared.Model;
using Figuritas.Shared.Model.Subastas;
using MongoDB.Driver;

namespace Figuritas.Api.Repositories;

public class AuctionRepository : IAuctionRepository
{
    private readonly IMongoCollection<Auction> _auctions;
    private readonly IIdGenerator _idGenerator;

    public AuctionRepository(MongoDbContext context, IIdGenerator idGenerator)
    {
        _auctions = context.Collection<Auction>("Auctions");
        _idGenerator = idGenerator;
    }

    public List<Auction> GetAll() => _auctions.Find(_ => true).ToList();

    public void Add(Auction auction)
    {
        auction.Id = _idGenerator.GetNextId<Auction>();
        _auctions.InsertOne(auction);
    }

    public Auction? GetById(int id) => _auctions.Find(a => a.Id == id).FirstOrDefault();

    public void Update(Auction auction)
    {
        var result = _auctions.ReplaceOne(a => a.Id == auction.Id, auction);
        if (!result.IsAcknowledged || result.MatchedCount == 0)
            throw new ArgumentException("Auction not found");
    }

    public List<Auction> GetByAuctioneerId(int auctioneerId, string? status, int page, int pageSize)
    {
        var filter = Builders<Auction>.Filter.Eq(a => a.AuctioneerId, auctioneerId);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<AuctionStatus>(status, ignoreCase: true, out var parsedStatus))
            filter = Builders<Auction>.Filter.And(filter, Builders<Auction>.Filter.Eq(a => a.Status, parsedStatus));

        return _auctions.Find(filter)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToList();
    }
}