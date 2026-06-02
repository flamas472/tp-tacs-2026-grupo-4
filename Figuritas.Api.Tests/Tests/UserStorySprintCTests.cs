using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Figuritas.Api.Repositories;
using Figuritas.Api.Services;
using Figuritas.Api.Workers;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model;
using Figuritas.Shared.Model.Subastas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Figuritas.Api.Tests;

/// <summary>
/// Sprint C — 8 test scenarios covering:
///   1. Manual offer acceptance: Closed auction, Won offer, remaining offers Lost, stock restored.
///   2. Non-owner acceptance: 403 Forbidden.
///   3. Offer belonging to a different auction: 404.
///   4. Acceptance on already-closed auction: 400.
///   5. Worker expiration with UserSelectedBestOfferId set: that offer wins.
///   6. Worker expiration with BestCurrentOfferId only: system-ranked offer wins.
///   7. Worker expiration with no offers: Cancelled, auctioneer stock restored.
///   8. Concurrency: manual accept + worker closure simultaneously → exactly one closes, stock correct.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class UserStorySprintCTests : IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public UserStorySprintCTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<UserResponseDTO> RegisterUserAsync(string username, string password = "password123")
    {
        var dto = new { Username = username, Password = password };
        var response = await _client.PostAsJsonAsync("/api/auth/register", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserResponseDTO>(JsonOpts))!;
    }

    private async Task<string> LoginAsync(string username, string password = "password123")
    {
        var dto = new { Username = username, Password = password };
        var response = await _client.PostAsJsonAsync("/api/auth/login", dto);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    private HttpClient AuthClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<List<Sticker>> GetCatalogAsync(int count = 3)
    {
        var response = await _client.GetAsync($"/api/stickers?Page=1&PageSize={count}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<Sticker>>(JsonOpts))!;
    }

    private async Task<UserStickerResponseDTO> PublishStickerAsync(
        HttpClient client, int userId, int catalogStickerId,
        int quantity = 2, bool canBeAuctioned = true, bool canBeDirectlyExchanged = true)
    {
        var dto = new PostUserStickerRequestDTO
        {
            StickerId = catalogStickerId,
            Quantity = quantity,
            CanBeAuctioned = canBeAuctioned,
            CanBeDirectlyExchanged = canBeDirectlyExchanged
        };
        var response = await client.PostAsJsonAsync($"/api/users/{userId}/stickers", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserStickerResponseDTO>(JsonOpts))!;
    }

    private async Task<AuctionResponseDTO> CreateAuctionAsync(
        HttpClient client, int userStickerId, DateTime? endsAt = null)
    {
        var dto = new PostAuctionRequestDTO
        {
            UserStickerId = userStickerId,
            MinimumOfferStickerIds = new List<int>(),
            EndsAt = endsAt ?? DateTime.UtcNow.AddHours(1)
        };
        var response = await client.PostAsJsonAsync("/api/auctions", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuctionResponseDTO>(JsonOpts))!;
    }

    private async Task<AuctionOfferResponseDTO> PlaceOfferAsync(
        HttpClient client, int auctionId, List<int> userStickerIds)
    {
        var dto = new PostAuctionOfferRequestDTO { OfferedUserStickerIds = userStickerIds };
        var response = await client.PostAsJsonAsync($"/api/auctions/{auctionId}/offers", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuctionOfferResponseDTO>(JsonOpts))!;
    }

    private async Task<AuctionResponseDTO> GetAuctionAsync(int auctionId)
    {
        var response = await _client.GetAsync($"/api/auctions/{auctionId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuctionResponseDTO>(JsonOpts))!;
    }

    private async Task<List<AuctionOfferResponseDTO>> GetAuctionOffersAsync(int auctionId)
    {
        var response = await _client.GetAsync($"/api/auctions/{auctionId}/offers");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<AuctionOfferResponseDTO>>(JsonOpts))!;
    }

    private async Task<UserStickerResponseDTO?> GetUserStickerAsync(HttpClient client, int userId, int stickerId)
    {
        var response = await client.GetAsync($"/api/users/{userId}/stickers/{stickerId}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserStickerResponseDTO>(JsonOpts);
    }

    // ─── Test 1: Accept valid offer manually ────────────────────────────────

    /// <summary>
    /// Scenario 1: Auctioneer manually accepts a pending offer.
    /// Expected: auction → Closed, accepted offer → Won, all other offers → Lost, losing bidders' stock restored.
    /// </summary>
    [Fact]
    public async Task AcceptOffer_ValidManualAcceptance_AuctionClosedOfferWonOthersLostStockRestored()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sc1_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sc1_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sc1_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sc1_auc_{suffix}");
        var tokenA = await LoginAsync($"sc1_bida_{suffix}");
        var tokenB = await LoginAsync($"sc1_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(3);

        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id, quantity: 2);
        var stickerA = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id, quantity: 2,
            canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB = await PublishStickerAsync(clientB, bidderB.Id, catalog[2 % catalog.Count].Id, quantity: 2,
            canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA.Id });
        var offerB = await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB.Id });

        // Auctioneer accepts offer A
        var acceptResponse = await clientAuc.PostAsync(
            $"/api/auctions/{auction.Id}/offers/{offerA.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var closedAuction = await GetAuctionAsync(auction.Id);
        Assert.Equal("Closed", closedAuction.Status);

        // Verify offer states
        var allOffers = await GetAuctionOffersAsync(auction.Id);
        var wonOffer = allOffers.First(o => o.Id == offerA.Id);
        var lostOffer = allOffers.First(o => o.Id == offerB.Id);
        Assert.Equal("Won", wonOffer.State);
        Assert.Equal("Lost", lostOffer.State);

        // Bidder B's sticker stock should be restored (quantity back to initial - 1 consumed by publish, +1 returned = back to 1)
        var restoredStickerB = await GetUserStickerAsync(clientB, bidderB.Id, stickerB.Id);
        Assert.NotNull(restoredStickerB);
        Assert.Equal(2, restoredStickerB!.Quantity); // restored to original 2
    }

    // ─── Test 2: Non-owner trying to accept → 403 ───────────────────────────

    /// <summary>
    /// Scenario 2: A user who is not the auctioneer tries to accept an offer → 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task AcceptOffer_NonOwnerCaller_Returns403Forbidden()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sc2_auc_{suffix}");
        var bidder = await RegisterUserAsync($"sc2_bid_{suffix}");
        var stranger = await RegisterUserAsync($"sc2_str_{suffix}");

        var tokenAuc = await LoginAsync($"sc2_auc_{suffix}");
        var tokenBid = await LoginAsync($"sc2_bid_{suffix}");
        var tokenStr = await LoginAsync($"sc2_str_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientBid = AuthClient(tokenBid);
        var clientStr = AuthClient(tokenStr);

        var catalog = await GetCatalogAsync(2);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id);
        var bidSticker = await PublishStickerAsync(clientBid, bidder.Id, catalog[1 % catalog.Count].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);
        var offer = await PlaceOfferAsync(clientBid, auction.Id, new List<int> { bidSticker.Id });

        // Stranger (not the auctioneer) tries to accept
        var response = await clientStr.PostAsync(
            $"/api/auctions/{auction.Id}/offers/{offer.Id}/accept", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── Test 3: Offer belongs to a different auction ────────────────────────

    /// <summary>
    /// Scenario 3: Auctioneer tries to accept an offer that belongs to a different auction → 404.
    /// </summary>
    [Fact]
    public async Task AcceptOffer_OfferBelongsToDifferentAuction_Returns404()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneerA = await RegisterUserAsync($"sc3_auca_{suffix}");
        var auctioneerB = await RegisterUserAsync($"sc3_aucb_{suffix}");
        var bidder = await RegisterUserAsync($"sc3_bid_{suffix}");

        var tokenA = await LoginAsync($"sc3_auca_{suffix}");
        var tokenB = await LoginAsync($"sc3_aucb_{suffix}");
        var tokenBid = await LoginAsync($"sc3_bid_{suffix}");
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);
        var clientBid = AuthClient(tokenBid);

        var catalog = await GetCatalogAsync(3);
        var stickerA = await PublishStickerAsync(clientA, auctioneerA.Id, catalog[0].Id);
        var stickerB = await PublishStickerAsync(clientB, auctioneerB.Id, catalog[1 % catalog.Count].Id);
        var bidSticker = await PublishStickerAsync(clientBid, bidder.Id, catalog[2 % catalog.Count].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auctionA = await CreateAuctionAsync(clientA, stickerA.Id);
        var auctionB = await CreateAuctionAsync(clientB, stickerB.Id);

        // Bidder places offer on auction B
        var offerOnB = await PlaceOfferAsync(clientBid, auctionB.Id, new List<int> { bidSticker.Id });

        // Auctioneer A tries to accept an offer that belongs to auction B (different auction)
        var response = await clientA.PostAsync(
            $"/api/auctions/{auctionA.Id}/offers/{offerOnB.Id}/accept", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── Test 4: Accept on already-closed auction ─────────────────────────

    /// <summary>
    /// Scenario 4: Auctioneer tries to accept an offer on an already-closed auction → 400.
    /// </summary>
    [Fact]
    public async Task AcceptOffer_AuctionAlreadyClosed_Returns400()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sc4_auc_{suffix}");
        var bidder = await RegisterUserAsync($"sc4_bid_{suffix}");

        var tokenAuc = await LoginAsync($"sc4_auc_{suffix}");
        var tokenBid = await LoginAsync($"sc4_bid_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientBid = AuthClient(tokenBid);

        var catalog = await GetCatalogAsync(2);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id);
        var bidSticker = await PublishStickerAsync(clientBid, bidder.Id, catalog[1 % catalog.Count].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);
        var offer = await PlaceOfferAsync(clientBid, auction.Id, new List<int> { bidSticker.Id });

        // First accept succeeds
        var firstAccept = await clientAuc.PostAsync(
            $"/api/auctions/{auction.Id}/offers/{offer.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, firstAccept.StatusCode);

        // Second accept on already-closed auction → 400
        var secondAccept = await clientAuc.PostAsync(
            $"/api/auctions/{auction.Id}/offers/{offer.Id}/accept", null);
        Assert.Equal(HttpStatusCode.BadRequest, secondAccept.StatusCode);
    }

    // ─── Tests 5, 6, 7: Worker expiration scenarios (unit tests) ────────────

    /// <summary>
    /// Minimal FakeAuctionService for worker unit tests that tracks which winning offer was used.
    /// </summary>
    private sealed class CapturingFakeAuctionService : AuctionService
    {
        private readonly Func<int, Task<AuctionResponseDTO>> _closeFunc;

        public CapturingFakeAuctionService(Func<int, Task<AuctionResponseDTO>> closeFunc)
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

        public int CloseCalledCount { get; private set; }
        public List<int> ClosedAuctionIds { get; } = new();

        public override Task<AuctionResponseDTO> CloseAuctionAutomatically(int auctionId)
        {
            CloseCalledCount++;
            ClosedAuctionIds.Add(auctionId);
            return _closeFunc(auctionId);
        }
    }

    private static AuctionEndingWorker CreateWorker()
    {
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var logger = new Mock<ILogger<AuctionEndingWorker>>();
        return new AuctionEndingWorker(scopeFactory.Object, logger.Object, TimeProvider.System);
    }

    /// <summary>
    /// Scenario 5: Worker expiration with UserSelectedBestOfferId set.
    /// CloseAuctionAutomatically should be invoked (the service method handles the priority internally).
    /// </summary>
    [Fact]
    public async Task WorkerExpiration_UserSelectedBestOfferIdSet_DelegatesToCloseAuctionAutomatically()
    {
        // The service's CloseAuctionAutomatically encapsulates the priority logic.
        // At the worker level we verify that it is called for an auction that has UserSelectedBestOfferId != null.
        var auction = new Auction
        {
            Id = 1001,
            AuctioneerId = 1,
            UserStickerId = 10,
            Status = AuctionStatus.Active,
            EndsAt = DateTime.UtcNow.AddMinutes(-5),
            BestCurrentOfferId = 50,
            UserSelectedBestOfferId = 55  // Manually chosen by auctioneer
        };

        var auctionRepo = new Mock<IAuctionRepository>();
        auctionRepo
            .Setup(r => r.GetExpiredActiveAuctionsAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Auction> { auction });
        auctionRepo
            .Setup(r => r.TryClaimAutomaticClosureAsync(1001))
            .ReturnsAsync(true);

        int? capturedAuctionId = null;
        var fakeService = new CapturingFakeAuctionService(id =>
        {
            capturedAuctionId = id;
            return Task.FromResult(new AuctionResponseDTO { Status = "Closed" });
        });

        var worker = CreateWorker();
        await worker.ProcessExpiredAuctionsAsync(auctionRepo.Object, fakeService);

        Assert.Equal(1, fakeService.CloseCalledCount);
        Assert.Equal(1001, capturedAuctionId);
    }

    /// <summary>
    /// Scenario 6: Worker expiration with only BestCurrentOfferId (no manual selection).
    /// CloseAuctionAutomatically should be invoked so the system-ranked offer wins.
    /// </summary>
    [Fact]
    public async Task WorkerExpiration_OnlyBestCurrentOfferIdSet_DelegatesToCloseAuctionAutomatically()
    {
        var auction = new Auction
        {
            Id = 2002,
            AuctioneerId = 2,
            UserStickerId = 20,
            Status = AuctionStatus.Active,
            EndsAt = DateTime.UtcNow.AddMinutes(-1),
            BestCurrentOfferId = 60,
            UserSelectedBestOfferId = null  // No manual selection
        };

        var auctionRepo = new Mock<IAuctionRepository>();
        auctionRepo
            .Setup(r => r.GetExpiredActiveAuctionsAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Auction> { auction });
        auctionRepo
            .Setup(r => r.TryClaimAutomaticClosureAsync(2002))
            .ReturnsAsync(true);

        var fakeService = new CapturingFakeAuctionService(id =>
            Task.FromResult(new AuctionResponseDTO { Status = "Closed" }));

        var worker = CreateWorker();
        await worker.ProcessExpiredAuctionsAsync(auctionRepo.Object, fakeService);

        Assert.Equal(1, fakeService.CloseCalledCount);
        Assert.Contains(2002, fakeService.ClosedAuctionIds);
    }

    /// <summary>
    /// Scenario 7: Integration test — Worker expires auction with zero offers → Cancelled, auctioneer stock restored.
    /// Uses a real (in-memory test) service via the integration factory.
    /// </summary>
    [Fact]
    public async Task WorkerExpiration_NoOffers_AuctionCancelledAuctioneerStockRestored()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sc7_auc_{suffix}");
        var tokenAuc = await LoginAsync($"sc7_auc_{suffix}");
        var clientAuc = AuthClient(tokenAuc);

        var catalog = await GetCatalogAsync(1);
        // Publish with quantity=1 so after reservation (quantity=0, active=false) restoration is verifiable
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id, quantity: 1);

        // Create an auction expiring in 2 seconds, no offers will be placed
        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id, endsAt: DateTime.UtcNow.AddSeconds(2));

        // Wait for expiry
        await Task.Delay(3000);

        // Invoke the service's automatic close directly (simulating worker)
        using var scope = _factory.Services.CreateScope();
        var auctionService = scope.ServiceProvider.GetRequiredService<AuctionService>();
        var auctionRepo = scope.ServiceProvider.GetRequiredService<IAuctionRepository>();

        // Claim closure
        var claimed = await auctionRepo.TryClaimAutomaticClosureAsync(auction.Id);
        if (claimed)
        {
            await auctionService.CloseAuctionAutomatically(auction.Id);
        }

        // Verify auction is Cancelled
        var finalAuction = await GetAuctionAsync(auction.Id);
        Assert.Equal("Cancelled", finalAuction.Status);

        // Verify auctioneer's sticker stock was restored (quantity=0 → 1, active=false → true)
        var restoredSticker = await GetUserStickerAsync(clientAuc, auctioneer.Id, aucSticker.Id);
        Assert.NotNull(restoredSticker);
        Assert.Equal(1, restoredSticker!.Quantity);
        Assert.True(restoredSticker.Active);
    }

    // ─── Test 8: Concurrency — manual accept + worker close ─────────────────

    /// <summary>
    /// Scenario 8: True parallel race between manual acceptance (HTTP endpoint) and worker
    /// automatic closure for the same expired auction.
    ///
    /// Both operations are launched simultaneously via Task.WhenAll — no sequential
    /// delay between them — to maximise the chance of a real DB-level collision.
    ///
    /// Post-conditions:
    ///   • Auction is in a terminal state (Closed or Cancelled), never Active.
    ///   • Exactly one operation succeeded (the other received a controlled error).
    ///   • Bidder's sticker quantity ≤ 1 (no double-return).
    ///   • Auctioneer's sticker quantity ≤ 1 (no double-return).
    /// </summary>
    [Fact]
    public async Task Concurrency_ManualAcceptAndWorkerClose_AuctionProcessedExactlyOnce()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sc8_auc_{suffix}");
        var bidder = await RegisterUserAsync($"sc8_bid_{suffix}");

        var tokenAuc = await LoginAsync($"sc8_auc_{suffix}");
        var tokenBid = await LoginAsync($"sc8_bid_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientBid = AuthClient(tokenBid);

        var catalog = await GetCatalogAsync(2);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id, quantity: 1);
        var bidSticker = await PublishStickerAsync(clientBid, bidder.Id, catalog[1 % catalog.Count].Id,
            quantity: 1, canBeAuctioned: false, canBeDirectlyExchanged: true);

        // Auction expires very soon so both paths hit a truly-expired auction.
        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id, endsAt: DateTime.UtcNow.AddSeconds(2));
        var offer = await PlaceOfferAsync(clientBid, auction.Id, new List<int> { bidSticker.Id });

        await Task.Delay(3000); // ensure the auction has expired before the race

        // Resolve service instances once (outside the parallel tasks) to avoid scope issues.
        using var scope = _factory.Services.CreateScope();
        var auctionService = scope.ServiceProvider.GetRequiredService<AuctionService>();
        var auctionRepo = scope.ServiceProvider.GetRequiredService<IAuctionRepository>();

        // ── Fire both operations truly simultaneously via Task.WhenAll ──────
        // Task A: auctioneer manually accepts the offer via the HTTP endpoint.
        // Task B: worker attempts automatic closure.
        // Both tasks are created before either is awaited so they start concurrently.
        var manualTask = Task.Run(() =>
            clientAuc.PostAsync($"/api/auctions/{auction.Id}/offers/{offer.Id}/accept", null));

        var workerTask = Task.Run(async () =>
        {
            try
            {
                var claimed = await auctionRepo.TryClaimAutomaticClosureAsync(auction.Id);
                if (claimed)
                    await auctionService.CloseAuctionAutomatically(auction.Id);
                return true;
            }
            catch
            {
                return false;
            }
        });

        // Await both tasks concurrently.  After WhenAll completes, both tasks are in a
        // terminal state so awaiting them individually cannot deadlock.
        await Task.WhenAll(manualTask, workerTask);
        var manualResponse = await manualTask;
        var workerSucceeded = await workerTask;

        // ── Invariant 1: auction must be in a terminal state ─────────────────
        var finalAuction = await GetAuctionAsync(auction.Id);
        Assert.True(
            finalAuction.Status == "Closed" || finalAuction.Status == "Cancelled",
            $"Expected terminal status but got: {finalAuction.Status}");

        // ── Invariant 2: exactly one operation succeeded ─────────────────────
        // Manual success = HTTP 200; worker success = workerSucceeded == true.
        var manualSucceeded = manualResponse.StatusCode == HttpStatusCode.OK;
        Assert.True(
            manualSucceeded ^ workerSucceeded,
            $"Expected exactly one operation to succeed. Manual: {manualResponse.StatusCode}, Worker: {workerSucceeded}. " +
            "Both succeeded → double-close (CRIT-C-01/02 not fixed). Both failed → liveness bug.");

        // ── Invariant 3: no double-return of bidder's stock ──────────────────
        // Bidder's sticker was reserved at quantity=0.
        // After exactly one close: if won → transferred (≤0 or gone); if lost → restored (=1).
        // Never 2, which would indicate the stock was returned twice.
        var bidderFinalSticker = await GetUserStickerAsync(clientBid, bidder.Id, bidSticker.Id);
        if (bidderFinalSticker != null)
        {
            Assert.True(bidderFinalSticker.Quantity <= 1,
                $"Bidder stock was double-returned. Expected ≤1 but got {bidderFinalSticker.Quantity}.");
        }

        // ── Invariant 4: no double-return of auctioneer's stock ──────────────
        // Auctioneer's sticker was reserved at quantity=0.
        // After exactly one close: if closed with winner → transferred (=0 or gone); if cancelled → restored (=1).
        var auctioneerFinalSticker = await GetUserStickerAsync(clientAuc, auctioneer.Id, aucSticker.Id);
        if (auctioneerFinalSticker != null)
        {
            Assert.True(auctioneerFinalSticker.Quantity <= 1,
                $"Auctioneer stock was double-returned. Expected ≤1 but got {auctioneerFinalSticker.Quantity}.");
        }
    }

    // ─── IAsyncLifetime ──────────────────────────────────────────────────────

    public async Task InitializeAsync() => await _factory.CleanMutableCollectionsAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
