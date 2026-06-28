using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model;
using Xunit;

namespace Figuritas.Api.Tests;

/// <summary>
/// Integration tests for the best-offer ranking algorithm on auctions.
///
/// The system selects BestCurrentOfferId using three criteria in strict order:
///   1. Most missing stickers of the auctioneer covered.
///   2. Most total stickers offered (when tied on criterion 1).
///   3. Oldest offer — FIFO (when tied on criteria 1 and 2).
///
/// These tests verify each criterion independently, plus regression coverage for
/// US06 (create auction) and US07 (create offer).
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class AuctionBestOfferRankingTests : IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public AuctionBestOfferRankingTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<UserResponseDTO> RegisterUserAsync(string username, string password)
    {
        var dto = new { Username = username, Password = password };
        var response = await _client.PostAsJsonAsync("/api/auth/register", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }))!;
    }

    private async Task<string> LoginAsync(string username, string password)
    {
        var dto = new { Username = username, Password = password };
        var response = await _client.PostAsJsonAsync("/api/auth/login", dto);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<List<Sticker>> GetCatalogStickersAsync(int page, int pageSize)
    {
        var response = await _client.GetAsync($"/api/stickers?Page={page}&PageSize={pageSize}");
        response.EnsureSuccessStatusCode();
        var stickers = await response.Content.ReadFromJsonAsync<List<Sticker>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(stickers);
        Assert.NotEmpty(stickers);
        return stickers!;
    }

    private async Task<UserStickerResponseDTO> PublishUserStickerAsync(
        HttpClient authenticatedClient,
        int userId,
        int catalogStickerId,
        int quantity = 5,
        bool canBeAuctioned = true,
        bool canBeDirectlyExchanged = true)
    {
        var dto = new PostUserStickerRequestDTO
        {
            StickerId = catalogStickerId,
            Quantity = quantity,
            CanBeAuctioned = canBeAuctioned,
            CanBeDirectlyExchanged = canBeDirectlyExchanged
        };
        var response = await authenticatedClient.PostAsJsonAsync($"/api/users/{userId}/stickers", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserStickerResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }))!;
    }

    private async Task<AuctionResponseDTO> CreateAuctionAsync(
        HttpClient authenticatedClient,
        int userStickerId,
        List<int>? minimumOfferStickerIds = null,
        DateTime? endsAt = null)
    {
        var dto = new PostAuctionRequestDTO
        {
            UserStickerId = userStickerId,
            MinimumOfferStickerIds = minimumOfferStickerIds ?? new List<int>(),
            EndsAt = endsAt ?? DateTime.UtcNow.AddHours(2)
        };
        var response = await authenticatedClient.PostAsJsonAsync("/api/auctions", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuctionResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }))!;
    }

    private async Task<AuctionOfferResponseDTO> PlaceOfferAsync(
        HttpClient authenticatedClient,
        int auctionId,
        List<int> offeredUserStickerIds)
    {
        var dto = new PostAuctionOfferRequestDTO
        {
            OfferedUserStickerIds = offeredUserStickerIds
        };
        var response = await authenticatedClient.PostAsJsonAsync($"/api/auctions/{auctionId}/offers", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuctionOfferResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }))!;
    }

    private async Task RegisterMissingStickerAsync(HttpClient authenticatedClient, int userId, int catalogStickerId)
    {
        var dto = new { StickerId = catalogStickerId };
        var response = await authenticatedClient.PostAsJsonAsync($"/api/users/{userId}/missing-stickers", dto);
        response.EnsureSuccessStatusCode();
    }

    private async Task<AuctionResponseDTO> GetAuctionAsync(int auctionId)
    {
        var response = await _client.GetAsync($"/api/auctions/{auctionId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuctionResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }))!;
    }

    // ─── Tests ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Criterion 1: Offer A covers 4 of the auctioneer's missing stickers with 4 total stickers.
    /// Offer B covers 2 missing stickers with 10 total stickers.
    /// Expected winner: Offer A (covers more missing stickers — criterion 1 wins).
    /// </summary>
    [Fact]
    public async Task BestOffer_Criterion1_MoreMissingsCovered_WinsOverMoreTotal()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();

        // Setup: auctioneer A, bidder B (first — 4 stickers), bidder C (second — 10 stickers)
        var auctioneer = await RegisterUserAsync($"bof_c1_auc_{suffix}", "Password123");
        var bidderB = await RegisterUserAsync($"bof_c1_b_{suffix}", "Password123");
        var bidderC = await RegisterUserAsync($"bof_c1_c_{suffix}", "Password123");
        var tokenAuctioneer = await LoginAsync($"bof_c1_auc_{suffix}", "Password123");
        var tokenB = await LoginAsync($"bof_c1_b_{suffix}", "Password123");
        var tokenC = await LoginAsync($"bof_c1_c_{suffix}", "Password123");
        var clientAuctioneer = ClientWithToken(tokenAuctioneer);
        var clientB = ClientWithToken(tokenB);
        var clientC = ClientWithToken(tokenC);

        // Fetch enough catalog stickers: 1 for the auction + 10 for bidder offers + 4 missing
        var catalogStickers = await GetCatalogStickersAsync(1, 20);
        Assert.True(catalogStickers.Count >= 12,
            $"Need at least 12 catalog stickers for criterion-1 test, got {catalogStickers.Count}.");

        // Auctioneer publishes one sticker to auction
        var auctionStickerUs = await PublishUserStickerAsync(
            clientAuctioneer, auctioneer.Id, catalogStickers[0].Id, quantity: 2, canBeAuctioned: true, canBeDirectlyExchanged: false);

        // Auctioneer registers 4 missing stickers (indices 2-5)
        await RegisterMissingStickerAsync(clientAuctioneer, auctioneer.Id, catalogStickers[2].Id);
        await RegisterMissingStickerAsync(clientAuctioneer, auctioneer.Id, catalogStickers[3].Id);
        await RegisterMissingStickerAsync(clientAuctioneer, auctioneer.Id, catalogStickers[4].Id);
        await RegisterMissingStickerAsync(clientAuctioneer, auctioneer.Id, catalogStickers[5].Id);

        var auction = await CreateAuctionAsync(clientAuctioneer, auctionStickerUs.Id);

        // Bidder B publishes 4 stickers — all covering 4 auctioneer missing stickers
        var b1 = await PublishUserStickerAsync(clientB, bidderB.Id, catalogStickers[2].Id);
        var b2 = await PublishUserStickerAsync(clientB, bidderB.Id, catalogStickers[3].Id);
        var b3 = await PublishUserStickerAsync(clientB, bidderB.Id, catalogStickers[4].Id);
        var b4 = await PublishUserStickerAsync(clientB, bidderB.Id, catalogStickers[5].Id);

        // Bidder C publishes 10 stickers — only 2 cover auctioneer missing stickers (indices 2 and 3)
        var c1 = await PublishUserStickerAsync(clientC, bidderC.Id, catalogStickers[2].Id);
        var c2 = await PublishUserStickerAsync(clientC, bidderC.Id, catalogStickers[3].Id);
        var c3 = await PublishUserStickerAsync(clientC, bidderC.Id, catalogStickers[6].Id);
        var c4 = await PublishUserStickerAsync(clientC, bidderC.Id, catalogStickers[7].Id);
        var c5 = await PublishUserStickerAsync(clientC, bidderC.Id, catalogStickers[8].Id);
        var c6 = await PublishUserStickerAsync(clientC, bidderC.Id, catalogStickers[9].Id);
        var c7 = await PublishUserStickerAsync(clientC, bidderC.Id, catalogStickers[10].Id);
        var c8 = await PublishUserStickerAsync(clientC, bidderC.Id, catalogStickers[11].Id);
        var c9 = await PublishUserStickerAsync(clientC, bidderC.Id, catalogStickers[12 % catalogStickers.Count].Id);
        var c10 = await PublishUserStickerAsync(clientC, bidderC.Id, catalogStickers[13 % catalogStickers.Count].Id);

        // Bidder C offers first — 10 stickers, covers 2 missing
        var offerC = await PlaceOfferAsync(clientC, auction.Id,
            new List<int> { c1.Id, c2.Id, c3.Id, c4.Id, c5.Id, c6.Id, c7.Id, c8.Id, c9.Id, c10.Id });

        // After C's offer: C is best so far (only offer)
        var auctionAfterC = await GetAuctionAsync(auction.Id);
        Assert.Equal(offerC.Id, auctionAfterC.BestCurrentOfferId);

        // Bidder B offers second — 4 stickers, covers 4 missing (better than C on criterion 1)
        var offerB = await PlaceOfferAsync(clientB, auction.Id,
            new List<int> { b1.Id, b2.Id, b3.Id, b4.Id });

        // After B's offer: B should win because it covers more missing stickers (4 > 2)
        var auctionAfterB = await GetAuctionAsync(auction.Id);
        Assert.NotNull(auctionAfterB.BestCurrentOfferId);
        Assert.Equal(offerB.Id, auctionAfterB.BestCurrentOfferId);
    }

    /// <summary>
    /// Criterion 2: Both offers cover the same number of the auctioneer's missing stickers.
    /// Offer A covers 3 missing with 5 total stickers. Offer B covers 3 missing with 7 total stickers.
    /// Expected winner: Offer B (more total stickers — criterion 2).
    /// </summary>
    [Fact]
    public async Task BestOffer_Criterion2_MoreTotalStickers_WinsWhenMissingsTied()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();

        var auctioneer = await RegisterUserAsync($"bof_c2_auc_{suffix}", "Password123");
        var bidderA = await RegisterUserAsync($"bof_c2_a_{suffix}", "Password123");
        var bidderB = await RegisterUserAsync($"bof_c2_b_{suffix}", "Password123");
        var tokenAuctioneer = await LoginAsync($"bof_c2_auc_{suffix}", "Password123");
        var tokenA = await LoginAsync($"bof_c2_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"bof_c2_b_{suffix}", "Password123");
        var clientAuctioneer = ClientWithToken(tokenAuctioneer);
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        // Need: 1 for auction + 3 shared missing + 2 extra for A + 4 extra for B = 10 catalog stickers
        var catalogStickers = await GetCatalogStickersAsync(1, 20);
        Assert.True(catalogStickers.Count >= 10,
            $"Need at least 10 catalog stickers for criterion-2 test, got {catalogStickers.Count}.");

        // Auctioneer publishes auction sticker
        var auctionStickerUs = await PublishUserStickerAsync(
            clientAuctioneer, auctioneer.Id, catalogStickers[0].Id, quantity: 2, canBeAuctioned: true, canBeDirectlyExchanged: false);

        // Auctioneer registers 3 missing stickers (indices 1-3)
        await RegisterMissingStickerAsync(clientAuctioneer, auctioneer.Id, catalogStickers[1].Id);
        await RegisterMissingStickerAsync(clientAuctioneer, auctioneer.Id, catalogStickers[2].Id);
        await RegisterMissingStickerAsync(clientAuctioneer, auctioneer.Id, catalogStickers[3].Id);

        var auction = await CreateAuctionAsync(clientAuctioneer, auctionStickerUs.Id);

        // Bidder A: 5 stickers, 3 cover missing (indices 1-3), 2 extras (indices 4-5)
        var a1 = await PublishUserStickerAsync(clientA, bidderA.Id, catalogStickers[1].Id);
        var a2 = await PublishUserStickerAsync(clientA, bidderA.Id, catalogStickers[2].Id);
        var a3 = await PublishUserStickerAsync(clientA, bidderA.Id, catalogStickers[3].Id);
        var a4 = await PublishUserStickerAsync(clientA, bidderA.Id, catalogStickers[4].Id);
        var a5 = await PublishUserStickerAsync(clientA, bidderA.Id, catalogStickers[5].Id);

        // Bidder B: 7 stickers, 3 cover missing (indices 1-3), 4 extras (indices 6-9)
        var b1 = await PublishUserStickerAsync(clientB, bidderB.Id, catalogStickers[1].Id);
        var b2 = await PublishUserStickerAsync(clientB, bidderB.Id, catalogStickers[2].Id);
        var b3 = await PublishUserStickerAsync(clientB, bidderB.Id, catalogStickers[3].Id);
        var b4 = await PublishUserStickerAsync(clientB, bidderB.Id, catalogStickers[6].Id);
        var b5 = await PublishUserStickerAsync(clientB, bidderB.Id, catalogStickers[7].Id);
        var b6 = await PublishUserStickerAsync(clientB, bidderB.Id, catalogStickers[8].Id);
        var b7 = await PublishUserStickerAsync(clientB, bidderB.Id, catalogStickers[9].Id);

        // Bidder A offers first — 5 stickers, 3 cover missing
        var offerA = await PlaceOfferAsync(clientA, auction.Id,
            new List<int> { a1.Id, a2.Id, a3.Id, a4.Id, a5.Id });

        var auctionAfterA = await GetAuctionAsync(auction.Id);
        Assert.Equal(offerA.Id, auctionAfterA.BestCurrentOfferId);

        // Bidder B offers second — 7 stickers, same 3 cover missing but more total
        var offerB = await PlaceOfferAsync(clientB, auction.Id,
            new List<int> { b1.Id, b2.Id, b3.Id, b4.Id, b5.Id, b6.Id, b7.Id });

        // B should win: tied on missing (3 = 3), but B offers more total (7 > 5)
        var auctionAfterB = await GetAuctionAsync(auction.Id);
        Assert.NotNull(auctionAfterB.BestCurrentOfferId);
        Assert.Equal(offerB.Id, auctionAfterB.BestCurrentOfferId);
    }

    /// <summary>
    /// Criterion 3 (FIFO): Both offers cover the same missing stickers and offer the same total count.
    /// Offer A is placed first. Offer B is placed second.
    /// Expected winner: Offer A (oldest offer — FIFO criterion 3).
    /// </summary>
    [Fact]
    public async Task BestOffer_Criterion3_OlderOffer_WinsWhenBothCriterionsTied()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();

        var auctioneer = await RegisterUserAsync($"bof_c3_auc_{suffix}", "Password123");
        var bidderA = await RegisterUserAsync($"bof_c3_a_{suffix}", "Password123");
        var bidderB = await RegisterUserAsync($"bof_c3_b_{suffix}", "Password123");
        var tokenAuctioneer = await LoginAsync($"bof_c3_auc_{suffix}", "Password123");
        var tokenA = await LoginAsync($"bof_c3_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"bof_c3_b_{suffix}", "Password123");
        var clientAuctioneer = ClientWithToken(tokenAuctioneer);
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        // Need: 1 auction sticker + 2 for A + 2 for B = 5 stickers. Both cover the same 2 missing.
        var catalogStickers = await GetCatalogStickersAsync(1, 20);
        Assert.True(catalogStickers.Count >= 5,
            $"Need at least 5 catalog stickers for criterion-3 test, got {catalogStickers.Count}.");

        // Auctioneer publishes auction sticker
        var auctionStickerUs = await PublishUserStickerAsync(
            clientAuctioneer, auctioneer.Id, catalogStickers[0].Id, quantity: 2, canBeAuctioned: true, canBeDirectlyExchanged: false);

        // Auctioneer registers 2 missing stickers
        await RegisterMissingStickerAsync(clientAuctioneer, auctioneer.Id, catalogStickers[1].Id);
        await RegisterMissingStickerAsync(clientAuctioneer, auctioneer.Id, catalogStickers[2].Id);

        var auction = await CreateAuctionAsync(clientAuctioneer, auctionStickerUs.Id);

        // Bidder A: 2 stickers covering both missing
        var a1 = await PublishUserStickerAsync(clientA, bidderA.Id, catalogStickers[1].Id);
        var a2 = await PublishUserStickerAsync(clientA, bidderA.Id, catalogStickers[2].Id);

        // Bidder B: same 2 catalog sticker types (same covered missing, same count)
        var b1 = await PublishUserStickerAsync(clientB, bidderB.Id, catalogStickers[1].Id);
        var b2 = await PublishUserStickerAsync(clientB, bidderB.Id, catalogStickers[2].Id);

        // Bidder A offers first
        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { a1.Id, a2.Id });

        var auctionAfterA = await GetAuctionAsync(auction.Id);
        Assert.Equal(offerA.Id, auctionAfterA.BestCurrentOfferId);

        // Bidder B offers second — identical score on criteria 1 and 2
        var offerB = await PlaceOfferAsync(clientB, auction.Id, new List<int> { b1.Id, b2.Id });

        // A should remain best: tied on missing and total, but A came first (FIFO)
        var auctionAfterB = await GetAuctionAsync(auction.Id);
        Assert.NotNull(auctionAfterB.BestCurrentOfferId);
        Assert.Equal(offerA.Id, auctionAfterB.BestCurrentOfferId);
    }

    /// <summary>
    /// When a new offer is strictly better than the current best, BestCurrentOfferId must change.
    /// </summary>
    [Fact]
    public async Task BestOffer_WhenBetterOfferArrives_BestCurrentOfferIdChanges()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();

        var auctioneer = await RegisterUserAsync($"bof_change_auc_{suffix}", "Password123");
        var bidderA = await RegisterUserAsync($"bof_change_a_{suffix}", "Password123");
        var bidderB = await RegisterUserAsync($"bof_change_b_{suffix}", "Password123");
        var tokenAuctioneer = await LoginAsync($"bof_change_auc_{suffix}", "Password123");
        var tokenA = await LoginAsync($"bof_change_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"bof_change_b_{suffix}", "Password123");
        var clientAuctioneer = ClientWithToken(tokenAuctioneer);
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 20);
        Assert.True(catalogStickers.Count >= 6,
            $"Need at least 6 catalog stickers for change test, got {catalogStickers.Count}.");

        var auctionStickerUs = await PublishUserStickerAsync(
            clientAuctioneer, auctioneer.Id, catalogStickers[0].Id, quantity: 2, canBeAuctioned: true, canBeDirectlyExchanged: false);

        // Auctioneer registers 3 missing stickers
        await RegisterMissingStickerAsync(clientAuctioneer, auctioneer.Id, catalogStickers[1].Id);
        await RegisterMissingStickerAsync(clientAuctioneer, auctioneer.Id, catalogStickers[2].Id);
        await RegisterMissingStickerAsync(clientAuctioneer, auctioneer.Id, catalogStickers[3].Id);

        var auction = await CreateAuctionAsync(clientAuctioneer, auctionStickerUs.Id);

        // Bidder A: only 1 sticker, covers 1 missing
        var a1 = await PublishUserStickerAsync(clientA, bidderA.Id, catalogStickers[1].Id);
        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { a1.Id });

        var auctionAfterA = await GetAuctionAsync(auction.Id);
        Assert.Equal(offerA.Id, auctionAfterA.BestCurrentOfferId);

        // Bidder B: 3 stickers, covers all 3 missing — clearly superior
        var b1 = await PublishUserStickerAsync(clientB, bidderB.Id, catalogStickers[1].Id);
        var b2 = await PublishUserStickerAsync(clientB, bidderB.Id, catalogStickers[2].Id);
        var b3 = await PublishUserStickerAsync(clientB, bidderB.Id, catalogStickers[3].Id);
        var offerB = await PlaceOfferAsync(clientB, auction.Id, new List<int> { b1.Id, b2.Id, b3.Id });

        // BestCurrentOfferId must switch to B's offer
        var auctionAfterB = await GetAuctionAsync(auction.Id);
        Assert.NotNull(auctionAfterB.BestCurrentOfferId);
        Assert.Equal(offerB.Id, auctionAfterB.BestCurrentOfferId);
        Assert.NotEqual(offerA.Id, auctionAfterB.BestCurrentOfferId);
    }

    /// <summary>
    /// When a new offer is strictly worse than the current best, BestCurrentOfferId must NOT change.
    /// </summary>
    [Fact]
    public async Task BestOffer_WhenWorseOfferArrives_BestCurrentOfferIdRemainsUnchanged()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();

        var auctioneer = await RegisterUserAsync($"bof_stay_auc_{suffix}", "Password123");
        var bidderA = await RegisterUserAsync($"bof_stay_a_{suffix}", "Password123");
        var bidderB = await RegisterUserAsync($"bof_stay_b_{suffix}", "Password123");
        var tokenAuctioneer = await LoginAsync($"bof_stay_auc_{suffix}", "Password123");
        var tokenA = await LoginAsync($"bof_stay_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"bof_stay_b_{suffix}", "Password123");
        var clientAuctioneer = ClientWithToken(tokenAuctioneer);
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 20);
        Assert.True(catalogStickers.Count >= 6,
            $"Need at least 6 catalog stickers for stay test, got {catalogStickers.Count}.");

        var auctionStickerUs = await PublishUserStickerAsync(
            clientAuctioneer, auctioneer.Id, catalogStickers[0].Id, quantity: 2, canBeAuctioned: true, canBeDirectlyExchanged: false);

        // Auctioneer registers 3 missing stickers
        await RegisterMissingStickerAsync(clientAuctioneer, auctioneer.Id, catalogStickers[1].Id);
        await RegisterMissingStickerAsync(clientAuctioneer, auctioneer.Id, catalogStickers[2].Id);
        await RegisterMissingStickerAsync(clientAuctioneer, auctioneer.Id, catalogStickers[3].Id);

        var auction = await CreateAuctionAsync(clientAuctioneer, auctionStickerUs.Id);

        // Bidder A: 3 stickers, covers all 3 missing — strongest possible offer
        var a1 = await PublishUserStickerAsync(clientA, bidderA.Id, catalogStickers[1].Id);
        var a2 = await PublishUserStickerAsync(clientA, bidderA.Id, catalogStickers[2].Id);
        var a3 = await PublishUserStickerAsync(clientA, bidderA.Id, catalogStickers[3].Id);
        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { a1.Id, a2.Id, a3.Id });

        var auctionAfterA = await GetAuctionAsync(auction.Id);
        Assert.Equal(offerA.Id, auctionAfterA.BestCurrentOfferId);

        // Bidder B: only 1 sticker, covers only 1 missing — inferior offer
        var b1 = await PublishUserStickerAsync(clientB, bidderB.Id, catalogStickers[4].Id);
        var offerB = await PlaceOfferAsync(clientB, auction.Id, new List<int> { b1.Id });

        // BestCurrentOfferId must remain A's offer
        var auctionAfterB = await GetAuctionAsync(auction.Id);
        Assert.NotNull(auctionAfterB.BestCurrentOfferId);
        Assert.Equal(offerA.Id, auctionAfterB.BestCurrentOfferId);
        Assert.NotEqual(offerB.Id, auctionAfterB.BestCurrentOfferId);
    }

    // ─── IAsyncLifetime ──────────────────────────────────────────────────────

    public async Task InitializeAsync() => await _factory.CleanMutableCollectionsAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
