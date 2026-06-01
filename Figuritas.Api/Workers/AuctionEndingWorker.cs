using Figuritas.Api.Repositories;
using Figuritas.Api.Services;
using Figuritas.Shared.Model.Notificaciones;
using Microsoft.Extensions.DependencyInjection;

namespace Figuritas.Api.Workers;

public class AuctionEndingWorker : BackgroundService
{
    internal static readonly TimeSpan AlertWindow = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuctionEndingWorker> _logger;
    private readonly TimeProvider _timeProvider;

    public AuctionEndingWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<AuctionEndingWorker> logger,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var auctionRepo = scope.ServiceProvider.GetRequiredService<IAuctionRepository>();
                var watchlistRepo = scope.ServiceProvider.GetRequiredService<IAuctionWatchlistRepository>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                await ProcessEndingAuctionsAsync(auctionRepo, watchlistRepo, notificationService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in AuctionEndingWorker.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    /// <summary>
    /// Core business logic: finds active auctions ending within the alert window and sends
    /// AuctionEnding notifications to all watchers. Extracted as an internal method to allow
    /// direct unit testing without depending on the background scheduler or real timers.
    /// </summary>
    internal async Task ProcessEndingAuctionsAsync(
        IAuctionRepository auctionRepo,
        IAuctionWatchlistRepository watchlistRepo,
        INotificationService notificationService)
    {
        var threshold = _timeProvider.GetUtcNow().UtcDateTime.Add(AlertWindow);
        var endingSoonAuctions = await auctionRepo.GetActiveAuctionsEndingBeforeAsync(threshold);

        foreach (var auction in endingSoonAuctions)
        {
            // Atomically claim the notification flag before sending.
            // If another worker instance already claimed it, skip this auction to avoid duplicates.
            var claimed = await auctionRepo.TryClaimEndingNotificationAsync(auction.Id);
            if (!claimed)
            {
                _logger.LogDebug(
                    "AuctionEnding notification for auction {AuctionId} already claimed by another process. Skipping.",
                    auction.Id);
                continue;
            }

            var watcherIds = await watchlistRepo.GetWatcherUserIdsAsync(auction.Id);

            foreach (var watcherId in watcherIds)
            {
                await notificationService.SendNotificationAsync(
                    watcherId,
                    NotificationType.AuctionEnding,
                    "Auction Ending Soon",
                    $"Auction #{auction.Id} is ending at {auction.EndsAt:u}. Don't miss out!",
                    expiresAt: auction.EndsAt);
            }

            _logger.LogInformation(
                "Sent AuctionEnding notifications for auction {AuctionId} to {Count} watchers.",
                auction.Id, watcherIds.Count);
        }
    }
}
