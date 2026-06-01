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
public class UserStory06Tests : IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public UserStory06Tests(IntegrationTestFactory factory)
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
        var user = await response.Content.ReadFromJsonAsync<UserResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return user!;
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
        var created = await response.Content.ReadFromJsonAsync<UserStickerResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return created!;
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
        var created = await response.Content.ReadFromJsonAsync<AuctionResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return created!;
    }

    // ─── US06 Tests ──────────────────────────────────────────────────────────

    /// <summary>
    /// Escenario 1: Usuario autenticado publica subasta válida. Valida HTTP 201, Status == "Active".
    /// </summary>
    [Fact]
    public async Task CreateAuction_WithValidData_Returns201AndStatusActive()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var user = await RegisterUserAsync($"us06_valid_{suffix}", "password123");
        var token = await LoginAsync($"us06_valid_{suffix}", "password123");
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
        var result = await response.Content.ReadFromJsonAsync<AuctionResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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
        var user = await RegisterUserAsync($"us06_qty0_{suffix}", "password123");
        var token = await LoginAsync($"us06_qty0_{suffix}", "password123");
        var client = ClientWithToken(token);

        var catalogStickers = await GetCatalogStickersAsync(1, 1);
        // Publish with quantity=1, then patch down to 0
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
        var user = await RegisterUserAsync($"us06_noauct_{suffix}", "password123");
        var token = await LoginAsync($"us06_noauct_{suffix}", "password123");
        var client = ClientWithToken(token);

        var catalogStickers = await GetCatalogStickersAsync(1, 1);
        // Publish with canBeAuctioned=false
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
        var user = await RegisterUserAsync($"us06_past_{suffix}", "password123");
        var token = await LoginAsync($"us06_past_{suffix}", "password123");
        var client = ClientWithToken(token);

        var catalogStickers = await GetCatalogStickersAsync(1, 1);
        var userSticker = await PublishUserStickerAsync(client, user.Id, catalogStickers[0].Id);

        var dto = new PostAuctionRequestDTO
        {
            UserStickerId = userSticker.Id,
            MinimumOfferStickerIds = new List<int>(),
            EndsAt = DateTime.UtcNow.AddHours(-1) // Past date
        };

        var response = await client.PostAsJsonAsync("/api/auctions", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Escenario 6: Tras POST exitoso, verificar que Quantity del UserSticker bajó en 1 (GET sticker).
    /// </summary>
    [Fact]
    public async Task CreateAuction_ValidPublication_DecrementsUserStickerQuantity()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var user = await RegisterUserAsync($"us06_decr_{suffix}", "password123");
        var token = await LoginAsync($"us06_decr_{suffix}", "password123");
        var client = ClientWithToken(token);

        var catalogStickers = await GetCatalogStickersAsync(1, 1);
        var userSticker = await PublishUserStickerAsync(client, user.Id, catalogStickers[0].Id, quantity: 3);
        var originalQuantity = userSticker.Quantity; // Should be 3

        var dto = new PostAuctionRequestDTO
        {
            UserStickerId = userSticker.Id,
            MinimumOfferStickerIds = new List<int>(),
            EndsAt = DateTime.UtcNow.AddHours(1)
        };

        var createResponse = await client.PostAsJsonAsync("/api/auctions", dto);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        // Fetch updated sticker from API
        var getResponse = await client.GetAsync($"/api/users/{user.Id}/stickers");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var stickers = await getResponse.Content.ReadFromJsonAsync<List<UserStickerResponseDTO>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(stickers);
        var updated = stickers!.FirstOrDefault(s => s.Id == userSticker.Id);
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
        var user = await RegisterUserAsync($"us06_last_{suffix}", "password123");
        var token = await LoginAsync($"us06_last_{suffix}", "password123");
        var client = ClientWithToken(token);

        var catalogStickers = await GetCatalogStickersAsync(1, 1);
        // Publish with exactly quantity=1
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

        // Sticker is now inactive (Quantity=0, Active=false).
        // GetById filters by Active=true, so it won't appear in the active list.
        // We use GetByUserId which also checks Active. So we try the individual sticker endpoint.
        // If it returns 404, it confirms Active=false. If returns 200, verify Quantity=0 and Active=false.
        var getAllResponse = await client.GetAsync($"/api/users/{user.Id}/stickers");
        Assert.Equal(HttpStatusCode.OK, getAllResponse.StatusCode);
        var allStickers = await getAllResponse.Content.ReadFromJsonAsync<List<UserStickerResponseDTO>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(allStickers);
        // The sticker with quantity=0 should not appear as active
        var found = allStickers!.FirstOrDefault(s => s.Id == userSticker.Id);
        // Either not found (filtered out as inactive) or found with Active=false and Quantity=0
        if (found != null)
        {
            Assert.Equal(0, found.Quantity);
            Assert.False(found.Active);
        }
        // If not found in the active listing, the test passes — it was correctly deactivated
    }

    /// <summary>
    /// Escenario 8: Subastar UserStickerId que existe pero pertenece a otro usuario → HTTP 400.
    /// </summary>
    [Fact]
    public async Task CreateAuction_StickerNotOwnedByCaller_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us06_own_a_{suffix}", "password123");
        var userB = await RegisterUserAsync($"us06_own_b_{suffix}", "password123");
        var tokenA = await LoginAsync($"us06_own_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us06_own_b_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 1);
        // UserB publishes a sticker
        var stickerOfB = await PublishUserStickerAsync(clientB, userB.Id, catalogStickers[0].Id);

        // UserA tries to auction UserB's sticker
        var dto = new PostAuctionRequestDTO
        {
            UserStickerId = stickerOfB.Id,
            MinimumOfferStickerIds = new List<int>(),
            EndsAt = DateTime.UtcNow.AddHours(1)
        };

        var response = await clientA.PostAsJsonAsync("/api/auctions", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Escenario 9: Usuario B oferta figuritas válidas sobre subasta activa de usuario A → HTTP 201.
    /// </summary>
    [Fact]
    public async Task CreateOffer_OnActiveAuction_Returns201Created()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us06_ofr_a_{suffix}", "password123");
        var userB = await RegisterUserAsync($"us06_ofr_b_{suffix}", "password123");
        var tokenA = await LoginAsync($"us06_ofr_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us06_ofr_b_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerB = await PublishUserStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientA, stickerA.Id);

        var offerDto = new PostAuctionOfferRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerB.Id }
        };

        var response = await clientB.PostAsJsonAsync($"/api/auctions/{auction.Id}/offers", offerDto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuctionOfferResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.Equal(userB.Id, result!.BidderId);
        Assert.Contains(stickerB.Id, result.OfferedUserStickerIds);
    }

    /// <summary>
    /// Escenario 10: Ofertar en subasta con EndsAt ya pasado → HTTP 400.
    /// </summary>
    [Fact]
    public async Task CreateOffer_OnExpiredAuction_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us06_exp_a_{suffix}", "password123");
        var userB = await RegisterUserAsync($"us06_exp_b_{suffix}", "password123");
        var tokenA = await LoginAsync($"us06_exp_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us06_exp_b_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerB = await PublishUserStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);

        // Create auction that expires in 2 seconds
        var auction = await CreateAuctionAsync(clientA, stickerA.Id,
            endsAt: DateTime.UtcNow.AddSeconds(2));

        // Wait for auction to expire
        await Task.Delay(3000);

        var offerDto = new PostAuctionOfferRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerB.Id }
        };

        var response = await clientB.PostAsJsonAsync($"/api/auctions/{auction.Id}/offers", offerDto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── IAsyncLifetime ──────────────────────────────────────────────────────

    public async Task InitializeAsync() => await _factory.CleanMutableCollectionsAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
