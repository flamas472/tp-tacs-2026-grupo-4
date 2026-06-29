using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Figuritas.Api.Repositories;
using Figuritas.Api.Services;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model;
using Figuritas.Shared.Model.Subastas;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Figuritas.Api.Tests;

/// <summary>
/// US07 integration tests covering:
/// - Place offer: valid data, auth, bidder validation, expired auction, minimum conditions, duplicate sticker IDs.
/// - Cancel offer: valid cancellation, leading-offer guard, authorization, idempotency, inventory math.
/// - Update offer (PATCH): append stickers, leading-offer guard, duplicate IDs, BestCurrentOfferId recalculation.
/// - Concurrency: double cancel, double PATCH, cancellation vs closure, PATCH vs closure.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class UserStory07Tests : IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public UserStory07Tests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<UserResponseDTO> RegisterUserAsync(string username, string password = "Password123")
    {
        var dto = new { Username = username, Password = password };
        var response = await _client.PostAsJsonAsync("/api/auth/register", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserResponseDTO>(JsonOpts))!;
    }

    private async Task<string> LoginAsync(string username, string password = "Password123")
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

    // Alias used by tests migrated from SprintD.
    private HttpClient AuthClient(string token) => ClientWithToken(token);

    private async Task<List<Sticker>> GetCatalogStickersAsync(int page, int pageSize)
    {
        var response = await _client.GetAsync($"/api/stickers?Page={page}&PageSize={pageSize}");
        response.EnsureSuccessStatusCode();
        var stickers = await response.Content.ReadFromJsonAsync<List<Sticker>>(JsonOpts);
        Assert.NotNull(stickers);
        Assert.NotEmpty(stickers);
        return stickers!;
    }

    // Convenience wrapper used by SprintD-origin tests.
    private Task<List<Sticker>> GetCatalogAsync(int count = 5) => GetCatalogStickersAsync(1, count);

    private async Task<UserStickerResponseDTO> PublishUserStickerAsync(
        HttpClient authenticatedClient,
        int userId,
        int catalogStickerId,
        int quantity = 2,
        bool canBeAuctioned = true,
        bool canBeDirectlyExchanged = false)
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
        return (await response.Content.ReadFromJsonAsync<UserStickerResponseDTO>(JsonOpts))!;
    }

    // Alias with canBeDirectlyExchanged defaulting to true, matching SprintD defaults.
    private Task<UserStickerResponseDTO> PublishStickerAsync(
        HttpClient client, int userId, int catalogStickerId,
        int quantity = 2, bool canBeAuctioned = false, bool canBeDirectlyExchanged = true)
        => PublishUserStickerAsync(client, userId, catalogStickerId, quantity, canBeAuctioned, canBeDirectlyExchanged);

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
            EndsAt = endsAt ?? DateTime.UtcNow.AddHours(1)
        };
        var response = await authenticatedClient.PostAsJsonAsync("/api/auctions", dto);
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

    // ─── US07 Tests: Place Offer ─────────────────────────────────────────────

    /// <summary>
    /// Test 1: POST de puja válida → HTTP 201 y BestCurrentOfferId apunta al ID de la oferta creada.
    /// </summary>
    [Fact]
    public async Task CreateOffer_WithValidData_Returns201CreatedAndUpdatesBestCurrentOfferId()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us07_valid_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us07_valid_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us07_valid_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us07_valid_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerB = await PublishUserStickerAsync(clientB, userB.Id,
            catalogStickers[1 % catalogStickers.Count].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientA, stickerA.Id);
        Assert.Null(auction.BestCurrentOfferId);

        var offerDto = new PostAuctionOfferRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerB.Id }
        };

        var response = await clientB.PostAsJsonAsync($"/api/auctions/{auction.Id}/offers", offerDto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdOffer = await response.Content.ReadFromJsonAsync<AuctionOfferResponseDTO>(JsonOpts);
        Assert.NotNull(createdOffer);
        Assert.Equal(userB.Id, createdOffer!.BidderId);
        Assert.Equal(auction.Id, createdOffer.AuctionId);

        var updatedAuction = await GetAuctionAsync(auction.Id);
        Assert.NotNull(updatedAuction.BestCurrentOfferId);
        Assert.Equal(createdOffer.Id, updatedAuction.BestCurrentOfferId);
    }

    /// <summary>
    /// Test 2: El subastador intenta pujar en su propia subasta → HTTP 400.
    /// </summary>
    [Fact]
    public async Task CreateOffer_AuctioneerAttemptingToBid_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us07_self_a_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us07_self_a_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);

        var catalogStickers = await GetCatalogStickersAsync(1, 1);
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id, quantity: 3);
        var auction = await CreateAuctionAsync(clientA, stickerA.Id);

        var offerDto = new PostAuctionOfferRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerA.Id }
        };

        var response = await clientA.PostAsJsonAsync($"/api/auctions/{auction.Id}/offers", offerDto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Test 3: Lista de IDs ofrecidos vacía → HTTP 400.
    /// </summary>
    [Fact]
    public async Task CreateOffer_WithEmptyStickerList_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us07_empty_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us07_empty_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us07_empty_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us07_empty_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 1);
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var auction = await CreateAuctionAsync(clientA, stickerA.Id);

        var offerDto = new PostAuctionOfferRequestDTO
        {
            OfferedUserStickerIds = new List<int>()
        };

        var response = await clientB.PostAsJsonAsync($"/api/auctions/{auction.Id}/offers", offerDto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Test 4: Inyección de un ID de sticker inexistente → HTTP 400.
    /// </summary>
    [Fact]
    public async Task CreateOffer_WithGhostStickerIds_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us07_ghost_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us07_ghost_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us07_ghost_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us07_ghost_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 1);
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var auction = await CreateAuctionAsync(clientA, stickerA.Id);

        var offerDto = new PostAuctionOfferRequestDTO
        {
            OfferedUserStickerIds = new List<int> { 999999999 }
        };

        var response = await clientB.PostAsJsonAsync($"/api/auctions/{auction.Id}/offers", offerDto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Test 5: Puja en subasta con EndsAt ya vencido → HTTP 400.
    /// </summary>
    [Fact]
    public async Task CreateOffer_OnExpiredAuction_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us07_exp_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us07_exp_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us07_exp_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us07_exp_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerB = await PublishUserStickerAsync(clientB, userB.Id,
            catalogStickers[1 % catalogStickers.Count].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientA, stickerA.Id,
            endsAt: DateTime.UtcNow.AddSeconds(2));

        await Task.Delay(3000);

        var offerDto = new PostAuctionOfferRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerB.Id }
        };

        var response = await clientB.PostAsJsonAsync($"/api/auctions/{auction.Id}/offers", offerDto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Test 6: Ofertar un sticker que pertenece a otro usuario → HTTP 400.
    /// </summary>
    [Fact]
    public async Task CreateOffer_StickerNotOwnedByBidder_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us07_own_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us07_own_b_{suffix}", "Password123");
        var userC = await RegisterUserAsync($"us07_own_c_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us07_own_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us07_own_b_{suffix}", "Password123");
        var tokenC = await LoginAsync($"us07_own_c_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);
        var clientC = ClientWithToken(tokenC);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var auction = await CreateAuctionAsync(clientA, stickerA.Id);

        var stickerC = await PublishUserStickerAsync(clientC, userC.Id,
            catalogStickers[1 % catalogStickers.Count].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);

        var offerDto = new PostAuctionOfferRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerC.Id }
        };

        var response = await clientB.PostAsJsonAsync($"/api/auctions/{auction.Id}/offers", offerDto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Test 7: Oferta no cumple los requisitos mínimos del subastador → HTTP 400.
    /// </summary>
    [Fact]
    public async Task CreateOffer_BelowMinimumCondition_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us07_min_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us07_min_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us07_min_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us07_min_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 3);
        Assert.True(catalogStickers.Count >= 2, "Need at least 2 catalog stickers for minimum offer test.");

        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerB = await PublishUserStickerAsync(clientB, userB.Id,
            catalogStickers[1 % catalogStickers.Count].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);

        var requiredCatalogStickerId = catalogStickers[catalogStickers.Count - 1].Id;
        if (requiredCatalogStickerId == catalogStickers[1 % catalogStickers.Count].Id && catalogStickers.Count >= 3)
            requiredCatalogStickerId = catalogStickers[2].Id;

        var auction = await CreateAuctionAsync(clientA, stickerA.Id,
            minimumOfferStickerIds: new List<int> { requiredCatalogStickerId });

        var offerDto = new PostAuctionOfferRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerB.Id }
        };

        var response = await clientB.PostAsJsonAsync($"/api/auctions/{auction.Id}/offers", offerDto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Test 8: POST sin cabecera Authorization → HTTP 401.
    /// </summary>
    [Fact]
    public async Task CreateOffer_WithoutJwtToken_Returns401Unauthorized()
    {
        var anonymousClient = _factory.CreateClient();

        var offerDto = new PostAuctionOfferRequestDTO
        {
            OfferedUserStickerIds = new List<int> { 1 }
        };

        var response = await anonymousClient.PostAsJsonAsync("/api/auctions/1/offers", offerDto);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Test 9: Regresión completa US01–US06 — verifica que los refactors de tipos no rompieron el pipeline CI.
    /// </summary>
    [Fact]
    public async Task Verify_NoRegressions_OnUserStories01To06()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();

        var user = await RegisterUserAsync($"us07_regr_{suffix}", "Password123");
        var token = await LoginAsync($"us07_regr_{suffix}", "Password123");
        var client = ClientWithToken(token);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var userSticker = await PublishUserStickerAsync(client, user.Id, catalogStickers[0].Id, quantity: 3);
        Assert.True(userSticker.Id > 0, "US01: UserSticker should be published with a valid ID.");
        Assert.Equal(3, userSticker.Quantity);

        var missingDto = new { StickerId = catalogStickers[catalogStickers.Count - 1].Id };
        var missingResponse = await client.PostAsJsonAsync($"/api/users/{user.Id}/missing-stickers", missingDto);
        Assert.Equal(HttpStatusCode.Created, missingResponse.StatusCode);

        var searchResponse = await _client.GetAsync("/api/stickers?Page=1&PageSize=5");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var auction = await CreateAuctionAsync(client, userSticker.Id);
        Assert.Equal("Active", auction.Status);
        Assert.Null(auction.BestCurrentOfferId);

        var fetchedAuction = await GetAuctionAsync(auction.Id);
        Assert.Equal(auction.Id, fetchedAuction.Id);
        Assert.Equal(auction.AuctioneerId, fetchedAuction.AuctioneerId);
        Assert.Null(fetchedAuction.BestCurrentOfferId);

        var userB = await RegisterUserAsync($"us07_regr_b_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us07_regr_b_{suffix}", "Password123");
        var clientB = ClientWithToken(tokenB);

        var stickerB = await PublishUserStickerAsync(clientB, userB.Id,
            catalogStickers[1 % catalogStickers.Count].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);

        var offerDto = new PostAuctionOfferRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerB.Id }
        };
        var offerResponse = await clientB.PostAsJsonAsync($"/api/auctions/{auction.Id}/offers", offerDto);
        Assert.Equal(HttpStatusCode.Created, offerResponse.StatusCode);

        var createdOffer = await offerResponse.Content.ReadFromJsonAsync<AuctionOfferResponseDTO>(JsonOpts);
        Assert.NotNull(createdOffer);
        Assert.Equal(auction.Id, createdOffer!.AuctionId);
        Assert.True(createdOffer.Id > 0);

        var finalAuction = await GetAuctionAsync(auction.Id);
        Assert.NotNull(finalAuction.BestCurrentOfferId);
        Assert.Equal(createdOffer.Id, finalAuction.BestCurrentOfferId);
    }

    /// <summary>
    /// Test 10: Puja con UserSticker del bidder con Quantity == 0 → HTTP 400.
    /// </summary>
    [Fact]
    public async Task CreateOffer_BidderStickerWithZeroQuantity_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us07_qty0_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us07_qty0_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us07_qty0_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us07_qty0_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);

        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var auction = await CreateAuctionAsync(clientA, stickerA.Id);

        var stickerB = await PublishUserStickerAsync(clientB, userB.Id,
            catalogStickers[1 % catalogStickers.Count].Id,
            quantity: 1, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var patchDto = new { Quantity = 0 };
        var patchResponse = await clientB.PatchAsJsonAsync($"/api/users/{userB.Id}/stickers/{stickerB.Id}", patchDto);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var offerDto = new PostAuctionOfferRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerB.Id }
        };

        var response = await clientB.PostAsJsonAsync($"/api/auctions/{auction.Id}/offers", offerDto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// SA-04: Creating an auction offer with duplicate sticker IDs returns 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task CreateOffer_WithDuplicateStickerIds_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sa04_a_{suffix}");
        var userB = await RegisterUserAsync($"sa04_b_{suffix}");
        var tokenA = await LoginAsync($"sa04_a_{suffix}");
        var tokenB = await LoginAsync($"sa04_b_{suffix}");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerB = await PublishUserStickerAsync(clientB, userB.Id,
            catalogStickers[1 % catalogStickers.Count].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientA, stickerA.Id);

        var offerDto = new PostAuctionOfferRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerB.Id, stickerB.Id }
        };

        var response = await clientB.PostAsJsonAsync($"/api/auctions/{auction.Id}/offers", offerDto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// SA-05: Creating an auction offer with duplicate IDs among three entries returns 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task CreateOffer_WithThreeDuplicateStickerIds_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sa05_a_{suffix}");
        var userB = await RegisterUserAsync($"sa05_b_{suffix}");
        var tokenA = await LoginAsync($"sa05_a_{suffix}");
        var tokenB = await LoginAsync($"sa05_b_{suffix}");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerB = await PublishUserStickerAsync(clientB, userB.Id,
            catalogStickers[1 % catalogStickers.Count].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientA, stickerA.Id);

        var offerDto = new PostAuctionOfferRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerB.Id, stickerB.Id, stickerB.Id }
        };

        var response = await clientB.PostAsJsonAsync($"/api/auctions/{auction.Id}/offers", offerDto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── US07 Tests: Cancel Offer ────────────────────────────────────────────

    /// <summary>
    /// Scenario 1: Bidder A cancels their own non-leading offer.
    /// Expected: offer transitions to Cancelled, A's reserved stock is restored.
    /// </summary>
    [Fact]
    public async Task CancelOffer_ValidCancellation_OfferCancelledAndStockRestored()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd1_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd1_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd1_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd1_auc_{suffix}");
        var tokenA = await LoginAsync($"sd1_bida_{suffix}");
        var tokenB = await LoginAsync($"sd1_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(4);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);
        var stickerA = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA.Id });
        await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        var cancelResponse = await clientA.DeleteAsync(
            $"/api/auctions/{auction.Id}/offers/{offerA.Id}");
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var cancelledOffer = (await cancelResponse.Content.ReadFromJsonAsync<AuctionOfferResponseDTO>(JsonOpts))!;
        Assert.Equal("Cancelled", cancelledOffer.State);

        var restoredSticker = await GetUserStickerAsync(clientA, bidderA.Id, stickerA.Id);
        Assert.NotNull(restoredSticker);
        Assert.Equal(2, restoredSticker!.Quantity);
        Assert.True(restoredSticker.Active);
    }

    /// <summary>
    /// Scenario 2: Bidder tries to cancel their offer which is BestCurrentOfferId → 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task CancelOffer_LeadingOffer_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd2_auc_{suffix}");
        var bidder = await RegisterUserAsync($"sd2_bid_{suffix}");

        var tokenAuc = await LoginAsync($"sd2_auc_{suffix}");
        var tokenBid = await LoginAsync($"sd2_bid_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientBid = AuthClient(tokenBid);

        var catalog = await GetCatalogAsync(2);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);
        var bidSticker = await PublishStickerAsync(clientBid, bidder.Id, catalog[1].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);
        var offer = await PlaceOfferAsync(clientBid, auction.Id, new List<int> { bidSticker.Id });

        var currentAuction = await GetAuctionAsync(auction.Id);
        Assert.Equal(offer.Id, currentAuction.BestCurrentOfferId);

        var cancelResponse = await clientBid.DeleteAsync(
            $"/api/auctions/{auction.Id}/offers/{offer.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, cancelResponse.StatusCode);
    }

    /// <summary>
    /// Scenario 3: A stranger (not the bidder) tries to cancel someone else's offer → 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task CancelOffer_NonOwnerCaller_Returns403Forbidden()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd3_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd3_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd3_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd3_auc_{suffix}");
        var tokenA = await LoginAsync($"sd3_bida_{suffix}");
        var tokenB = await LoginAsync($"sd3_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(4);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);
        var stickerA = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA.Id });
        await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        var cancelResponse = await clientB.DeleteAsync(
            $"/api/auctions/{auction.Id}/offers/{offerA.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, cancelResponse.StatusCode);
    }

    /// <summary>
    /// Scenario 4: Bidder attempts to cancel an offer that was already cancelled → 400.
    /// </summary>
    [Fact]
    public async Task CancelOffer_AlreadyCancelledOffer_ReturnsControlledError()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd4_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd4_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd4_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd4_auc_{suffix}");
        var tokenA = await LoginAsync($"sd4_bida_{suffix}");
        var tokenB = await LoginAsync($"sd4_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(4);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);
        var stickerA = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA.Id });
        await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        var firstCancel = await clientA.DeleteAsync($"/api/auctions/{auction.Id}/offers/{offerA.Id}");
        Assert.Equal(HttpStatusCode.OK, firstCancel.StatusCode);

        var secondCancel = await clientA.DeleteAsync($"/api/auctions/{auction.Id}/offers/{offerA.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, secondCancel.StatusCode);
    }

    /// <summary>
    /// Scenario 5: Precise inventory math after cancellation — quantity restored exactly.
    /// </summary>
    [Fact]
    public async Task CancelOffer_InventoryMath_QuantityRestoredExactly()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd5_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd5_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd5_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd5_auc_{suffix}");
        var tokenA = await LoginAsync($"sd5_bida_{suffix}");
        var tokenB = await LoginAsync($"sd5_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(4);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);
        var stickerA = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id,
            quantity: 3, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA.Id });
        var afterOffer = await GetUserStickerAsync(clientA, bidderA.Id, stickerA.Id);
        Assert.Equal(2, afterOffer!.Quantity);

        await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        var cancelResponse = await clientA.DeleteAsync($"/api/auctions/{auction.Id}/offers/{offerA.Id}");
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var restoredSticker = await GetUserStickerAsync(clientA, bidderA.Id, stickerA.Id);
        Assert.NotNull(restoredSticker);
        Assert.Equal(3, restoredSticker!.Quantity);
        Assert.True(restoredSticker.Active);
    }

    /// <summary>
    /// Scenario 6: BestCurrentOfferId remains correct after cancelling a non-leading offer.
    /// </summary>
    [Fact]
    public async Task CancelOffer_BestCurrentOfferIdRecalculatedCorrectly()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd6_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd6_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd6_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd6_auc_{suffix}");
        var tokenA = await LoginAsync($"sd6_bida_{suffix}");
        var tokenB = await LoginAsync($"sd6_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(4);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);
        var stickerA = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA.Id });
        var offerB = await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        var beforeCancel = await GetAuctionAsync(auction.Id);
        Assert.Equal(offerB.Id, beforeCancel.BestCurrentOfferId);

        var cancelResponse = await clientA.DeleteAsync($"/api/auctions/{auction.Id}/offers/{offerA.Id}");
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var afterCancel = await GetAuctionAsync(auction.Id);
        Assert.Equal(offerB.Id, afterCancel.BestCurrentOfferId);
    }

    // ─── US07 Tests: Update Offer (PATCH) ───────────────────────────────────

    /// <summary>
    /// Scenario 7: Bidder appends additional stickers to their non-leading offer.
    /// Expected: new stickers added, stock reserved.
    /// </summary>
    [Fact]
    public async Task UpdateOffer_ValidPatch_StickersAppendedAndStockReserved()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd7_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd7_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd7_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd7_auc_{suffix}");
        var tokenA = await LoginAsync($"sd7_bida_{suffix}");
        var tokenB = await LoginAsync($"sd7_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(5);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);
        var stickerA1 = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerA2 = await PublishStickerAsync(clientA, bidderA.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[4].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA1.Id });
        await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        var beforePatch = await GetUserStickerAsync(clientA, bidderA.Id, stickerA2.Id);
        Assert.Equal(2, beforePatch!.Quantity);

        var patchDto = new UpdateAuctionOfferRequestDTO { AdditionalStickerIds = new List<int> { stickerA2.Id } };
        var patchResponse = await clientA.PatchAsJsonAsync(
            $"/api/auctions/{auction.Id}/offers/{offerA.Id}", patchDto);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var updatedOffer = (await patchResponse.Content.ReadFromJsonAsync<AuctionOfferResponseDTO>(JsonOpts))!;
        Assert.Contains(stickerA1.Id, updatedOffer.OfferedUserStickerIds);
        Assert.Contains(stickerA2.Id, updatedOffer.OfferedUserStickerIds);
        Assert.Equal(2, updatedOffer.OfferedUserStickerIds.Count);

        var afterPatch = await GetUserStickerAsync(clientA, bidderA.Id, stickerA2.Id);
        Assert.Equal(1, afterPatch!.Quantity);
    }

    /// <summary>
    /// Scenario 8: Bidder tries to PATCH their offer which is BestCurrentOfferId → 400.
    /// </summary>
    [Fact]
    public async Task UpdateOffer_LeadingOffer_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd8_auc_{suffix}");
        var bidder = await RegisterUserAsync($"sd8_bid_{suffix}");

        var tokenAuc = await LoginAsync($"sd8_auc_{suffix}");
        var tokenBid = await LoginAsync($"sd8_bid_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientBid = AuthClient(tokenBid);

        var catalog = await GetCatalogAsync(3);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);
        var bidSticker1 = await PublishStickerAsync(clientBid, bidder.Id, catalog[1].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var bidSticker2 = await PublishStickerAsync(clientBid, bidder.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);
        var offer = await PlaceOfferAsync(clientBid, auction.Id, new List<int> { bidSticker1.Id });

        var currentAuction = await GetAuctionAsync(auction.Id);
        Assert.Equal(offer.Id, currentAuction.BestCurrentOfferId);

        var patchDto = new UpdateAuctionOfferRequestDTO { AdditionalStickerIds = new List<int> { bidSticker2.Id } };
        var patchResponse = await clientBid.PatchAsJsonAsync(
            $"/api/auctions/{auction.Id}/offers/{offer.Id}", patchDto);
        Assert.Equal(HttpStatusCode.BadRequest, patchResponse.StatusCode);
    }

    /// <summary>
    /// Scenario 9: PATCH with duplicate IDs in AdditionalStickerIds → 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task UpdateOffer_DuplicateAdditionalStickerIds_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd9_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd9_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd9_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd9_auc_{suffix}");
        var tokenA = await LoginAsync($"sd9_bida_{suffix}");
        var tokenB = await LoginAsync($"sd9_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(5);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);
        var stickerA1 = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id,
            quantity: 3, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerA2 = await PublishStickerAsync(clientA, bidderA.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[4].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA1.Id });
        await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        var patchDto = new UpdateAuctionOfferRequestDTO
        {
            AdditionalStickerIds = new List<int> { stickerA2.Id, stickerA2.Id }
        };
        var patchResponse = await clientA.PatchAsJsonAsync(
            $"/api/auctions/{auction.Id}/offers/{offerA.Id}", patchDto);
        Assert.Equal(HttpStatusCode.BadRequest, patchResponse.StatusCode);
    }

    /// <summary>
    /// Scenario 10: After a successful PATCH adding stickers, BestCurrentOfferId is recalculated.
    /// A has 1 sticker, B has 2 (B is leader). After A adds 2 more (total 3), A becomes the new leader.
    /// </summary>
    [Fact]
    public async Task UpdateOffer_BestCurrentOfferIdRecalculatedAfterPatch()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd10_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd10_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd10_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd10_auc_{suffix}");
        var tokenA = await LoginAsync($"sd10_bida_{suffix}");
        var tokenB = await LoginAsync($"sd10_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(6);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);
        var stickerA1 = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerA2 = await PublishStickerAsync(clientA, bidderA.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerA3 = await PublishStickerAsync(clientA, bidderA.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[4].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[5].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA1.Id });
        var offerB = await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        var beforePatch = await GetAuctionAsync(auction.Id);
        Assert.Equal(offerB.Id, beforePatch.BestCurrentOfferId);

        var patchDto = new UpdateAuctionOfferRequestDTO
        {
            AdditionalStickerIds = new List<int> { stickerA2.Id, stickerA3.Id }
        };
        var patchResponse = await clientA.PatchAsJsonAsync(
            $"/api/auctions/{auction.Id}/offers/{offerA.Id}", patchDto);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var afterPatch = await GetAuctionAsync(auction.Id);
        Assert.Equal(offerA.Id, afterPatch.BestCurrentOfferId);
    }

    // ─── US07 Tests: Concurrency ─────────────────────────────────────────────

    /// <summary>
    /// Scenario 11: Two simultaneous DELETE requests for the same offer.
    /// Exactly one succeeds (200), the other gets 400. Stock is released exactly once.
    /// </summary>
    [Fact]
    public async Task Concurrency_DoubleCancellation_OnlyOneSucceedsStockReleasedOnce()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd11_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd11_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd11_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd11_auc_{suffix}");
        var tokenA = await LoginAsync($"sd11_bida_{suffix}");
        var tokenB = await LoginAsync($"sd11_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA1 = AuthClient(tokenA);
        var clientA2 = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(4);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);
        var stickerA = await PublishStickerAsync(clientA1, bidderA.Id, catalog[1].Id,
            quantity: 1, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        var offerA = await PlaceOfferAsync(clientA1, auction.Id, new List<int> { stickerA.Id });
        await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        var task1 = clientA1.DeleteAsync($"/api/auctions/{auction.Id}/offers/{offerA.Id}");
        var task2 = clientA2.DeleteAsync($"/api/auctions/{auction.Id}/offers/{offerA.Id}");
        await Task.WhenAll(task1, task2);
        var r1 = await task1;
        var r2 = await task2;

        var successCount = new[] { r1, r2 }.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(1, successCount);

        var finalSticker = await GetUserStickerAsync(clientA1, bidderA.Id, stickerA.Id);
        Assert.NotNull(finalSticker);
        Assert.Equal(1, finalSticker!.Quantity);
    }

    /// <summary>
    /// Scenario 12: Two simultaneous PATCH requests each adding a different sticker.
    /// Both arrays are consolidated (3 stickers total), no data loss, both stickers reserved.
    /// </summary>
    [Fact]
    public async Task Concurrency_DoublePatch_BothArraysConsolidatedNoDataLoss()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd12_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd12_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd12_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd12_auc_{suffix}");
        var tokenA = await LoginAsync($"sd12_bida_{suffix}");
        var tokenB = await LoginAsync($"sd12_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA1 = AuthClient(tokenA);
        var clientA2 = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(6);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);
        var stickerA1 = await PublishStickerAsync(clientA1, bidderA.Id, catalog[1].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerA2 = await PublishStickerAsync(clientA1, bidderA.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerA3 = await PublishStickerAsync(clientA1, bidderA.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[4].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[5].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        var offerA = await PlaceOfferAsync(clientA1, auction.Id, new List<int> { stickerA1.Id });
        await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        var patchDto2 = new UpdateAuctionOfferRequestDTO { AdditionalStickerIds = new List<int> { stickerA2.Id } };
        var patchDto3 = new UpdateAuctionOfferRequestDTO { AdditionalStickerIds = new List<int> { stickerA3.Id } };

        var patchTask1 = clientA1.PatchAsJsonAsync($"/api/auctions/{auction.Id}/offers/{offerA.Id}", patchDto2);
        var patchTask2 = clientA2.PatchAsJsonAsync($"/api/auctions/{auction.Id}/offers/{offerA.Id}", patchDto3);
        await Task.WhenAll(patchTask1, patchTask2);
        var pr1 = await patchTask1;
        var pr2 = await patchTask2;

        Assert.Equal(HttpStatusCode.OK, pr1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, pr2.StatusCode);

        var finalA2 = await GetUserStickerAsync(clientA1, bidderA.Id, stickerA2.Id);
        var finalA3 = await GetUserStickerAsync(clientA1, bidderA.Id, stickerA3.Id);
        Assert.Equal(1, finalA2!.Quantity);
        Assert.Equal(1, finalA3!.Quantity);

        var allOffers = await GetAuctionOffersAsync(auction.Id);
        var finalOffer = allOffers.FirstOrDefault(o => o.Id == offerA.Id);
        Assert.NotNull(finalOffer);
        Assert.Contains(stickerA2.Id, finalOffer!.OfferedUserStickerIds);
        Assert.Contains(stickerA3.Id, finalOffer.OfferedUserStickerIds);
        Assert.Equal(3, finalOffer.OfferedUserStickerIds.Count);
    }

    /// <summary>
    /// Scenario 13: Cancellation races against auction closure.
    /// Stock must be released at most once for A's offer — atomic guards prevent double release.
    /// </summary>
    [Fact]
    public async Task Concurrency_CancellationVsAuctionClosure_NoDoubleStockRelease()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd13_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd13_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd13_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd13_auc_{suffix}");
        var tokenA = await LoginAsync($"sd13_bida_{suffix}");
        var tokenB = await LoginAsync($"sd13_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(4);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 1, canBeAuctioned: true);
        var stickerA = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id,
            quantity: 1, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id, endsAt: DateTime.UtcNow.AddSeconds(3));

        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA.Id });
        await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        await Task.Delay(4000);

        using var scope = _factory.Services.CreateScope();
        var auctionService = scope.ServiceProvider.GetRequiredService<AuctionService>();
        var auctionRepo = scope.ServiceProvider.GetRequiredService<IAuctionRepository>();

        var cancelTask = Task.Run(() =>
            clientA.DeleteAsync($"/api/auctions/{auction.Id}/offers/{offerA.Id}"));

        var workerTask = Task.Run(async () =>
        {
            try
            {
                var claimed = await auctionRepo.TryClaimAutomaticClosureAsync(auction.Id);
                if (claimed)
                    await auctionService.CloseAuctionAutomatically(auction.Id);
                return true;
            }
            catch { return false; }
        });

        await Task.WhenAll(cancelTask, workerTask);

        var finalStickerA = await GetUserStickerAsync(clientA, bidderA.Id, stickerA.Id);
        if (finalStickerA != null)
        {
            Assert.True(finalStickerA.Quantity <= 1,
                $"Bidder A stock was double-released. Expected ≤1 but got {finalStickerA.Quantity}.");
        }

        var allOffers = await GetAuctionOffersAsync(auction.Id);
        var offerAFinal = allOffers.FirstOrDefault(o => o.Id == offerA.Id);
        if (offerAFinal != null)
        {
            Assert.True(
                offerAFinal.State == "Cancelled" || offerAFinal.State == "Lost",
                $"Offer A should be Cancelled or Lost but got: {offerAFinal.State}");
        }
    }

    /// <summary>
    /// Scenario 14: PATCH races against auction closure.
    /// Final inventory must be coherent — no sticker reserved without commitment, no double-reservation.
    /// </summary>
    [Fact]
    public async Task Concurrency_PatchVsAuctionClosure_FinalInventoryCoherent()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd14_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd14_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd14_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd14_auc_{suffix}");
        var tokenA = await LoginAsync($"sd14_bida_{suffix}");
        var tokenB = await LoginAsync($"sd14_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(5);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 1, canBeAuctioned: true);
        var stickerA1 = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerA2 = await PublishStickerAsync(clientA, bidderA.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[4].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id, endsAt: DateTime.UtcNow.AddSeconds(3));

        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA1.Id });
        await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        var stickerA2BeforeRace = await GetUserStickerAsync(clientA, bidderA.Id, stickerA2.Id);
        var initialA2Qty = stickerA2BeforeRace!.Quantity;

        await Task.Delay(4000);

        using var scope = _factory.Services.CreateScope();
        var auctionService = scope.ServiceProvider.GetRequiredService<AuctionService>();
        var auctionRepo = scope.ServiceProvider.GetRequiredService<IAuctionRepository>();

        var patchDto = new UpdateAuctionOfferRequestDTO { AdditionalStickerIds = new List<int> { stickerA2.Id } };
        var patchTask = Task.Run(() =>
            clientA.PatchAsJsonAsync($"/api/auctions/{auction.Id}/offers/{offerA.Id}", patchDto));

        var workerTask = Task.Run(async () =>
        {
            try
            {
                var claimed = await auctionRepo.TryClaimAutomaticClosureAsync(auction.Id);
                if (claimed)
                    await auctionService.CloseAuctionAutomatically(auction.Id);
                return true;
            }
            catch { return false; }
        });

        await Task.WhenAll(patchTask, workerTask);

        var finalAuction = await GetAuctionAsync(auction.Id);
        Assert.True(
            finalAuction.Status == "Closed" || finalAuction.Status == "Cancelled",
            $"Expected terminal status but got: {finalAuction.Status}");

        var finalA2 = await GetUserStickerAsync(clientA, bidderA.Id, stickerA2.Id);
        if (finalA2 != null)
        {
            Assert.True(finalA2.Quantity <= initialA2Qty,
                $"stickerA2 quantity exceeds initial. Expected ≤{initialA2Qty} but got {finalA2.Quantity}.");
        }
    }

    // ─── IAsyncLifetime ──────────────────────────────────────────────────────

    public async Task InitializeAsync() => await _factory.CleanMutableCollectionsAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
