using Figuritas.Shared.Model;
using Figuritas.Shared.Model.Intercambios;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IConfiguration configuration)
    {
        var settings = configuration.GetSection("Mongo").Get<MongoDbSettings>()
            ?? throw new InvalidOperationException("Falta la configuración 'Mongo' en appsettings.json");

        var client = new MongoClient(settings.ConnectionString);
        _database = client.GetDatabase(settings.DatabaseName);
        EnsureClassMapsRegistered();
    }

    public IMongoCollection<T> Collection<T>(string collectionName) => _database.GetCollection<T>(collectionName);

    private static void EnsureClassMapsRegistered()
    {
        RegisterClassMap<Category>();
        RegisterClassMap<NationalTeam>();
        RegisterClassMap<Team>();
        RegisterClassMap<Sticker>();
        RegisterClassMap<User>();
        RegisterClassMap<UserSticker>();
        RegisterClassMap<Rate>();
        RegisterClassMap<Exchange>();
        RegisterClassMap<ExchangeProposal>();
        RegisterClassMap<ExchangeSuggestion>();
        RegisterClassMap<Auction>();
        RegisterClassMap<AuctionOffer>();
    }

    private static void RegisterClassMap<T>() where T : class
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(T)))
        {
            return;
        }

        BsonClassMap.RegisterClassMap<T>(cm =>
        {
            cm.AutoMap();
            var idProperty = typeof(T).GetProperty("Id");
            if (idProperty != null)
            {
                cm.MapIdMember(idProperty);
            }
            cm.SetIgnoreExtraElements(true);
        });
    }
}
