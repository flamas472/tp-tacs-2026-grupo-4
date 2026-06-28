using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Figuritas.Api.Tests;

/// <summary>
/// Custom WebApplicationFactory that overrides the MongoDB database name so
/// integration tests run against an isolated database instead of production/dev data.
/// The database name is stable across a single test process run and differs from the
/// production name, so re-runs always start from a known state after each cleanup.
/// </summary>
public sealed class IntegrationTestFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Fixed name for the test-exclusive MongoDB database.
    /// Kept stable (not randomised) so that the same database is targeted on every run,
    /// making pre-run cleanup predictable.
    /// </summary>
    public const string TestDatabaseName = "FiguriTACS_TestDB";

    /// <summary>
    /// Ensures that at most one cleanup operation runs at a time.
    /// This prevents race conditions between the IAsyncLifetime.InitializeAsync calls
    /// of consecutive test classes sharing the same collection fixture.
    /// </summary>
    private readonly SemaphoreSlim _cleanupLock = new(1, 1);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Override the database name, disable the exchange proposal rate-limit guard so
            // integration tests can create back-to-back proposals without triggering the
            // chronological anti-spam check that is only meaningful in production traffic,
            // and disable the login rate limiter so tests can register/login freely.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:DatabaseName"] = TestDatabaseName,
                ["RateLimit:ExchangeProposalWindowSeconds"] = "0",
                ["RateLimit:LoginEnabled"] = "false",
                ["RateLimit:RegisterEnabled"] = "false",
                ["RateLimit:RefreshEnabled"] = "false"
            });
        });

        builder.UseEnvironment("Development");
    }

    /// <summary>
    /// Removes all documents from every mutable collection so every test starts
    /// from a clean, predictable state.
    ///
    /// Uses DeleteMany rather than DropCollection to avoid recreation delays and
    /// index loss.  The semaphore guarantees that concurrent InitializeAsync calls
    /// from different test-class instances do not interleave.
    ///
    /// Catalogue collections (Stickers, NationalTeams, Teams, Categories) are
    /// intentionally excluded because they are seeded once at host start-up and are
    /// read-only from the tests' point of view.
    /// </summary>
    public async Task CleanMutableCollectionsAsync()
    {
        await _cleanupLock.WaitAsync();
        try
        {
            using var scope = Services.CreateScope();
            var mongoContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
            var database = mongoContext.GetDatabase();

            // These names must match the collection names used in the respective repositories.
            var mutableCollections = new[]
            {
                "Users",
                "UserStickers",
                "MissingStickers",
                "ExchangeProposals",
                "Exchanges",
                "Auctions",
                "AuctionOffers",
                "AuctionWatchlists",
                "Notifications",
                "Rates",
                "refresh_tokens"
            };

            foreach (var name in mutableCollections)
            {
                var collection = database.GetCollection<MongoDB.Bson.BsonDocument>(name);
                await collection.DeleteManyAsync(FilterDefinition<MongoDB.Bson.BsonDocument>.Empty);
            }
        }
        finally
        {
            _cleanupLock.Release();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cleanupLock.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// xUnit collection fixture that creates a single <see cref="IntegrationTestFactory"/>
/// for the entire test collection and exposes the clean-up helper so individual test
/// classes can call it inside <c>InitializeAsync</c>.
///
/// All integration test classes must carry:
///   [Collection(nameof(IntegrationTestCollection))]
/// and receive <see cref="IntegrationTestFactory"/> via constructor injection.
/// </summary>
[CollectionDefinition(nameof(IntegrationTestCollection))]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFactory>
{
}
