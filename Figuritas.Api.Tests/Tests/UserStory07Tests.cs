using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Figuritas.Api.Tests;

[Collection(nameof(IntegrationTestCollection))]
public class UserStory07Tests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public UserStory07Tests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<UserResponseDTO> RegisterUserAsync(string username, string password)
    {
        var dto = new { Username = username, Password = password };
        var response = await _client.PostAsJsonAsync("/api/users", dto);
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
        return body.GetProperty("token").GetString()!;
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
            EndsAt = endsAt ?? DateTime.UtcNow.AddHours(1)
        };
        var response = await authenticatedClient.PostAsJsonAsync("/api/auctions", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuctionResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }))!;
    }

    private async Task<AuctionResponseDTO> GetAuctionAsync(int auctionId)
    {
        var response = await _client.GetAsync($"/api/auctions/{auctionId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuctionResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }))!;
    }

    // ─── US07 Tests ──────────────────────────────────────────────────────────

    /// <summary>
    /// Test 1: POST de puja válida → HTTP 201 y BestCurrentOfferId de la subasta apunta al ID de la oferta creada.
    /// </summary>
    [Fact]
    public async Task CreateOffer_WithValidData_Returns201CreatedAndUpdatesBestCurrentOfferId()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us07_valid_a_{suffix}", "password123");
        var userB = await RegisterUserAsync($"us07_valid_b_{suffix}", "password123");
        var tokenA = await LoginAsync($"us07_valid_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us07_valid_b_{suffix}", "password123");
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
        var createdOffer = await response.Content.ReadFromJsonAsync<AuctionOfferResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(createdOffer);
        Assert.Equal(userB.Id, createdOffer!.BidderId);
        Assert.Equal(auction.Id, createdOffer.AuctionId);

        // Verify BestCurrentOfferId is updated in the auction
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
        var userA = await RegisterUserAsync($"us07_self_a_{suffix}", "password123");
        var tokenA = await LoginAsync($"us07_self_a_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);

        var catalogStickers = await GetCatalogStickersAsync(1, 1);
        // Publish 2 units: one for the auction, another to offer
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
        var userA = await RegisterUserAsync($"us07_empty_a_{suffix}", "password123");
        var userB = await RegisterUserAsync($"us07_empty_b_{suffix}", "password123");
        var tokenA = await LoginAsync($"us07_empty_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us07_empty_b_{suffix}", "password123");
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
        var userA = await RegisterUserAsync($"us07_ghost_a_{suffix}", "password123");
        var userB = await RegisterUserAsync($"us07_ghost_b_{suffix}", "password123");
        var tokenA = await LoginAsync($"us07_ghost_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us07_ghost_b_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 1);
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var auction = await CreateAuctionAsync(clientA, stickerA.Id);

        var offerDto = new PostAuctionOfferRequestDTO
        {
            OfferedUserStickerIds = new List<int> { 999999999 } // Non-existent ID
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
        var userA = await RegisterUserAsync($"us07_exp_a_{suffix}", "password123");
        var userB = await RegisterUserAsync($"us07_exp_b_{suffix}", "password123");
        var tokenA = await LoginAsync($"us07_exp_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us07_exp_b_{suffix}", "password123");
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
        var userA = await RegisterUserAsync($"us07_own_a_{suffix}", "password123");
        var userB = await RegisterUserAsync($"us07_own_b_{suffix}", "password123");
        var userC = await RegisterUserAsync($"us07_own_c_{suffix}", "password123");
        var tokenA = await LoginAsync($"us07_own_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us07_own_b_{suffix}", "password123");
        var tokenC = await LoginAsync($"us07_own_c_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);
        var clientC = ClientWithToken(tokenC);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        // UserA creates the auction
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var auction = await CreateAuctionAsync(clientA, stickerA.Id);

        // UserC publishes a sticker — UserB will try to offer it (does not own it)
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
        var userA = await RegisterUserAsync($"us07_min_a_{suffix}", "password123");
        var userB = await RegisterUserAsync($"us07_min_b_{suffix}", "password123");
        var tokenA = await LoginAsync($"us07_min_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us07_min_b_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 3);
        Assert.True(catalogStickers.Count >= 2, "Need at least 2 catalog stickers for minimum offer test.");

        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerB = await PublishUserStickerAsync(clientB, userB.Id,
            catalogStickers[1 % catalogStickers.Count].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);

        // Required catalog sticker is different from what B offers
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

        // US01: publish a user sticker
        var user = await RegisterUserAsync($"us07_regr_{suffix}", "password123");
        var token = await LoginAsync($"us07_regr_{suffix}", "password123");
        var client = ClientWithToken(token);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var userSticker = await PublishUserStickerAsync(client, user.Id, catalogStickers[0].Id, quantity: 3);
        Assert.True(userSticker.Id > 0, "US01: UserSticker should be published with a valid ID.");
        Assert.Equal(3, userSticker.Quantity);

        // US02: register a missing sticker (single StickerId per request)
        var missingDto = new { StickerId = catalogStickers[catalogStickers.Count - 1].Id };
        var missingResponse = await client.PostAsJsonAsync($"/api/users/{user.Id}/missing-stickers", missingDto);
        Assert.Equal(HttpStatusCode.Created, missingResponse.StatusCode);

        // US03: search stickers by catalog (basic GET)
        var searchResponse = await _client.GetAsync("/api/stickers?Page=1&PageSize=5");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        // US06: create auction with valid data
        var auction = await CreateAuctionAsync(client, userSticker.Id);
        Assert.Equal("Active", auction.Status);
        Assert.Null(auction.BestCurrentOfferId);

        // US06 GET: fetch auction and verify int? BestCurrentOfferId serializes as null
        var fetchedAuction = await GetAuctionAsync(auction.Id);
        Assert.Equal(auction.Id, fetchedAuction.Id);
        Assert.Equal(auction.AuctioneerId, fetchedAuction.AuctioneerId);
        Assert.Null(fetchedAuction.BestCurrentOfferId);

        // US07 prerequisite: bidder creates offer and BestCurrentOfferId updates to int
        var userB = await RegisterUserAsync($"us07_regr_b_{suffix}", "password123");
        var tokenB = await LoginAsync($"us07_regr_b_{suffix}", "password123");
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

        var createdOffer = await offerResponse.Content.ReadFromJsonAsync<AuctionOfferResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(createdOffer);
        Assert.Equal(auction.Id, createdOffer!.AuctionId); // int FK, not string
        Assert.True(createdOffer.Id > 0);

        var finalAuction = await GetAuctionAsync(auction.Id);
        Assert.NotNull(finalAuction.BestCurrentOfferId); // int? updated
        Assert.Equal(createdOffer.Id, finalAuction.BestCurrentOfferId);
    }

    // ─── IAsyncLifetime ──────────────────────────────────────────────────────

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;
}
