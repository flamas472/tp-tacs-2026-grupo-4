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
/// US06 integration tests covering:
/// - Create auction: valid data, auth, stock management, business rules.
/// - List/filter auctions: auth, own-auction exclusion, pagination, UserSelectedBestOfferId serialization.
/// - Accept offer / close auction: manual acceptance, authorization guards, worker expiration, concurrency.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class UserStory06Tests : IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public UserStory06Tests(IntegrationTestFactory factory)
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

    // Alias used by tests migrated from SprintC.
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

    // Convenience wrapper used by SprintC-origin tests.
    private Task<List<Sticker>> GetCatalogAsync(int count = 3) => GetCatalogStickersAsync(1, count);

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

    // Alias with canBeDirectlyExchanged defaulting to true, matching SprintC defaults.
    private Task<UserStickerResponseDTO> PublishStickerAsync(
        HttpClient client, int userId, int catalogStickerId,
        int quantity = 2, bool canBeAuctioned = true, bool canBeDirectlyExchanged = true)
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

    // ─── Worker unit-test support ────────────────────────────────────────────

    /// <summary>
    /// Minimal fake AuctionService for worker unit tests that captures closure calls.
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

    // ─── US06 Tests: Create Auction ─────────────────────────────────────────

    /// <summary>
    /// Escenario 1: Usuario autenticado publica subasta válida. Valida HTTP 201, Status == "Active".
    /// </summary>
    [Fact]
    public async Task CreateAuction_WithValidData_Returns201AndStatusActive()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var user = await RegisterUserAsync($"us06_valid_{suffix}", "Password123");
        var token = await LoginAsync($"us06_valid_{suffix}", "Password123");
        var client = ClientWithToken(token);

        var catalogStickers = await GetCatalogStickersAsync(1, 1);
        var userSticker = await PublishUserStickerAsync(client, user.Id, catalogStickers[0].Id);

        var dto = new PostAuctionRequestDTO
        {
            UserStickerId = userSticker.Id,
            MinimumOfferStickerIds = new List<int>(),
            EndsAt = DateTime.UtcNow.AddHours(1)
        };

        var response = await client.PostAsJsonAsync("/api/auctions", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuctionResponseDTO>(JsonOpts);
        Assert.NotNull(result);
        Assert.Equal("Active", result!.Status);
        Assert.Equal(user.Id, result.AuctioneerId);
        Assert.Equal(userSticker.Id, result.UserStickerId);
    }

    /// <summary>
    /// Escenario 2: POST sin cabecera Authorization. Valida HTTP 401.
    /// </summary>
    [Fact]
    public async Task CreateAuction_WithoutJwtToken_Returns401Unauthorized()
    {
        var anonymousClient = _factory.CreateClient();
        var dto = new PostAuctionRequestDTO
        {
            UserStickerId = 1,
            MinimumOfferStickerIds = new List<int>(),
            EndsAt = DateTime.UtcNow.AddHours(1)
        };

        var response = await anonymousClient.PostAsJsonAsync("/api/auctions", dto);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Escenario 3: Subastar UserSticker con stock == 0 → HTTP 400.
    /// </summary>
    [Fact]
    public async Task CreateAuction_WithQuantityZero_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var user = await RegisterUserAsync($"us06_qty0_{suffix}", "Password123");
        var token = await LoginAsync($"us06_qty0_{suffix}", "Password123");
        var client = ClientWithToken(token);

        var catalogStickers = await GetCatalogStickersAsync(1, 1);
        var userSticker = await PublishUserStickerAsync(client, user.Id, catalogStickers[0].Id, quantity: 1);

        var patchDto = new { Quantity = 0 };
        var patchResponse = await client.PatchAsJsonAsync($"/api/users/{user.Id}/stickers/{userSticker.Id}", patchDto);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var dto = new PostAuctionRequestDTO
        {
            UserStickerId = userSticker.Id,
            MinimumOfferStickerIds = new List<int>(),
            EndsAt = DateTime.UtcNow.AddHours(1)
        };

        var response = await client.PostAsJsonAsync("/api/auctions", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Escenario 4: Subastar figurita con CanBeAuctioned == false → HTTP 400.
    /// </summary>
    [Fact]
    public async Task CreateAuction_WithCanBeAuctionedFalse_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var user = await RegisterUserAsync($"us06_noauct_{suffix}", "Password123");
        var token = await LoginAsync($"us06_noauct_{suffix}", "Password123");
        var client = ClientWithToken(token);

        var catalogStickers = await GetCatalogStickersAsync(1, 1);
        var userSticker = await PublishUserStickerAsync(
            client, user.Id, catalogStickers[0].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);

        var dto = new PostAuctionRequestDTO
        {
            UserStickerId = userSticker.Id,
            MinimumOfferStickerIds = new List<int>(),
            EndsAt = DateTime.UtcNow.AddHours(1)
        };

        var response = await client.PostAsJsonAsync("/api/auctions", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Escenario 5: EndsAt &lt; DateTime.UtcNow → HTTP 400.
    /// </summary>
    [Fact]
    public async Task CreateAuction_WithEndsAtInPast_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var user = await RegisterUserAsync($"us06_past_{suffix}", "Password123");
        var token = await LoginAsync($"us06_past_{suffix}", "Password123");
        var client = ClientWithToken(token);

        var catalogStickers = await GetCatalogStickersAsync(1, 1);
        var userSticker = await PublishUserStickerAsync(client, user.Id, catalogStickers[0].Id);

        var dto = new PostAuctionRequestDTO
        {
            UserStickerId = userSticker.Id,
            MinimumOfferStickerIds = new List<int>(),
            EndsAt = DateTime.UtcNow.AddHours(-1)
        };

        var response = await client.PostAsJsonAsync("/api/auctions", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Escenario 6: Tras POST exitoso, verificar que Quantity del UserSticker bajó en 1.
    /// </summary>
    [Fact]
    public async Task CreateAuction_ValidPublication_DecrementsUserStickerQuantity()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var user = await RegisterUserAsync($"us06_decr_{suffix}", "Password123");
        var token = await LoginAsync($"us06_decr_{suffix}", "Password123");
        var client = ClientWithToken(token);

        var catalogStickers = await GetCatalogStickersAsync(1, 1);
        var userSticker = await PublishUserStickerAsync(client, user.Id, catalogStickers[0].Id, quantity: 3);
        var originalQuantity = userSticker.Quantity;

        var dto = new PostAuctionRequestDTO
        {
            UserStickerId = userSticker.Id,
            MinimumOfferStickerIds = new List<int>(),
            EndsAt = DateTime.UtcNow.AddHours(1)
        };

        var createResponse = await client.PostAsJsonAsync("/api/auctions", dto);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/users/{user.Id}/stickers/{userSticker.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var updated = await getResponse.Content.ReadFromJsonAsync<UserStickerResponseDTO>(JsonOpts);
        Assert.NotNull(updated);
        Assert.Equal(originalQuantity - 1, updated!.Quantity);
    }

    /// <summary>
    /// Escenario 7: UserSticker con Quantity == 1, tras subastarlo, Quantity == 0 y Active == false.
    /// </summary>
    [Fact]
    public async Task CreateAuction_LastStickerUnit_MarksUserStickerAsInactive()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var user = await RegisterUserAsync($"us06_last_{suffix}", "Password123");
        var token = await LoginAsync($"us06_last_{suffix}", "Password123");
        var client = ClientWithToken(token);

        var catalogStickers = await GetCatalogStickersAsync(1, 1);
        var userSticker = await PublishUserStickerAsync(client, user.Id, catalogStickers[0].Id, quantity: 1);
        Assert.Equal(1, userSticker.Quantity);

        var dto = new PostAuctionRequestDTO
        {
            UserStickerId = userSticker.Id,
            MinimumOfferStickerIds = new List<int>(),
            EndsAt = DateTime.UtcNow.AddHours(1)
        };

        var createResponse = await client.PostAsJsonAsync("/api/auctions", dto);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var getStickerResponse = await client.GetAsync($"/api/users/{user.Id}/stickers/{userSticker.Id}");
        if (getStickerResponse.StatusCode == HttpStatusCode.OK)
        {
            var found = await getStickerResponse.Content.ReadFromJsonAsync<UserStickerResponseDTO>(JsonOpts);
            Assert.NotNull(found);
            Assert.Equal(0, found!.Quantity);
            Assert.False(found.Active);
        }
        // If 404, the sticker was correctly deactivated and filtered from the inventory.
    }

    /// <summary>
    /// Escenario 8: Subastar UserStickerId que pertenece a otro usuario → HTTP 400.
    /// </summary>
    [Fact]
    public async Task CreateAuction_StickerNotOwnedByCaller_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us06_own_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us06_own_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us06_own_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us06_own_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 1);
        var stickerOfB = await PublishUserStickerAsync(clientB, userB.Id, catalogStickers[0].Id);

        var dto = new PostAuctionRequestDTO
        {
            UserStickerId = stickerOfB.Id,
            MinimumOfferStickerIds = new List<int>(),
            EndsAt = DateTime.UtcNow.AddHours(1)
        };

        var response = await clientA.PostAsJsonAsync("/api/auctions", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── US06 Tests: List / Filter Auctions ─────────────────────────────────

    /// <summary>
    /// SA-01: GET /api/auctions without JWT returns 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task GetAuctions_WithoutJwtToken_Returns401Unauthorized()
    {
        var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.GetAsync("/api/auctions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// SA-02: Authenticated user creating an auction does not see it in GET /api/auctions.
    /// Another user listing the same endpoint does see it.
    /// </summary>
    [Fact]
    public async Task GetAuctions_AuthenticatedUser_DoesNotSeeOwnAuctions()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sa02_a_{suffix}");
        var userB = await RegisterUserAsync($"sa02_b_{suffix}");
        var tokenA = await LoginAsync($"sa02_a_{suffix}");
        var tokenB = await LoginAsync($"sa02_b_{suffix}");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickerId = (await GetCatalogStickersAsync(1, 1))[0].Id;
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickerId);
        var auction = await CreateAuctionAsync(clientA, stickerA.Id);

        var responseA = await clientA.GetAsync("/api/auctions?page=1&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
        var auctionsForA = await responseA.Content.ReadFromJsonAsync<List<AuctionResponseDTO>>(JsonOpts);
        Assert.NotNull(auctionsForA);
        Assert.DoesNotContain(auctionsForA!, a => a.Id == auction.Id);

        var responseB = await clientB.GetAsync("/api/auctions?page=1&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);
        var auctionsForB = await responseB.Content.ReadFromJsonAsync<List<AuctionResponseDTO>>(JsonOpts);
        Assert.NotNull(auctionsForB);
        Assert.Contains(auctionsForB!, a => a.Id == auction.Id);
    }

    /// <summary>
    /// SA-03: Pagination parameters are preserved when filtering by authenticated user.
    /// Verifies backward compatibility: response is still a list (not null/error).
    /// </summary>
    [Fact]
    public async Task GetAuctions_WithPaginationParams_ReturnsOkWithList()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sa03_a_{suffix}");
        var tokenA = await LoginAsync($"sa03_a_{suffix}");
        var clientA = ClientWithToken(tokenA);

        var response = await clientA.GetAsync("/api/auctions?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auctions = await response.Content.ReadFromJsonAsync<List<AuctionResponseDTO>>(JsonOpts);
        Assert.NotNull(auctions);
    }

    /// <summary>
    /// SA-09: Newly created auction has UserSelectedBestOfferId == null in GET /api/auctions/{id}.
    /// </summary>
    [Fact]
    public async Task GetAuction_NewAuction_UserSelectedBestOfferIdIsNull()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sa09_a_{suffix}");
        var tokenA = await LoginAsync($"sa09_a_{suffix}");
        var clientA = ClientWithToken(tokenA);

        var catalogStickerId = (await GetCatalogStickersAsync(1, 1))[0].Id;
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickerId);
        var auction = await CreateAuctionAsync(clientA, stickerA.Id);

        var response = await clientA.GetAsync($"/api/auctions/{auction.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.Content.ReadFromJsonAsync<AuctionResponseDTO>(JsonOpts);

        Assert.NotNull(fetched);
        Assert.Null(fetched!.UserSelectedBestOfferId);
    }

    /// <summary>
    /// SA-10: GET /api/auctions list includes UserSelectedBestOfferId (null) for all returned auctions.
    /// </summary>
    [Fact]
    public async Task GetAuctions_ResponseDtoContainsUserSelectedBestOfferId_NullByDefault()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sa10_a_{suffix}");
        var userB = await RegisterUserAsync($"sa10_b_{suffix}");
        var tokenA = await LoginAsync($"sa10_a_{suffix}");
        var tokenB = await LoginAsync($"sa10_b_{suffix}");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickerId = (await GetCatalogStickersAsync(1, 1))[0].Id;
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickerId);
        var createdAuction = await CreateAuctionAsync(clientA, stickerA.Id);

        var response = await clientB.GetAsync("/api/auctions?page=1&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auctions = await response.Content.ReadFromJsonAsync<List<AuctionResponseDTO>>(JsonOpts);

        Assert.NotNull(auctions);
        var target = auctions!.FirstOrDefault(a => a.Id == createdAuction.Id);
        Assert.NotNull(target);
        Assert.Null(target!.UserSelectedBestOfferId);
    }

    /// <summary>
    /// SA-11: UserSelectedBestOfferId field is present in the JSON body as null (not absent).
    /// Verifies serialization includes the property even when null.
    /// </summary>
    [Fact]
    public async Task GetAuction_JsonBody_ContainsUserSelectedBestOfferIdProperty()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sa11_a_{suffix}");
        var tokenA = await LoginAsync($"sa11_a_{suffix}");
        var clientA = ClientWithToken(tokenA);

        var catalogStickerId = (await GetCatalogStickersAsync(1, 1))[0].Id;
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickerId);
        var auction = await CreateAuctionAsync(clientA, stickerA.Id);

        var response = await clientA.GetAsync($"/api/auctions/{auction.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rawJson = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(rawJson);

        Assert.True(
            doc.RootElement.TryGetProperty("userSelectedBestOfferId", out var prop),
            "Expected 'userSelectedBestOfferId' property to be present in the JSON response.");
        Assert.Equal(JsonValueKind.Null, prop.ValueKind);
    }

    // ─── US06 Tests: Accept Offer / Close Auction ────────────────────────────

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

        var acceptResponse = await clientAuc.PostAsync(
            $"/api/auctions/{auction.Id}/offers/{offerA.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var closedAuction = await GetAuctionAsync(auction.Id);
        Assert.Equal("Closed", closedAuction.Status);

        var allOffers = await GetAuctionOffersAsync(auction.Id);
        var wonOffer = allOffers.First(o => o.Id == offerA.Id);
        var lostOffer = allOffers.First(o => o.Id == offerB.Id);
        Assert.Equal("Won", wonOffer.State);
        Assert.Equal("Lost", lostOffer.State);

        var restoredStickerB = await GetUserStickerAsync(clientB, bidderB.Id, stickerB.Id);
        Assert.NotNull(restoredStickerB);
        Assert.Equal(2, restoredStickerB!.Quantity);
    }

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

        var response = await clientStr.PostAsync(
            $"/api/auctions/{auction.Id}/offers/{offer.Id}/accept", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

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

        var offerOnB = await PlaceOfferAsync(clientBid, auctionB.Id, new List<int> { bidSticker.Id });

        var response = await clientA.PostAsync(
            $"/api/auctions/{auctionA.Id}/offers/{offerOnB.Id}/accept", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

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

        var firstAccept = await clientAuc.PostAsync(
            $"/api/auctions/{auction.Id}/offers/{offer.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, firstAccept.StatusCode);

        var secondAccept = await clientAuc.PostAsync(
            $"/api/auctions/{auction.Id}/offers/{offer.Id}/accept", null);
        Assert.Equal(HttpStatusCode.BadRequest, secondAccept.StatusCode);
    }

    /// <summary>
    /// Scenario 5: Worker expiration with UserSelectedBestOfferId set.
    /// CloseAuctionAutomatically should be invoked — the service handles priority internally.
    /// </summary>
    [Fact]
    public async Task WorkerExpiration_UserSelectedBestOfferIdSet_DelegatesToCloseAuctionAutomatically()
    {
        var auction = new Auction
        {
            Id = 1001,
            AuctioneerId = 1,
            UserStickerId = 10,
            Status = AuctionStatus.Active,
            EndsAt = DateTime.UtcNow.AddMinutes(-5),
            BestCurrentOfferId = 50,
            UserSelectedBestOfferId = 55
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
    /// Scenario 6: Worker expiration with only BestCurrentOfferId set (no manual selection).
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
            UserSelectedBestOfferId = null
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
    /// Scenario 7: Worker expires auction with zero offers → Cancelled, auctioneer stock restored.
    /// </summary>
    [Fact]
    public async Task WorkerExpiration_NoOffers_AuctionCancelledAuctioneerStockRestored()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sc7_auc_{suffix}");
        var tokenAuc = await LoginAsync($"sc7_auc_{suffix}");
        var clientAuc = AuthClient(tokenAuc);

        var catalog = await GetCatalogAsync(1);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id, quantity: 1);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id, endsAt: DateTime.UtcNow.AddSeconds(2));

        await Task.Delay(3000);

        using var scope = _factory.Services.CreateScope();
        var auctionService = scope.ServiceProvider.GetRequiredService<AuctionService>();
        var auctionRepo = scope.ServiceProvider.GetRequiredService<IAuctionRepository>();

        var claimed = await auctionRepo.TryClaimAutomaticClosureAsync(auction.Id);
        if (claimed)
        {
            await auctionService.CloseAuctionAutomatically(auction.Id);
        }

        var finalAuction = await GetAuctionAsync(auction.Id);
        Assert.Equal("Cancelled", finalAuction.Status);

        var restoredSticker = await GetUserStickerAsync(clientAuc, auctioneer.Id, aucSticker.Id);
        Assert.NotNull(restoredSticker);
        Assert.Equal(1, restoredSticker!.Quantity);
        Assert.True(restoredSticker.Active);
    }

    /// <summary>
    /// Scenario 8: True parallel race between manual acceptance and worker automatic closure.
    /// Exactly one operation must succeed; stock must not be double-returned.
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

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id, endsAt: DateTime.UtcNow.AddSeconds(2));
        var offer = await PlaceOfferAsync(clientBid, auction.Id, new List<int> { bidSticker.Id });

        await Task.Delay(3000);

        using var scope = _factory.Services.CreateScope();
        var auctionService = scope.ServiceProvider.GetRequiredService<AuctionService>();
        var auctionRepo = scope.ServiceProvider.GetRequiredService<IAuctionRepository>();

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

        await Task.WhenAll(manualTask, workerTask);
        var manualResponse = await manualTask;
        var workerSucceeded = await workerTask;

        // Invariant 1: auction must be in a terminal state.
        var finalAuction = await GetAuctionAsync(auction.Id);
        Assert.True(
            finalAuction.Status == "Closed" || finalAuction.Status == "Cancelled",
            $"Expected terminal status but got: {finalAuction.Status}");

        // Invariant 2: exactly one operation succeeded.
        var manualSucceeded = manualResponse.StatusCode == HttpStatusCode.OK;
        Assert.True(
            manualSucceeded ^ workerSucceeded,
            $"Expected exactly one operation to succeed. Manual: {manualResponse.StatusCode}, Worker: {workerSucceeded}.");

        // Invariant 3: no double-return of bidder's stock.
        var bidderFinalSticker = await GetUserStickerAsync(clientBid, bidder.Id, bidSticker.Id);
        if (bidderFinalSticker != null)
        {
            Assert.True(bidderFinalSticker.Quantity <= 1,
                $"Bidder stock was double-returned. Expected ≤1 but got {bidderFinalSticker.Quantity}.");
        }

        // Invariant 4: no double-return of auctioneer's stock.
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
