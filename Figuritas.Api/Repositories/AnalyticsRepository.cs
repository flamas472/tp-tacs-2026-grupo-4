using Figuritas.Shared.Model;
using Figuritas.Shared.Model.Intercambios;
using Figuritas.Shared.Model.Notificaciones;
using Figuritas.Shared.Model.Subastas;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Figuritas.Api.Repositories;

/// <summary>
/// Aggregation-only repository for platform analytics.
/// Uses MongoDB aggregation pipelines for efficiency.
/// No CRUD operations — responsibility boundary is read-only analytics.
/// </summary>
public class AnalyticsRepository : IAnalyticsRepository
{
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<UserSticker> _userStickers;
    private readonly IMongoCollection<ExchangeProposal> _proposals;
    private readonly IMongoCollection<Auction> _auctions;
    private readonly IMongoCollection<AuctionOffer> _auctionOffers;
    private readonly IMongoCollection<Notification> _notifications;

    public AnalyticsRepository(MongoDbContext context)
    {
        _users = context.Collection<User>("Users");
        _userStickers = context.Collection<UserSticker>("UserStickers");
        _proposals = context.Collection<ExchangeProposal>("ExchangeProposals");
        _auctions = context.Collection<Auction>("Auctions");
        _auctionOffers = context.Collection<AuctionOffer>("AuctionOffers");
        _notifications = context.Collection<Notification>("Notifications");
        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        // Index: Users.Role — eliminates COLLSCAN when filtering admins/superadmins
        var userRoleIndex = new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(u => u.Role));
        _users.Indexes.CreateOne(userRoleIndex);

        // Index: Notifications.Type — improves type-based analytics queries
        var notifTypeIndex = new CreateIndexModel<Notification>(
            Builders<Notification>.IndexKeys.Ascending(n => n.Type));
        _notifications.Indexes.CreateOne(notifTypeIndex);

        // Index: ExchangeProposals.State — improves state-based analytics queries
        var proposalStateIndex = new CreateIndexModel<ExchangeProposal>(
            Builders<ExchangeProposal>.IndexKeys.Ascending(p => p.State));
        _proposals.Indexes.CreateOne(proposalStateIndex);

        // Index: Auctions.Status — improves status-based analytics queries
        var auctionStatusIndex = new CreateIndexModel<Auction>(
            Builders<Auction>.IndexKeys.Ascending(a => a.Status));
        _auctions.Indexes.CreateOne(auctionStatusIndex);
    }

    public async Task<int> CountTotalUsersAsync()
    {
        return (int)await _users.CountDocumentsAsync(FilterDefinition<User>.Empty);
    }

    /// <summary>
    /// Active user = user who owns at least one active UserSticker with Quantity > 0.
    /// This definition is used because the system lacks a LastActiveAt timestamp.
    /// </summary>
    public async Task<int> CountActiveUsersWithPublishedStickersAsync()
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                { "Active", true },
                { "Quantity", new BsonDocument("$gt", 0) }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$UserId" }
            }),
            new BsonDocument("$count", "total")
        };

        var result = await _userStickers
            .Aggregate<BsonDocument>(pipeline)
            .FirstOrDefaultAsync();

        return result?["total"].AsInt32 ?? 0;
    }

    public async Task<double> GetAverageCommunityReputationAsync()
    {
        var pipeline = new[]
        {
            new BsonDocument("$unwind", "$Ratings"),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", BsonNull.Value },
                { "avgRep", new BsonDocument("$avg", "$Ratings.Stars") }
            })
        };

        var result = await _users
            .Aggregate<BsonDocument>(pipeline)
            .FirstOrDefaultAsync();

        return result != null ? Math.Round(result["avgRep"].ToDouble(), 2) : 0.0;
    }

    public async Task<int> CountTotalProposalsAsync()
    {
        return (int)await _proposals.CountDocumentsAsync(FilterDefinition<ExchangeProposal>.Empty);
    }

    public async Task<int> CountProposalsByStateAsync(string state)
    {
        if (!Enum.TryParse<ExchangeProposalState>(state, ignoreCase: true, out var parsedState))
            return 0;
        return (int)await _proposals.CountDocumentsAsync(p => p.State == parsedState);
    }

    public async Task<int> CountTotalAuctionsAsync()
    {
        return (int)await _auctions.CountDocumentsAsync(FilterDefinition<Auction>.Empty);
    }

    public async Task<int> CountAuctionsByStatusAsync(string status)
    {
        if (!Enum.TryParse<AuctionStatus>(status, ignoreCase: true, out var parsedStatus))
            return 0;
        return (int)await _auctions.CountDocumentsAsync(a => a.Status == parsedStatus);
    }

    /// <summary>
    /// Closed auctions with winner = Closed status AND BestCurrentOfferId is not null.
    /// </summary>
    public async Task<int> CountClosedAuctionsWithWinnerAsync()
    {
        var filter = Builders<Auction>.Filter.And(
            Builders<Auction>.Filter.Eq(a => a.Status, AuctionStatus.Closed),
            Builders<Auction>.Filter.Ne(a => a.BestCurrentOfferId, (int?)null)
        );
        return (int)await _auctions.CountDocumentsAsync(filter);
    }

    /// <summary>
    /// Closed auctions without winner = Closed status AND BestCurrentOfferId is null.
    /// </summary>
    public async Task<int> CountClosedAuctionsWithoutWinnerAsync()
    {
        var filter = Builders<Auction>.Filter.And(
            Builders<Auction>.Filter.Eq(a => a.Status, AuctionStatus.Closed),
            Builders<Auction>.Filter.Eq(a => a.BestCurrentOfferId, (int?)null)
        );
        return (int)await _auctions.CountDocumentsAsync(filter);
    }

    public async Task<int> CountTotalNotificationsAsync()
    {
        return (int)await _notifications.CountDocumentsAsync(FilterDefinition<Notification>.Empty);
    }

    public async Task<int> CountNotificationsByTypeAsync(string type)
    {
        if (!Enum.TryParse<NotificationType>(type, ignoreCase: true, out var parsedType))
            return 0;
        return (int)await _notifications.CountDocumentsAsync(n => n.Type == parsedType);
    }
}
