using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Figuritas.Shared.DTO;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model;
using Xunit;

namespace Figuritas.Api.Tests;

/// <summary>
/// Concurrent integration tests that verify each optimistic-locking / race-condition fix
/// under actual parallel pressure. All tests fire multiple HTTP requests via Task.WhenAll
/// and assert DB-level invariants after the storm settles.
///
///  T1 — RC-02/03: Concurrent proposal cancellation → stock restored exactly once.
///  T2 — RC-04:    Concurrent proposal creation with same Qty=1 sticker → exactly one wins.
///  T3 — RC-15:    Concurrent ratings for same exchange → exactly one rating added.
///  T4 — RC-07:    Concurrent bids from same bidder on same auction → only one active bid.
///  T5 — RC-12:    Concurrent PATCH on same UserSticker → final quantity coherent.
///  T6 — RC-05:    Concurrent proposal acceptance, shared Qty=1 sticker → loser rolls back to Rejected.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class ConcurrencyTests : IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public ConcurrencyTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.CleanMutableCollectionsAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<UserResponseDTO> RegisterUserAsync(string username)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { Username = username, Password = "Password123" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserResponseDTO>(JsonOpts))!;
    }

    private async Task<string> LoginAsync(string username)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { Username = username, Password = "Password123" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    private HttpClient AuthClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<List<Sticker>> GetCatalogAsync(int count = 10)
    {
        var response = await _client.GetAsync($"/api/stickers?Page=1&PageSize={count}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<Sticker>>(JsonOpts))!;
    }

    private async Task<UserStickerResponseDTO> PublishStickerAsync(
        HttpClient client, int userId, int catalogStickerId,
        int quantity = 2, bool canBeDirectlyExchanged = true, bool canBeAuctioned = false)
    {
        var dto = new PostUserStickerRequestDTO
        {
            StickerId = catalogStickerId,
            Quantity = quantity,
            CanBeDirectlyExchanged = canBeDirectlyExchanged,
            CanBeAuctioned = canBeAuctioned
        };
        var response = await client.PostAsJsonAsync($"/api/users/{userId}/stickers", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserStickerResponseDTO>(JsonOpts))!;
    }

    private async Task<ExchangeProposalResponseDTO> CreateProposalAsync(
        HttpClient client, int proposedUserId, List<int> offeredUserStickerIds, int requestedUserStickerId)
    {
        var dto = new PostExchangeProposalRequestDTO
        {
            ProposedUserId = proposedUserId,
            OfferedUserStickerIds = offeredUserStickerIds,
            RequestedUserStickerId = requestedUserStickerId
        };
        var response = await client.PostAsJsonAsync("/api/exchange-proposals", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExchangeProposalResponseDTO>(JsonOpts))!;
    }

    /// <summary>
    /// Reads the Quantity of a UserSticker via the HTTP endpoint.
    /// Returns null if the sticker is not found (inactive or does not exist).
    /// </summary>
    private async Task<int?> GetStickerQuantityAsync(HttpClient client, int userId, int userStickerId)
    {
        var response = await client.GetAsync($"/api/users/{userId}/stickers/{userStickerId}");
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("quantity").GetInt32();
    }

    private async Task<int> GetExchangeIdAsync(HttpClient client, int proposalId)
    {
        var response = await client.GetAsync($"/api/exchange-proposals/{proposalId}/exchange");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetInt32();
    }

    private async Task<AuctionResponseDTO> CreateAuctionAsync(
        HttpClient client, int userStickerId)
    {
        var dto = new PostAuctionRequestDTO
        {
            UserStickerId = userStickerId,
            MinimumOfferStickerIds = new List<int>(),
            EndsAt = DateTime.UtcNow.AddHours(2)
        };
        var response = await client.PostAsJsonAsync("/api/auctions", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuctionResponseDTO>(JsonOpts))!;
    }

    // ─── T1: Concurrent cancel (RC-02/03) ───────────────────────────────────

    /// <summary>
    /// T1 — RC-02/03: Two goroutines try to cancel the same pending proposal at the same time.
    /// TryTransitionFromPendingAsync is atomic: exactly one transition succeeds.
    /// Post-condition: the proponent's reserved stock is restored exactly once (Quantity == 1).
    /// </summary>
    [Fact]
    public async Task T1_ConcurrentCancel_StockRestoredExactlyOnce()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"t1_a_{suffix}");
        var userB = await RegisterUserAsync($"t1_b_{suffix}");
        var tokenA = await LoginAsync($"t1_a_{suffix}");
        var tokenB = await LoginAsync($"t1_b_{suffix}");
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync();
        // A offers sticker with exactly qty=1 so there is no slack.
        var stickerA = await PublishStickerAsync(clientA, userA.Id, catalog[0].Id, quantity: 1);
        var stickerB = await PublishStickerAsync(clientB, userB.Id, catalog[1].Id, quantity: 1);

        // Create the proposal — stock reserved: stickerA.Quantity drops to 0.
        var proposal = await CreateProposalAsync(clientA, userB.Id, new List<int> { stickerA.Id }, stickerB.Id);

        // Fire two concurrent cancels from user A.
        var t1 = clientA.PostAsync($"/api/exchange-proposals/{proposal.Id}/cancel", null);
        var t2 = AuthClient(tokenA).PostAsync($"/api/exchange-proposals/{proposal.Id}/cancel", null);
        var results = await Task.WhenAll(t1, t2);

        var statuses = results.Select(r => (int)r.StatusCode).OrderBy(s => s).ToList();

        // Exactly one cancel must succeed (200). The other must be a non-2xx
        // (either 409 Conflict from the atomic guard, or 400 BadRequest from the pre-check).
        var successCount = statuses.Count(s => s == 200);
        Assert.Equal(1, successCount);
        Assert.All(results, r =>
            Assert.True(r.StatusCode == HttpStatusCode.OK
                        || r.StatusCode == HttpStatusCode.Conflict
                        || r.StatusCode == HttpStatusCode.BadRequest,
                $"Unexpected status {r.StatusCode}"));

        // Post-condition: sticker must be restored to qty=1, not 2.
        var qty = await GetStickerQuantityAsync(clientA, userA.Id, stickerA.Id);
        Assert.Equal(1, qty);
    }

    // ─── T2: Concurrent proposal creation with shared Qty=1 sticker (RC-04) ─

    /// <summary>
    /// T2 — RC-04: Two proposals are submitted concurrently, both offering the same sticker
    /// (Quantity=1). TryReserveOneUnitAsync is atomic: only one reservation can succeed.
    /// Post-condition: exactly one proposal created; sticker stock is not negative.
    /// </summary>
    [Fact]
    public async Task T2_ConcurrentProposalCreation_OnlyOneWins_StockNotNegative()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"t2_a_{suffix}");
        var userB = await RegisterUserAsync($"t2_b_{suffix}");
        var userC = await RegisterUserAsync($"t2_c_{suffix}");
        var tokenA = await LoginAsync($"t2_a_{suffix}");
        var tokenB = await LoginAsync($"t2_b_{suffix}");
        var tokenC = await LoginAsync($"t2_c_{suffix}");
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);
        var clientC = AuthClient(tokenC);

        var catalog = await GetCatalogAsync();
        // A has exactly one unit of sticker[0] — only one proposal can reserve it.
        var stickerA = await PublishStickerAsync(clientA, userA.Id, catalog[0].Id, quantity: 1);
        var stickerB = await PublishStickerAsync(clientB, userB.Id, catalog[1].Id, quantity: 1);
        var stickerC = await PublishStickerAsync(clientC, userC.Id, catalog[2].Id, quantity: 1);

        // Two concurrent proposals from A, both offering the same qty=1 sticker.
        var dtoToB = new PostExchangeProposalRequestDTO
        {
            ProposedUserId = userB.Id,
            OfferedUserStickerIds = new List<int> { stickerA.Id },
            RequestedUserStickerId = stickerB.Id
        };
        var dtoToC = new PostExchangeProposalRequestDTO
        {
            ProposedUserId = userC.Id,
            OfferedUserStickerIds = new List<int> { stickerA.Id },
            RequestedUserStickerId = stickerC.Id
        };

        var t1 = clientA.PostAsJsonAsync("/api/exchange-proposals", dtoToB);
        var t2 = AuthClient(tokenA).PostAsJsonAsync("/api/exchange-proposals", dtoToC);
        var results = await Task.WhenAll(t1, t2);

        var statuses = results.Select(r => (int)r.StatusCode).ToList();
        var successCount = statuses.Count(s => s is 200 or 201);
        Assert.Equal(1, successCount);

        // The failed request must be a 400 (insufficient stock).
        Assert.Contains(statuses, s => s == 400);

        // Post-condition: sticker is fully reserved (qty=0 → active=false → GET returns 404).
        var qty = await GetStickerQuantityAsync(clientA, userA.Id, stickerA.Id);
        Assert.Null(qty);
    }

    // ─── T3: Concurrent ratings for the same exchange (RC-15) ───────────────

    /// <summary>
    /// T3 — RC-15: The same rater fires two concurrent POST /api/ratings for the same exchange.
    /// TryAddRatingAsync uses $push conditioned on $not $elemMatch: only one push lands.
    /// Post-condition: exactly one rating is stored for that (raterUserId, exchangeId) pair.
    /// </summary>
    [Fact]
    public async Task T3_ConcurrentRatings_ExactlyOneAdded()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"t3_a_{suffix}");
        var userB = await RegisterUserAsync($"t3_b_{suffix}");
        var tokenA = await LoginAsync($"t3_a_{suffix}");
        var tokenB = await LoginAsync($"t3_b_{suffix}");
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync();
        var stickerA = await PublishStickerAsync(clientA, userA.Id, catalog[0].Id, quantity: 2);
        var stickerB = await PublishStickerAsync(clientB, userB.Id, catalog[1].Id, quantity: 2);

        var proposal = await CreateProposalAsync(clientA, userB.Id, new List<int> { stickerA.Id }, stickerB.Id);
        var acceptResp = await clientB.PostAsync($"/api/exchange-proposals/{proposal.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptResp.StatusCode);

        var exchangeId = await GetExchangeIdAsync(clientA, proposal.Id);

        var ratingDto = new PostRatingRequestDTO
        {
            ExchangeId = exchangeId,
            TargetUserId = userB.Id,
            Stars = 5,
            Comment = "Great!"
        };

        // Two concurrent rating requests from A for the same exchange.
        var r1 = clientA.PostAsJsonAsync("/api/ratings", ratingDto);
        var r2 = AuthClient(tokenA).PostAsJsonAsync("/api/ratings", ratingDto);
        var results = await Task.WhenAll(r1, r2);

        var statuses = results.Select(r => (int)r.StatusCode).ToList();
        var successCount = statuses.Count(s => s is 200 or 201);
        Assert.Equal(1, successCount);

        // The duplicate must return 409 Conflict ("Already rated").
        Assert.Contains(statuses, s => s == 409);

        // Post-condition: user B has exactly 1 rating for this exchange.
        var ratingsResp = await clientA.GetAsync($"/api/users/{userB.Id}/ratings?Page=1&PageSize=50");
        ratingsResp.EnsureSuccessStatusCode();
        var ratings = (await ratingsResp.Content.ReadFromJsonAsync<List<RatingResponseDTO>>(JsonOpts))!;
        var ratingsForExchange = ratings.Where(r => r.ExchangeId == exchangeId && r.EvaluatorUserId == userA.Id).ToList();
        Assert.Single(ratingsForExchange);
    }

    // ─── T4: Concurrent bids from the same bidder (RC-07) ───────────────────

    /// <summary>
    /// T4 — RC-07: The same bidder fires two concurrent bids on the same auction,
    /// each offering a different sticker. The unique partial index on (AuctionId, BidderId)
    /// where Status==Active ensures only one insert succeeds. The losing request rolls back
    /// its reserved stickers via IncrementQuantityAndActivateAsync.
    /// Post-condition: the auction has exactly one active offer from the bidder.
    /// </summary>
    [Fact]
    public async Task T4_ConcurrentBidsFromSameBidder_OnlyOneActiveOffer()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"t4_auc_{suffix}");
        var bidder = await RegisterUserAsync($"t4_bid_{suffix}");
        var tokenAuc = await LoginAsync($"t4_auc_{suffix}");
        var tokenBid = await LoginAsync($"t4_bid_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientBid = AuthClient(tokenBid);

        var catalog = await GetCatalogAsync();

        // Auctioneer lists a sticker.
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 1, canBeAuctioned: true, canBeDirectlyExchanged: false);
        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        // Bidder has two separate stickers to offer in separate bids.
        var bidSticker1 = await PublishStickerAsync(clientBid, bidder.Id, catalog[1].Id, quantity: 1);
        var bidSticker2 = await PublishStickerAsync(clientBid, bidder.Id, catalog[2].Id, quantity: 1);

        // Two concurrent bids from the same bidder, each offering a different sticker.
        var dto1 = new PostAuctionOfferRequestDTO { OfferedUserStickerIds = new List<int> { bidSticker1.Id } };
        var dto2 = new PostAuctionOfferRequestDTO { OfferedUserStickerIds = new List<int> { bidSticker2.Id } };

        var b1 = clientBid.PostAsJsonAsync($"/api/auctions/{auction.Id}/offers", dto1);
        var b2 = AuthClient(tokenBid).PostAsJsonAsync($"/api/auctions/{auction.Id}/offers", dto2);
        var results = await Task.WhenAll(b1, b2);

        var statuses = results.Select(r => (int)r.StatusCode).ToList();
        var successCount = statuses.Count(s => s is 200 or 201);
        Assert.Equal(1, successCount);

        // The duplicate bid must fail (400 from DuplicateKey guard).
        Assert.Contains(statuses, s => s == 400);

        // Post-condition: exactly one active offer on the auction.
        var offersResp = await _client.GetAsync($"/api/auctions/{auction.Id}/offers");
        offersResp.EnsureSuccessStatusCode();
        var offers = (await offersResp.Content.ReadFromJsonAsync<List<AuctionOfferResponseDTO>>(JsonOpts))!;
        Assert.Single(offers);
    }

    // ─── T6: Concurrent proposal acceptance with shared Qty=1 sticker (RC-05 / GAP-A) ─

    /// <summary>
    /// T6 — RC-05 / GAP-A: User B owns sticker Y with Qty=1. Two proposals from different
    /// proponents (P1 from A, P2 from C) both request sticker Y. B accepts both concurrently.
    /// AcceptAtomically succeeds for both (distinct documents), but TryReserveOneUnitAsync
    /// is atomic: only one acceptance can decrement the stock. The loser rolls back:
    ///   - The losing proposal transitions from Accepted → Rejected.
    ///   - The loser's offered sticker is released (IncrementQuantityAndActivateAsync).
    /// Post-conditions:
    ///   1. Exactly one 200 response.
    ///   2. stickerY is consumed (404).
    ///   3. The losing proposal is in state "Rejected".
    ///   4. The offered sticker of the losing proponent is restored (Qty=1).
    /// </summary>
    [Fact]
    public async Task T6_ConcurrentProposalAcceptance_SharedSticker_OnlyOneSucceeds()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"t6_a_{suffix}");
        var userB = await RegisterUserAsync($"t6_b_{suffix}");
        var userC = await RegisterUserAsync($"t6_c_{suffix}");
        var tokenA = await LoginAsync($"t6_a_{suffix}");
        var tokenB = await LoginAsync($"t6_b_{suffix}");
        var tokenC = await LoginAsync($"t6_c_{suffix}");
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);
        var clientC = AuthClient(tokenC);

        var catalog = await GetCatalogAsync(5);

        // B has exactly 1 unit of stickerY — the contested resource.
        var stickerY = await PublishStickerAsync(clientB, userB.Id, catalog[0].Id,
            quantity: 1, canBeDirectlyExchanged: true);

        // A offers stickerA1 to B, requesting stickerY.
        var stickerA1 = await PublishStickerAsync(clientA, userA.Id, catalog[1].Id,
            quantity: 1, canBeDirectlyExchanged: true);

        // C offers stickerC1 to B, also requesting stickerY.
        var stickerC1 = await PublishStickerAsync(clientC, userC.Id, catalog[2].Id,
            quantity: 1, canBeDirectlyExchanged: true);

        var proposalP1 = await CreateProposalAsync(clientA, userB.Id,
            new List<int> { stickerA1.Id }, stickerY.Id);
        var proposalP2 = await CreateProposalAsync(clientC, userB.Id,
            new List<int> { stickerC1.Id }, stickerY.Id);

        // B accepts both proposals concurrently.
        var accept1 = clientB.PostAsync($"/api/exchange-proposals/{proposalP1.Id}/accept", null);
        var accept2 = AuthClient(tokenB).PostAsync($"/api/exchange-proposals/{proposalP2.Id}/accept", null);
        var results = await Task.WhenAll(accept1, accept2);

        var pairs = results
            .Zip(new[] { proposalP1, proposalP2 }, (r, p) => (Response: r, Proposal: p))
            .ToList();

        var statuses = pairs.Select(x => (int)x.Response.StatusCode).OrderBy(s => s).ToList();

        // Exactly one acceptance must succeed (200).
        Assert.Equal(1, statuses.Count(s => s == 200));
        Assert.Equal(1, statuses.Count(s => s != 200));

        // Post-condition 1: stickerY must no longer be accessible (Qty=0 → Active=false → 404).
        var qty = await GetStickerQuantityAsync(clientB, userB.Id, stickerY.Id);
        Assert.Null(qty);

        // Identify which proposal won and which lost.
        var winner = pairs.First(x => x.Response.IsSuccessStatusCode);
        var loser  = pairs.First(x => !x.Response.IsSuccessStatusCode);

        // Post-condition 2: The losing proposal must be in state "Rejected".
        var loserProposalResp = await clientB.GetAsync($"/api/exchange-proposals/{loser.Proposal.Id}");
        loserProposalResp.EnsureSuccessStatusCode();
        var loserProposalBody = await loserProposalResp.Content.ReadFromJsonAsync<JsonElement>();
        var loserState = loserProposalBody.GetProperty("state").GetString();
        Assert.Equal("Rejected", loserState);

        // Post-condition 3: The offered sticker of the losing proponent must be restored (Qty=1).
        // P1 was offered by A (stickerA1), P2 was offered by C (stickerC1).
        var (loserProponentClient, loserProponentId, loserOfferedStickerId) =
            loser.Proposal.Id == proposalP1.Id
                ? (clientA, userA.Id, stickerA1.Id)
                : (clientC, userC.Id, stickerC1.Id);

        var restoredQty = await GetStickerQuantityAsync(loserProponentClient, loserProponentId, loserOfferedStickerId);
        Assert.Equal(1, restoredQty);
    }

    // ─── T5: Concurrent PATCH on the same UserSticker (RC-12) ───────────────

    /// <summary>
    /// T5 — RC-12: Multiple goroutines concurrently PATCH the same UserSticker with
    /// different Quantity values. Version-conditioned ReplaceOne ensures no silent
    /// lost-update: each writer either commits against the version it read or retries up to
    /// 3 times before yielding a 409.
    /// Post-condition: the final Quantity is always one of the requested values — never
    /// a corrupted intermediate or the original starting value written twice.
    /// </summary>
    [Fact]
    public async Task T5_ConcurrentPatch_FinalQuantityIsCoherent()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"t5_a_{suffix}");
        var tokenA = await LoginAsync($"t5_a_{suffix}");
        var clientA = AuthClient(tokenA);

        var catalog = await GetCatalogAsync();
        var sticker = await PublishStickerAsync(clientA, userA.Id, catalog[0].Id, quantity: 5);

        // Fire 6 concurrent PATCHes with distinct requested quantities.
        var requestedQuantities = new[] { 10, 20, 30, 40, 50, 60 };
        var tasks = requestedQuantities.Select(qty =>
        {
            var client = AuthClient(tokenA);
            var dto = new PatchUserStickerDTO { Quantity = qty };
            return client.PatchAsJsonAsync(
                $"/api/users/{userA.Id}/stickers/{sticker.Id}", dto);
        }).ToList();

        var results = await Task.WhenAll(tasks);
        var statuses = results.Select(r => (int)r.StatusCode).ToList();

        // Every response must be either 200 (success) or 409 (optimistic concurrency failure).
        Assert.All(statuses, s =>
            Assert.True(s == 200 || s == 409,
                $"Expected 200 or 409 but got {s}"));

        // At least one must have succeeded.
        Assert.Contains(statuses, s => s == 200);

        // Determine which quantities resulted in 200 responses.
        var succeededQuantities = results
            .Zip(requestedQuantities, (r, q) => (Status: (int)r.StatusCode, Quantity: q))
            .Where(x => x.Status == 200)
            .Select(x => x.Quantity)
            .ToHashSet();

        // Read the actual final quantity from MongoDB via HTTP.
        var finalQty = await GetStickerQuantityAsync(clientA, userA.Id, sticker.Id);
        Assert.NotNull(finalQty);

        // The final quantity must equal one of the values that got a 200 response.
        // This verifies no lost-update: the last committed write determines the final state.
        Assert.Contains(finalQty.Value, succeededQuantities);
    }
}
