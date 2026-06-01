using Figuritas.Api.Repositories;
using Figuritas.Api.Services;
using Figuritas.Api.Workers;
using Figuritas.Shared.Model;
using Figuritas.Shared.Model.Notificaciones;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Figuritas.Api.Tests;

/// <summary>
/// Unit tests for AuctionEndingWorker.ProcessEndingAuctionsAsync.
/// These tests exercise the core business logic directly without:
/// - real timers or Task.Delay
/// - background scheduling (BackgroundService loop)
/// - SignalR
/// - MongoDB (all repositories are mocked)
/// </summary>
public class AuctionEndingWorkerTests
{
    // ─── Helpers ────────────────────────────────────────────────────────────

    private static AuctionEndingWorker CreateWorker(
        Mock<IAuctionRepository> auctionRepo,
        Mock<IAuctionWatchlistRepository> watchlistRepo,
        Mock<INotificationService> notificationService,
        TimeProvider? timeProvider = null)
    {
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var logger = new Mock<ILogger<AuctionEndingWorker>>();
        return new AuctionEndingWorker(scopeFactory.Object, logger.Object, timeProvider ?? TimeProvider.System);
    }

    private static Auction BuildActiveAuction(int id, DateTime endsAt, bool notificationSent = false) =>
        new()
        {
            Id = id,
            AuctioneerId = 1,
            UserStickerId = 10,
            Status = Figuritas.Shared.Model.Subastas.AuctionStatus.Active,
            EndsAt = endsAt,
            AuctionEndingNotificationSent = notificationSent
        };

    // ─── Escenario 1 — Subasta activa próxima a finalizar ────────────────────

    /// <summary>
    /// Scenario 1: Active auction ending in 15 minutes, not yet flagged, with one watcher.
    /// TryClaimEndingNotificationAsync returns true → SendNotificationAsync called once for that watcher.
    /// </summary>
    [Fact]
    public async Task ProcessEndingAuctions_ActiveAuctionEndingIn15Min_SendsOneNotification()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(now);

        var auction = BuildActiveAuction(id: 42, endsAt: now.UtcDateTime.AddMinutes(15));
        var auctionRepo = new Mock<IAuctionRepository>();
        var watchlistRepo = new Mock<IAuctionWatchlistRepository>();
        var notificationService = new Mock<INotificationService>();

        auctionRepo
            .Setup(r => r.GetActiveAuctionsEndingBeforeAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Auction> { auction });

        auctionRepo
            .Setup(r => r.TryClaimEndingNotificationAsync(42))
            .ReturnsAsync(true);

        watchlistRepo
            .Setup(r => r.GetWatcherUserIdsAsync(42))
            .ReturnsAsync(new List<int> { 7 });

        notificationService
            .Setup(s => s.SendNotificationAsync(
                It.IsAny<int>(),
                It.IsAny<NotificationType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>()))
            .ReturnsAsync((Notification?)null);

        var worker = CreateWorker(auctionRepo, watchlistRepo, notificationService, fakeTime);

        // Act
        await worker.ProcessEndingAuctionsAsync(auctionRepo.Object, watchlistRepo.Object, notificationService.Object);

        // Assert
        notificationService.Verify(
            s => s.SendNotificationAsync(
                7,
                NotificationType.AuctionEnding,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>()),
            Times.Once,
            "Expected exactly one notification to watcher userId=7.");
    }

    // ─── Escenario 2 — Subasta fuera de la ventana ───────────────────────────

    /// <summary>
    /// Scenario 2: Auction ending in 2 hours — outside the 30-minute alert window.
    /// GetActiveAuctionsEndingBeforeAsync returns an empty list → no notifications sent.
    /// </summary>
    [Fact]
    public async Task ProcessEndingAuctions_AuctionFarFromEnding_SendsNoNotification()
    {
        // Arrange
        var auctionRepo = new Mock<IAuctionRepository>();
        var watchlistRepo = new Mock<IAuctionWatchlistRepository>();
        var notificationService = new Mock<INotificationService>();

        // The repository already filters by the threshold; returning an empty list simulates
        // a scenario where no auctions fall within the alert window.
        auctionRepo
            .Setup(r => r.GetActiveAuctionsEndingBeforeAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Auction>());

        var worker = CreateWorker(auctionRepo, watchlistRepo, notificationService);

        // Act
        await worker.ProcessEndingAuctionsAsync(auctionRepo.Object, watchlistRepo.Object, notificationService.Object);

        // Assert
        notificationService.Verify(
            s => s.SendNotificationAsync(
                It.IsAny<int>(),
                It.IsAny<NotificationType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>()),
            Times.Never,
            "No notification should be sent when no auctions are ending soon.");
    }

    // ─── Escenario 3 — Subasta ya marcada como procesada ────────────────────

    /// <summary>
    /// Scenario 3: TryClaimEndingNotificationAsync returns false (flag already set by another process).
    /// → SendNotificationAsync is never invoked.
    /// </summary>
    [Fact]
    public async Task ProcessEndingAuctions_ClaimReturnsFalse_SendsNoNotification()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(now);

        var auction = BuildActiveAuction(id: 55, endsAt: now.UtcDateTime.AddMinutes(10), notificationSent: true);
        var auctionRepo = new Mock<IAuctionRepository>();
        var watchlistRepo = new Mock<IAuctionWatchlistRepository>();
        var notificationService = new Mock<INotificationService>();

        auctionRepo
            .Setup(r => r.GetActiveAuctionsEndingBeforeAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Auction> { auction });

        // Claim fails: another process (or a prior run) already set the flag.
        auctionRepo
            .Setup(r => r.TryClaimEndingNotificationAsync(55))
            .ReturnsAsync(false);

        var worker = CreateWorker(auctionRepo, watchlistRepo, notificationService, fakeTime);

        // Act
        await worker.ProcessEndingAuctionsAsync(auctionRepo.Object, watchlistRepo.Object, notificationService.Object);

        // Assert
        notificationService.Verify(
            s => s.SendNotificationAsync(
                It.IsAny<int>(),
                It.IsAny<NotificationType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>()),
            Times.Never,
            "Notification must not be sent when the claim flag could not be acquired.");
    }

    // ─── Escenario 4 — Múltiples watchers ───────────────────────────────────

    /// <summary>
    /// Scenario 4: Auction ending soon with 3 distinct watchers.
    /// TryClaimEndingNotificationAsync returns true → SendNotificationAsync called exactly 3 times.
    /// </summary>
    [Fact]
    public async Task ProcessEndingAuctions_MultipleWatchers_SendsOneNotificationPerWatcher()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(now);

        var auction = BuildActiveAuction(id: 99, endsAt: now.UtcDateTime.AddMinutes(20));
        var watcherIds = new List<int> { 10, 20, 30 };

        var auctionRepo = new Mock<IAuctionRepository>();
        var watchlistRepo = new Mock<IAuctionWatchlistRepository>();
        var notificationService = new Mock<INotificationService>();

        auctionRepo
            .Setup(r => r.GetActiveAuctionsEndingBeforeAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Auction> { auction });

        auctionRepo
            .Setup(r => r.TryClaimEndingNotificationAsync(99))
            .ReturnsAsync(true);

        watchlistRepo
            .Setup(r => r.GetWatcherUserIdsAsync(99))
            .ReturnsAsync(watcherIds);

        notificationService
            .Setup(s => s.SendNotificationAsync(
                It.IsAny<int>(),
                It.IsAny<NotificationType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>()))
            .ReturnsAsync((Notification?)null);

        var worker = CreateWorker(auctionRepo, watchlistRepo, notificationService, fakeTime);

        // Act
        await worker.ProcessEndingAuctionsAsync(auctionRepo.Object, watchlistRepo.Object, notificationService.Object);

        // Assert
        notificationService.Verify(
            s => s.SendNotificationAsync(
                It.IsAny<int>(),
                NotificationType.AuctionEnding,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>()),
            Times.Exactly(3),
            "Expected exactly 3 notifications, one per watcher.");

        // Verify each specific watcher received the notification
        foreach (var watcherId in watcherIds)
        {
            notificationService.Verify(
                s => s.SendNotificationAsync(
                    watcherId,
                    NotificationType.AuctionEnding,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<int?>()),
                Times.Once,
                $"Watcher userId={watcherId} must receive exactly one notification.");
        }
    }

    // ─── Escenario 5 — Idempotencia: múltiples ejecuciones ──────────────────

    /// <summary>
    /// Scenario 5: Two consecutive executions for the same auction.
    /// First run: TryClaimEndingNotificationAsync returns true → notification sent.
    /// Second run: TryClaimEndingNotificationAsync returns false (flag already set) → no notification.
    /// Total: SendNotificationAsync called exactly once.
    /// </summary>
    [Fact]
    public async Task ProcessEndingAuctions_RunTwice_SendsNotificationOnlyOnFirstRun()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(now);

        var auction = BuildActiveAuction(id: 77, endsAt: now.UtcDateTime.AddMinutes(25));
        var auctionRepo = new Mock<IAuctionRepository>();
        var watchlistRepo = new Mock<IAuctionWatchlistRepository>();
        var notificationService = new Mock<INotificationService>();

        auctionRepo
            .Setup(r => r.GetActiveAuctionsEndingBeforeAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Auction> { auction });

        // First call succeeds, second fails (simulates the atomic MongoDB flag flip)
        var claimCallCount = 0;
        auctionRepo
            .Setup(r => r.TryClaimEndingNotificationAsync(77))
            .ReturnsAsync(() => ++claimCallCount == 1);

        watchlistRepo
            .Setup(r => r.GetWatcherUserIdsAsync(77))
            .ReturnsAsync(new List<int> { 5 });

        notificationService
            .Setup(s => s.SendNotificationAsync(
                It.IsAny<int>(),
                It.IsAny<NotificationType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>()))
            .ReturnsAsync((Notification?)null);

        var worker = CreateWorker(auctionRepo, watchlistRepo, notificationService, fakeTime);

        // Act — first execution
        await worker.ProcessEndingAuctionsAsync(auctionRepo.Object, watchlistRepo.Object, notificationService.Object);

        // Act — second execution (claim will return false this time)
        await worker.ProcessEndingAuctionsAsync(auctionRepo.Object, watchlistRepo.Object, notificationService.Object);

        // Assert: notification sent exactly once despite two executions
        notificationService.Verify(
            s => s.SendNotificationAsync(
                It.IsAny<int>(),
                NotificationType.AuctionEnding,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>()),
            Times.Once,
            "Notification must be sent exactly once even when the worker runs twice for the same auction.");
    }

    // ─── Escenario 6 — Threshold calculado con TimeProvider ─────────────────

    /// <summary>
    /// Scenario 6: Verifies that the threshold passed to GetActiveAuctionsEndingBeforeAsync
    /// equals UtcNow + AlertWindow, using a controlled FakeTimeProvider.
    /// This ensures the worker uses the injected time abstraction instead of DateTime.UtcNow directly.
    /// </summary>
    [Fact]
    public async Task ProcessEndingAuctions_UsesTimeProvider_ThresholdIsNowPlusAlertWindow()
    {
        // Arrange
        var fixedNow = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var expectedThreshold = fixedNow.UtcDateTime.Add(AuctionEndingWorker.AlertWindow);
        var fakeTime = new FakeTimeProvider(fixedNow);

        var auctionRepo = new Mock<IAuctionRepository>();
        var watchlistRepo = new Mock<IAuctionWatchlistRepository>();
        var notificationService = new Mock<INotificationService>();

        DateTime? capturedThreshold = null;
        auctionRepo
            .Setup(r => r.GetActiveAuctionsEndingBeforeAsync(It.IsAny<DateTime>()))
            .Callback<DateTime>(t => capturedThreshold = t)
            .ReturnsAsync(new List<Auction>());

        var worker = CreateWorker(auctionRepo, watchlistRepo, notificationService, fakeTime);

        // Act
        await worker.ProcessEndingAuctionsAsync(auctionRepo.Object, watchlistRepo.Object, notificationService.Object);

        // Assert
        Assert.NotNull(capturedThreshold);
        Assert.Equal(expectedThreshold, capturedThreshold!.Value);
    }

    // ─── Escenario 7 — Sin watchers ─────────────────────────────────────────

    /// <summary>
    /// Scenario 7: Auction is ending soon, claim succeeds, but the watchlist has no watchers.
    /// → SendNotificationAsync is never called.
    /// </summary>
    [Fact]
    public async Task ProcessEndingAuctions_NoWatchers_SendsNoNotification()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(now);

        var auction = BuildActiveAuction(id: 11, endsAt: now.UtcDateTime.AddMinutes(5));
        var auctionRepo = new Mock<IAuctionRepository>();
        var watchlistRepo = new Mock<IAuctionWatchlistRepository>();
        var notificationService = new Mock<INotificationService>();

        auctionRepo
            .Setup(r => r.GetActiveAuctionsEndingBeforeAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Auction> { auction });

        auctionRepo
            .Setup(r => r.TryClaimEndingNotificationAsync(11))
            .ReturnsAsync(true);

        watchlistRepo
            .Setup(r => r.GetWatcherUserIdsAsync(11))
            .ReturnsAsync(new List<int>());

        var worker = CreateWorker(auctionRepo, watchlistRepo, notificationService, fakeTime);

        // Act
        await worker.ProcessEndingAuctionsAsync(auctionRepo.Object, watchlistRepo.Object, notificationService.Object);

        // Assert
        notificationService.Verify(
            s => s.SendNotificationAsync(
                It.IsAny<int>(),
                It.IsAny<NotificationType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>()),
            Times.Never,
            "No notification should be sent when the auction has no watchers.");
    }

    // ─── Escenario 8 — Múltiples subastas, procesamiento independiente ───────

    /// <summary>
    /// Scenario 8: Two auctions ending soon. Claim succeeds for both.
    /// Each has one distinct watcher → two separate notifications sent.
    /// </summary>
    [Fact]
    public async Task ProcessEndingAuctions_TwoAuctionsWithOneWatcherEach_SendsTwoNotifications()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(now);

        var auction1 = BuildActiveAuction(id: 100, endsAt: now.UtcDateTime.AddMinutes(10));
        var auction2 = BuildActiveAuction(id: 200, endsAt: now.UtcDateTime.AddMinutes(20));

        var auctionRepo = new Mock<IAuctionRepository>();
        var watchlistRepo = new Mock<IAuctionWatchlistRepository>();
        var notificationService = new Mock<INotificationService>();

        auctionRepo
            .Setup(r => r.GetActiveAuctionsEndingBeforeAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Auction> { auction1, auction2 });

        auctionRepo.Setup(r => r.TryClaimEndingNotificationAsync(100)).ReturnsAsync(true);
        auctionRepo.Setup(r => r.TryClaimEndingNotificationAsync(200)).ReturnsAsync(true);

        watchlistRepo.Setup(r => r.GetWatcherUserIdsAsync(100)).ReturnsAsync(new List<int> { 101 });
        watchlistRepo.Setup(r => r.GetWatcherUserIdsAsync(200)).ReturnsAsync(new List<int> { 201 });

        notificationService
            .Setup(s => s.SendNotificationAsync(
                It.IsAny<int>(),
                It.IsAny<NotificationType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>()))
            .ReturnsAsync((Notification?)null);

        var worker = CreateWorker(auctionRepo, watchlistRepo, notificationService, fakeTime);

        // Act
        await worker.ProcessEndingAuctionsAsync(auctionRepo.Object, watchlistRepo.Object, notificationService.Object);

        // Assert
        notificationService.Verify(
            s => s.SendNotificationAsync(
                It.IsAny<int>(),
                NotificationType.AuctionEnding,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int?>()),
            Times.Exactly(2),
            "Expected exactly 2 notifications — one per auction/watcher pair.");

        notificationService.Verify(
            s => s.SendNotificationAsync(101, NotificationType.AuctionEnding,
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<int?>()),
            Times.Once);

        notificationService.Verify(
            s => s.SendNotificationAsync(201, NotificationType.AuctionEnding,
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<int?>()),
            Times.Once);
    }
}

/// <summary>
/// Minimal TimeProvider implementation for unit tests that returns a fixed point in time.
/// Avoids the dependency on Microsoft.Extensions.TimeProvider.Testing, which requires an
/// additional NuGet package not already in the project.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _fixedUtcNow;

    public FakeTimeProvider(DateTimeOffset fixedUtcNow)
    {
        _fixedUtcNow = fixedUtcNow;
    }

    public override DateTimeOffset GetUtcNow() => _fixedUtcNow;
}
