using Figuritas.Api.Repositories;
using Figuritas.Api.Services;
using Figuritas.Api.Workers;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model;
using Figuritas.Shared.Model.Subastas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Figuritas.Api.Tests;

/// <summary>
/// Unit tests for AuctionEndingWorker.ProcessExpiredAuctionsAsync.
/// These tests exercise automatic closure without real timers, databases, or SignalR.
///
/// AuctionService is replaced by a lightweight test-double (FakeAuctionService) to avoid
/// standing up a real MongoDB — consistent with how ProcessEndingAuctionsAsync tests mock
/// IAuctionRepository, IAuctionWatchlistRepository, and INotificationService directly.
/// </summary>
public class AuctionAutoCloseWorkerTests
{
    // ─── Test double ────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal subclass of AuctionService that overrides CloseAuctionAutomatically so
    /// tests can control its behavior without a real database or service dependencies.
    /// </summary>
    private sealed class FakeAuctionService : AuctionService
    {
        private readonly Func<int, Task<AuctionResponseDTO>> _closeFunc;

        public FakeAuctionService(Func<int, Task<AuctionResponseDTO>> closeFunc)
            : base(
                Mock.Of<IUserStickerRepository>(),
                Mock.Of<IAuctionRepository>(),
                Mock.Of<IAuctionOfferRepository>(),
                Mock.Of<IMissingStickerRepository>(),
                new AuctionWatchlistService(
                    Mock.Of<IAuctionWatchlistRepository>(),
                    Mock.Of<IAuctionRepository>()),
                Mock.Of<IUserRepository>())
        {
            _closeFunc = closeFunc;
        }

        public int CloseCalled { get; private set; }

        public override Task<AuctionResponseDTO> CloseAuctionAutomatically(int auctionId)
        {
            CloseCalled++;
            return _closeFunc(auctionId);
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static AuctionEndingWorker CreateWorker(TimeProvider? timeProvider = null)
    {
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var logger = new Mock<ILogger<AuctionEndingWorker>>();
        return new AuctionEndingWorker(scopeFactory.Object, logger.Object, timeProvider ?? TimeProvider.System);
    }

    private static Auction BuildExpiredActiveAuction(int id, int auctioneerId = 1, int? bestOfferId = null) =>
        new()
        {
            Id = id,
            AuctioneerId = auctioneerId,
            UserStickerId = 10,
            Status = AuctionStatus.Active,
            EndsAt = DateTime.UtcNow.AddMinutes(-10), // already expired
            BestCurrentOfferId = bestOfferId
        };

    // ─── Escenario 1 — Subasta vencida se cierra automáticamente ────────────

    /// <summary>
    /// Scenario 1: One active expired auction. Worker claims closure and delegates to
    /// AuctionService.CloseAuctionAutomatically.
    /// </summary>
    [Fact]
    public async Task ProcessExpiredAuctions_OneExpiredAuction_ClosesItAutomatically()
    {
        // Arrange
        var auction = BuildExpiredActiveAuction(id: 10, bestOfferId: 5);
        var auctionRepo = new Mock<IAuctionRepository>();

        auctionRepo
            .Setup(r => r.GetExpiredActiveAuctionsAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Auction> { auction });

        auctionRepo
            .Setup(r => r.TryClaimAutomaticClosureAsync(10))
            .ReturnsAsync(true);

        var fakeService = new FakeAuctionService(_ => Task.FromResult(new AuctionResponseDTO()));
        var worker = CreateWorker();

        // Act
        await worker.ProcessExpiredAuctionsAsync(auctionRepo.Object, fakeService);

        // Assert
        Assert.Equal(1, fakeService.CloseCalled);
    }

    // ─── Escenario 2 — Idempotencia: segunda ejecución no produce efectos ────

    /// <summary>
    /// Scenario 2: Worker runs twice for the same expired auction.
    /// First run: claim succeeds → closed.
    /// Second run: claim fails → no-op.
    /// CloseAuctionAutomatically called exactly once total.
    /// </summary>
    [Fact]
    public async Task ProcessExpiredAuctions_RunTwice_ClosesOnlyOnFirstRun()
    {
        // Arrange
        var auction = BuildExpiredActiveAuction(id: 20);
        var auctionRepo = new Mock<IAuctionRepository>();

        auctionRepo
            .Setup(r => r.GetExpiredActiveAuctionsAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Auction> { auction });

        var callCount = 0;
        auctionRepo
            .Setup(r => r.TryClaimAutomaticClosureAsync(20))
            .ReturnsAsync(() => ++callCount == 1); // first true, subsequent false

        var fakeService = new FakeAuctionService(_ => Task.FromResult(new AuctionResponseDTO()));
        var worker = CreateWorker();

        // Act
        await worker.ProcessExpiredAuctionsAsync(auctionRepo.Object, fakeService);
        await worker.ProcessExpiredAuctionsAsync(auctionRepo.Object, fakeService);

        // Assert: called once despite two runs
        Assert.Equal(1, fakeService.CloseCalled);
    }

    // ─── Escenario 3 — No hay subastas vencidas ──────────────────────────────

    /// <summary>
    /// Scenario 3: No expired auctions. CloseAuctionAutomatically never invoked.
    /// </summary>
    [Fact]
    public async Task ProcessExpiredAuctions_NoExpiredAuctions_DoesNothing()
    {
        // Arrange
        var auctionRepo = new Mock<IAuctionRepository>();

        auctionRepo
            .Setup(r => r.GetExpiredActiveAuctionsAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Auction>());

        var fakeService = new FakeAuctionService(_ => Task.FromResult(new AuctionResponseDTO()));
        var worker = CreateWorker();

        // Act
        await worker.ProcessExpiredAuctionsAsync(auctionRepo.Object, fakeService);

        // Assert
        Assert.Equal(0, fakeService.CloseCalled);
    }

    // ─── Escenario 4 — Multi-instancia: claim perdido ────────────────────────

    /// <summary>
    /// Scenario 4: Another worker instance already claimed the auction.
    /// TryClaimAutomaticClosureAsync returns false → CloseAuctionAutomatically not called.
    /// </summary>
    [Fact]
    public async Task ProcessExpiredAuctions_ClaimReturnsFalse_DoesNotClose()
    {
        // Arrange
        var auction = BuildExpiredActiveAuction(id: 30);
        var auctionRepo = new Mock<IAuctionRepository>();

        auctionRepo
            .Setup(r => r.GetExpiredActiveAuctionsAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Auction> { auction });

        auctionRepo
            .Setup(r => r.TryClaimAutomaticClosureAsync(30))
            .ReturnsAsync(false); // another instance already claimed it

        var fakeService = new FakeAuctionService(_ => Task.FromResult(new AuctionResponseDTO()));
        var worker = CreateWorker();

        // Act
        await worker.ProcessExpiredAuctionsAsync(auctionRepo.Object, fakeService);

        // Assert
        Assert.Equal(0, fakeService.CloseCalled);
    }

    // ─── Escenario 5 — Múltiples subastas vencidas ──────────────────────────

    /// <summary>
    /// Scenario 5: Three expired auctions, all claims succeed.
    /// CloseAuctionAutomatically is called once per auction.
    /// </summary>
    [Fact]
    public async Task ProcessExpiredAuctions_ThreeExpiredAuctions_ClosesAllThree()
    {
        // Arrange
        var auctions = new List<Auction>
        {
            BuildExpiredActiveAuction(id: 100),
            BuildExpiredActiveAuction(id: 200),
            BuildExpiredActiveAuction(id: 300)
        };

        var auctionRepo = new Mock<IAuctionRepository>();

        auctionRepo
            .Setup(r => r.GetExpiredActiveAuctionsAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(auctions);

        auctionRepo.Setup(r => r.TryClaimAutomaticClosureAsync(100)).ReturnsAsync(true);
        auctionRepo.Setup(r => r.TryClaimAutomaticClosureAsync(200)).ReturnsAsync(true);
        auctionRepo.Setup(r => r.TryClaimAutomaticClosureAsync(300)).ReturnsAsync(true);

        var closedIds = new List<int>();
        var fakeService = new FakeAuctionService(id =>
        {
            closedIds.Add(id);
            return Task.FromResult(new AuctionResponseDTO());
        });

        var worker = CreateWorker();

        // Act
        await worker.ProcessExpiredAuctionsAsync(auctionRepo.Object, fakeService);

        // Assert
        Assert.Equal(3, fakeService.CloseCalled);
        Assert.Contains(100, closedIds);
        Assert.Contains(200, closedIds);
        Assert.Contains(300, closedIds);
    }

    // ─── Escenario 6 — Excepción en cierre no detiene otras subastas ─────────

    /// <summary>
    /// Scenario 6: First auction throws during CloseAuctionAutomatically.
    /// Worker continues and closes the second auction without throwing.
    /// </summary>
    [Fact]
    public async Task ProcessExpiredAuctions_ExceptionOnFirst_ContinuesToCloseOthers()
    {
        // Arrange
        var auctionFailing = BuildExpiredActiveAuction(id: 400);
        var auctionOk = BuildExpiredActiveAuction(id: 500);

        var auctionRepo = new Mock<IAuctionRepository>();

        auctionRepo
            .Setup(r => r.GetExpiredActiveAuctionsAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Auction> { auctionFailing, auctionOk });

        auctionRepo.Setup(r => r.TryClaimAutomaticClosureAsync(400)).ReturnsAsync(true);
        auctionRepo.Setup(r => r.TryClaimAutomaticClosureAsync(500)).ReturnsAsync(true);

        var successfullyClosed = new List<int>();
        var fakeService = new FakeAuctionService(id =>
        {
            if (id == 400) throw new InvalidOperationException("Simulated failure");
            successfullyClosed.Add(id);
            return Task.FromResult(new AuctionResponseDTO());
        });

        var worker = CreateWorker();

        // Act — must not throw (worker catches per-auction exceptions)
        await worker.ProcessExpiredAuctionsAsync(auctionRepo.Object, fakeService);

        // Assert: second auction was closed despite first failing
        Assert.Contains(500, successfullyClosed);
    }
}
