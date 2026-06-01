using Figuritas.Shared.Model;
using Figuritas.Shared.Model.Intercambios;
using Figuritas.Shared.Model.Notificaciones;
using Figuritas.Shared.Model.Subastas;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IConfiguration configuration)
    {
        var settings = configuration.GetSection("Mongo").Get<MongoDbSettings>()
            ?? throw new InvalidOperationException("Missing 'Mongo' configuration in appsettings.json");

        var client = new MongoClient(settings.ConnectionString);
        _database = client.GetDatabase(settings.DatabaseName);
        EnsureClassMapsRegistered();
    }

    public IMongoCollection<T> Collection<T>(string collectionName) => _database.GetCollection<T>(collectionName);

    /// <summary>
    /// Exposes the underlying IMongoDatabase so that test infrastructure can perform
    /// operations such as dropping collections for isolation between test runs.
    /// </summary>
    public IMongoDatabase GetDatabase() => _database;

    /// <summary>
    /// Exposes the underlying IMongoClient so that services can start multi-document sessions
    /// for ACID transactions.
    /// </summary>
    public IMongoClient GetClient() => _database.Client;

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
        RegisterClassMap<Auction>();
        RegisterClassMap<AuctionOffer>();
        RegisterClassMap<MissingSticker>();
        RegisterClassMap<Notification>();
        RegisterClassMap<AuctionWatchlist>();
    }

    private static readonly object _classMapLock = new();

    private static void RegisterClassMap<T>() where T : class
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(T)))
            return;

        lock (_classMapLock)
        {
            if (BsonClassMap.IsClassMapRegistered(typeof(T)))
                return;

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
}
