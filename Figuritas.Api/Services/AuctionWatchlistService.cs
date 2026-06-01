using Figuritas.Api.Repositories;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model.Subastas;

namespace Figuritas.Api.Services;

public class AuctionWatchlistService
{
    private readonly IAuctionWatchlistRepository _watchlistRepo;
    private readonly IAuctionRepository _auctionRepo;

    public AuctionWatchlistService(
        IAuctionWatchlistRepository watchlistRepo,
        IAuctionRepository auctionRepo)
    {
        _watchlistRepo = watchlistRepo;
        _auctionRepo = auctionRepo;
    }

    public async Task<AuctionWatchlistResponseDTO> AddWatchAsync(int userId, int auctionId)
    {
        var auction = _auctionRepo.GetById(auctionId);
        if (auction == null)
            throw new KeyNotFoundException("Auction not found.");

        if (await _watchlistRepo.ExistsAsync(userId, auctionId))
            throw new InvalidOperationException("Already watching this auction.");

        var entry = new AuctionWatchlist
        {
            UserId = userId,
            AuctionId = auctionId
        };

        var saved = await _watchlistRepo.AddAsync(entry);
        return MapToDto(saved);
    }

    public async Task RemoveWatchAsync(int userId, int auctionId)
    {
        var removed = await _watchlistRepo.RemoveAsync(userId, auctionId);
        if (!removed)
            throw new KeyNotFoundException("Watchlist entry not found.");
    }

    public async Task<bool> IsWatchingAsync(int userId, int auctionId)
    {
        return await _watchlistRepo.ExistsAsync(userId, auctionId);
    }

    public async Task<List<int>> GetWatchersAsync(int auctionId)
    {
        return await _watchlistRepo.GetWatcherUserIdsAsync(auctionId);
    }

    public async Task<List<AuctionWatchlistResponseDTO>> GetMyWatchlistAsync(int userId)
    {
        var entries = await _watchlistRepo.GetByUserIdAsync(userId);
        return entries.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Adds a user to the watchlist silently (no exception if already watching).
    /// Used internally when a user places a bid.
    /// </summary>
    public async Task EnsureWatchingAsync(int userId, int auctionId)
    {
        if (!await _watchlistRepo.ExistsAsync(userId, auctionId))
        {
            var entry = new AuctionWatchlist
            {
                UserId = userId,
                AuctionId = auctionId
            };
            await _watchlistRepo.AddAsync(entry);
        }
    }

    private static AuctionWatchlistResponseDTO MapToDto(AuctionWatchlist entry) => new()
    {
        Id = entry.Id,
        UserId = entry.UserId,
        AuctionId = entry.AuctionId,
        CreatedAt = entry.CreatedAt
    };
}
