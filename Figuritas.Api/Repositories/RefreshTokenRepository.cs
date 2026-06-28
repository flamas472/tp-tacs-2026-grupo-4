using Figuritas.Shared.Model;
using MongoDB.Driver;

namespace Figuritas.Api.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IMongoCollection<RefreshToken> _tokens;

    public RefreshTokenRepository(MongoDbContext context)
    {
        _tokens = context.Collection<RefreshToken>("refresh_tokens");
        EnsureIndexes();
    }

    private void EnsureIndexes()
    {
        // Fast lookup by token string
        _tokens.Indexes.CreateOne(new CreateIndexModel<RefreshToken>(
            Builders<RefreshToken>.IndexKeys.Ascending(t => t.Token),
            new CreateIndexOptions { Unique = true }));

        // Fast revocation of all tokens for a user
        _tokens.Indexes.CreateOne(new CreateIndexModel<RefreshToken>(
            Builders<RefreshToken>.IndexKeys.Ascending(t => t.UserId)));

        // TTL index: automatically delete documents after ExpiresAt
        _tokens.Indexes.CreateOne(new CreateIndexModel<RefreshToken>(
            Builders<RefreshToken>.IndexKeys.Ascending(t => t.ExpiresAt),
            new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }));
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token)
    {
        return await _tokens.Find(t => t.Token == token).FirstOrDefaultAsync();
    }

    public async Task CreateAsync(RefreshToken refreshToken)
    {
        await _tokens.InsertOneAsync(refreshToken);
    }

    public async Task RevokeAsync(string token)
    {
        await _tokens.UpdateOneAsync(
            t => t.Token == token,
            Builders<RefreshToken>.Update.Set(t => t.IsRevoked, true));
    }

    public async Task RevokeAllForUserAsync(string userId)
    {
        await _tokens.UpdateManyAsync(
            t => t.UserId == userId && !t.IsRevoked,
            Builders<RefreshToken>.Update.Set(t => t.IsRevoked, true));
    }
}
