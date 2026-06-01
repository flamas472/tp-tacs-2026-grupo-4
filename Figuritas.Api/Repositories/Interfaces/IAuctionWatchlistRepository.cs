using Figuritas.Shared.Model.Subastas;

namespace Figuritas.Api.Repositories;

public interface IAuctionWatchlistRepository
{
    Task<AuctionWatchlist> AddAsync(AuctionWatchlist entry);
    Task<bool> RemoveAsync(int userId, int auctionId);
    Task<bool> ExistsAsync(int userId, int auctionId);
    Task<List<int>> GetWatcherUserIdsAsync(int auctionId);
    Task<List<AuctionWatchlist>> GetByUserIdAsync(int userId);
}
