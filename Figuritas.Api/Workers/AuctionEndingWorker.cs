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
                var auctionService = scope.ServiceProvider.GetRequiredService<AuctionService>();

                await ProcessEndingAuctionsAsync(auctionRepo, watchlistRepo, notificationService);
                await ProcessExpiredAuctionsAsync(auctionRepo, auctionService);
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
                    "Subasta por finalizar",
                    $"La subasta #{auction.Id} finaliza el {auction.EndsAt:dd/MM/yyyy HH:mm}. ¡No te la pierdas!",
                    expiresAt: auction.EndsAt);
            }

            _logger.LogInformation(
                "Sent AuctionEnding notifications for auction {AuctionId} to {Count} watchers.",
                auction.Id, watcherIds.Count);
        }
    }

    /// <summary>
    /// Finds active auctions whose expiry date has already passed and closes them automatically.
    /// Delegates all business logic (winner selection, stock transfers, missing-sticker cleanup)
    /// to <see cref="AuctionService.CloseAuctionAutomatically"/>.
    ///
    /// Uses an atomic claim flag (<see cref="IAuctionRepository.TryClaimAutomaticClosureAsync"/>)
    /// to guarantee idempotency across multiple worker instances: only the instance that wins
    /// the atomic update will proceed to close the auction.
    ///
    /// Extracted as an internal method to allow direct unit testing.
    /// </summary>
    internal async Task ProcessExpiredAuctionsAsync(
        IAuctionRepository auctionRepo,
        AuctionService auctionService)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiredAuctions = await auctionRepo.GetExpiredActiveAuctionsAsync(now);

        foreach (var auction in expiredAuctions)
        {
            // Atomically claim the closure right. Only one worker instance will succeed.
            var claimed = await auctionRepo.TryClaimAutomaticClosureAsync(auction.Id);
            if (!claimed)
            {
                _logger.LogDebug(
                    "Automatic closure for auction {AuctionId} already claimed by another process. Skipping.",
                    auction.Id);
                continue;
            }

            try
            {
                await auctionService.CloseAuctionAutomatically(auction.Id);

                _logger.LogInformation(
                    "Auction {AuctionId} automatically closed. Winner offer: {WinnerId}.",
                    auction.Id, auction.BestCurrentOfferId?.ToString() ?? "none");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to automatically close auction {AuctionId}.", auction.Id);
            }
        }
    }
}
