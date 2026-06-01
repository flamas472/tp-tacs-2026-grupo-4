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

/// <summary>
/// Integration tests for User Story 03 — Search available stickers on the market.
/// Uses WebApplicationFactory to run the API in memory.
/// Requires a running MongoDB instance (same as the app).
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class UserStory03Tests : IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public UserStory03Tests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.CleanMutableCollectionsAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<UserResponseDTO> RegisterUserAsync(string username, string password)
    {
        var dto = new { Username = username, Password = password };
        var response = await _client.PostAsJsonAsync("/api/users", dto);
        response.EnsureSuccessStatusCode();
        var user = await response.Content.ReadFromJsonAsync<UserResponseDTO>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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

    private async Task<List<Sticker>> GetCatalogStickersAsync(int page, int pageSize)
    {
        var response = await _client.GetAsync($"/api/stickers?Page={page}&PageSize={pageSize}");
        response.EnsureSuccessStatusCode();
        var stickers = await response.Content.ReadFromJsonAsync<List<Sticker>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(stickers);
        Assert.NotEmpty(stickers);
        return stickers!;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<UserStickerResponseDTO> PublishStickerAsync(HttpClient authenticatedClient, int userId, int stickerId, int quantity = 2, bool canBeDirectlyExchanged = true, bool canBeAuctioned = false)
    {
        var dto = new PostUserStickerRequestDTO
        {
            StickerId = stickerId,
            Quantity = quantity,
            CanBeDirectlyExchanged = canBeDirectlyExchanged,
            CanBeAuctioned = canBeAuctioned
        };
        var response = await authenticatedClient.PostAsJsonAsync($"/api/users/{userId}/stickers", dto);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<UserStickerResponseDTO>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return created!;
    }

    // ─── US03 Tests ──────────────────────────────────────────────────────────

    /// <summary>
    /// Escenario 1: GET /api/market/stickers sin token JWT → 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task US03_GetMarketStickers_WithoutToken_Returns401()
    {
        var anonymousClient = _factory.CreateClient();
        var response = await anonymousClient.GetAsync("/api/market/stickers");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Escenario 2: Usuario A publica figurita, Usuario A busca → su propia figurita no aparece.
    /// </summary>
    [Fact]
    public async Task US03_GetMarketStickers_DoesNotReturnOwnStickers()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us03_own_{uniqueSuffix}", "password123");
        var tokenA = await LoginAsync($"us03_own_{uniqueSuffix}", "password123");

        var stickers = await GetCatalogStickersAsync(1, 1);
        var authenticatedClient = ClientWithToken(tokenA);
        await PublishStickerAsync(authenticatedClient, userA.Id, stickers[0].Id);

        var searchResponse = await authenticatedClient.GetAsync("/api/market/stickers");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var results = await searchResponse.Content.ReadFromJsonAsync<List<MarketStickerResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(results);
        Assert.DoesNotContain(results, r => r.OwnerId == userA.Id);
    }

    /// <summary>
    /// Escenario 3: Figurita con Quantity = 0 no aparece en resultados del mercado.
    /// </summary>
    [Fact]
    public async Task US03_GetMarketStickers_ExcludesZeroQuantityStickers()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us03_qty_a_{uniqueSuffix}", "password123");
        var userB = await RegisterUserAsync($"us03_qty_b_{uniqueSuffix}", "password123");
        var tokenA = await LoginAsync($"us03_qty_a_{uniqueSuffix}", "password123");
        var tokenB = await LoginAsync($"us03_qty_b_{uniqueSuffix}", "password123");

        var stickers = await GetCatalogStickersAsync(1, 2);

        // User B publica con cantidad 0 (mediante PATCH después de publicar)
        var clientB = ClientWithToken(tokenB);
        var published = await PublishStickerAsync(clientB, userB.Id, stickers[0].Id, quantity: 2);

        // Actualizar quantity a 0 via PATCH
        var patchDto = new { Quantity = 0 };
        var patchResponse = await clientB.PatchAsJsonAsync($"/api/users/{userB.Id}/stickers/{published.Id}", patchDto);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var clientA = ClientWithToken(tokenA);
        var searchResponse = await clientA.GetAsync("/api/market/stickers");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var results = await searchResponse.Content.ReadFromJsonAsync<List<MarketStickerResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(results);
        Assert.DoesNotContain(results, r => r.UserStickerId == published.Id);
    }

    /// <summary>
    /// Escenario 4: Figurita con CanBeDirectlyExchanged = false y CanBeAuctioned = false no aparece.
    /// </summary>
    [Fact]
    public async Task US03_GetMarketStickers_ExcludesNonExchangeableStickers()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us03_noex_a_{uniqueSuffix}", "password123");
        var userB = await RegisterUserAsync($"us03_noex_b_{uniqueSuffix}", "password123");
        var tokenA = await LoginAsync($"us03_noex_a_{uniqueSuffix}", "password123");
        var tokenB = await LoginAsync($"us03_noex_b_{uniqueSuffix}", "password123");

        var stickers = await GetCatalogStickersAsync(1, 1);
        var clientB = ClientWithToken(tokenB);

        // Publicar con ambas flags en false
        var published = await PublishStickerAsync(clientB, userB.Id, stickers[0].Id, quantity: 2, canBeDirectlyExchanged: false, canBeAuctioned: false);

        var clientA = ClientWithToken(tokenA);
        var searchResponse = await clientA.GetAsync("/api/market/stickers");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var results = await searchResponse.Content.ReadFromJsonAsync<List<MarketStickerResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(results);
        Assert.DoesNotContain(results, r => r.UserStickerId == published.Id);
    }

    /// <summary>
    /// Escenario 5: Filtro dinámico por Number funciona correctamente.
    /// </summary>
    [Fact]
    public async Task US03_GetMarketStickers_FilterByNumber_ReturnsMatchingResults()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us03_num_a_{uniqueSuffix}", "password123");
        var userB = await RegisterUserAsync($"us03_num_b_{uniqueSuffix}", "password123");
        var tokenA = await LoginAsync($"us03_num_a_{uniqueSuffix}", "password123");
        var tokenB = await LoginAsync($"us03_num_b_{uniqueSuffix}", "password123");

        var stickers = await GetCatalogStickersAsync(1, 2);
        var clientB = ClientWithToken(tokenB);
        await PublishStickerAsync(clientB, userB.Id, stickers[0].Id);

        var clientA = ClientWithToken(tokenA);
        var targetNumber = stickers[0].Number;
        var searchResponse = await clientA.GetAsync($"/api/market/stickers?Number={targetNumber}");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var results = await searchResponse.Content.ReadFromJsonAsync<List<MarketStickerResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(results);
        Assert.All(results, r => Assert.Equal(targetNumber, r.StickerNumber));
    }

    /// <summary>
    /// Escenario 6: Filtro dinámico por NationalTeam funciona correctamente.
    /// </summary>
    [Fact]
    public async Task US03_GetMarketStickers_FilterByNationalTeam_ReturnsMatchingResults()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us03_nat_a_{uniqueSuffix}", "password123");
        var userB = await RegisterUserAsync($"us03_nat_b_{uniqueSuffix}", "password123");
        var tokenA = await LoginAsync($"us03_nat_a_{uniqueSuffix}", "password123");
        var tokenB = await LoginAsync($"us03_nat_b_{uniqueSuffix}", "password123");

        var stickers = await GetCatalogStickersAsync(1, 1);
        var clientB = ClientWithToken(tokenB);
        await PublishStickerAsync(clientB, userB.Id, stickers[0].Id);

        var clientA = ClientWithToken(tokenA);
        var nationalTeam = stickers[0].NationalTeam;
        var searchResponse = await clientA.GetAsync($"/api/market/stickers?NationalTeam={Uri.EscapeDataString(nationalTeam)}");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var results = await searchResponse.Content.ReadFromJsonAsync<List<MarketStickerResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(results);
        Assert.All(results, r => Assert.Contains(nationalTeam, r.StickerNationalTeam, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Escenario 7: Filtro dinámico por Description (jugador) funciona correctamente.
    /// </summary>
    [Fact]
    public async Task US03_GetMarketStickers_FilterByDescription_ReturnsMatchingResults()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us03_desc_a_{uniqueSuffix}", "password123");
        var userB = await RegisterUserAsync($"us03_desc_b_{uniqueSuffix}", "password123");
        var tokenA = await LoginAsync($"us03_desc_a_{uniqueSuffix}", "password123");
        var tokenB = await LoginAsync($"us03_desc_b_{uniqueSuffix}", "password123");

        var stickers = await GetCatalogStickersAsync(1, 1);
        var clientB = ClientWithToken(tokenB);
        await PublishStickerAsync(clientB, userB.Id, stickers[0].Id);

        var clientA = ClientWithToken(tokenA);
        var description = stickers[0].Description;
        var searchResponse = await clientA.GetAsync($"/api/market/stickers?Description={Uri.EscapeDataString(description)}");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var results = await searchResponse.Content.ReadFromJsonAsync<List<MarketStickerResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(results);
        Assert.All(results, r => Assert.Contains(description, r.StickerDescription, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Escenario 8: Page = 0 o PageSize = 0 → 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task US03_GetMarketStickers_InvalidPagination_Returns400()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var user = await RegisterUserAsync($"us03_pag_{uniqueSuffix}", "password123");
        var token = await LoginAsync($"us03_pag_{uniqueSuffix}", "password123");
        var authenticatedClient = ClientWithToken(token);

        var responsePageZero = await authenticatedClient.GetAsync("/api/market/stickers?Page=0&PageSize=20");
        Assert.Equal(HttpStatusCode.BadRequest, responsePageZero.StatusCode);

        var responsePageSizeZero = await authenticatedClient.GetAsync("/api/market/stickers?Page=1&PageSize=0");
        Assert.Equal(HttpStatusCode.BadRequest, responsePageSizeZero.StatusCode);
    }

    /// <summary>
    /// Escenario 9: Figurita válida publicada por Usuario B es visible para Usuario A.
    /// </summary>
    [Fact]
    public async Task US03_GetMarketStickers_ReturnsOtherUsersValidStickers()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us03_vis_a_{uniqueSuffix}", "password123");
        var userB = await RegisterUserAsync($"us03_vis_b_{uniqueSuffix}", "password123");
        var tokenA = await LoginAsync($"us03_vis_a_{uniqueSuffix}", "password123");
        var tokenB = await LoginAsync($"us03_vis_b_{uniqueSuffix}", "password123");

        var stickers = await GetCatalogStickersAsync(1, 1);
        var clientB = ClientWithToken(tokenB);
        var published = await PublishStickerAsync(clientB, userB.Id, stickers[0].Id, quantity: 3, canBeDirectlyExchanged: true, canBeAuctioned: false);

        var clientA = ClientWithToken(tokenA);
        var searchResponse = await clientA.GetAsync("/api/market/stickers");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var results = await searchResponse.Content.ReadFromJsonAsync<List<MarketStickerResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(results);
        Assert.Contains(results, r =>
            r.UserStickerId == published.Id &&
            r.OwnerId == userB.Id &&
            r.Quantity == 3 &&
            r.CanBeDirectlyExchanged == true);
    }

    /// <summary>
    /// Escenario 10: Filtro dinámico por Team funciona correctamente.
    /// </summary>
    [Fact]
    public async Task US03_GetMarketStickers_FilterByTeam_ReturnsMatchingResults()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us03_team_a_{uniqueSuffix}", "password123");
        var userB = await RegisterUserAsync($"us03_team_b_{uniqueSuffix}", "password123");
        var tokenA = await LoginAsync($"us03_team_a_{uniqueSuffix}", "password123");
        var tokenB = await LoginAsync($"us03_team_b_{uniqueSuffix}", "password123");

        var stickers = await GetCatalogStickersAsync(1, 1);
        var clientB = ClientWithToken(tokenB);
        await PublishStickerAsync(clientB, userB.Id, stickers[0].Id);

        var clientA = ClientWithToken(tokenA);
        var team = stickers[0].Team;
        var searchResponse = await clientA.GetAsync($"/api/market/stickers?Team={Uri.EscapeDataString(team)}");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var results = await searchResponse.Content.ReadFromJsonAsync<List<MarketStickerResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(results);
        Assert.All(results, r => Assert.Contains(team, r.StickerTeam, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Escenario 11: Filtro dinámico por Category funciona correctamente.
    /// </summary>
    [Fact]
    public async Task US03_GetMarketStickers_FilterByCategory_ReturnsMatchingResults()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us03_cat_a_{uniqueSuffix}", "password123");
        var userB = await RegisterUserAsync($"us03_cat_b_{uniqueSuffix}", "password123");
        var tokenA = await LoginAsync($"us03_cat_a_{uniqueSuffix}", "password123");
        var tokenB = await LoginAsync($"us03_cat_b_{uniqueSuffix}", "password123");

        var stickers = await GetCatalogStickersAsync(1, 1);
        var clientB = ClientWithToken(tokenB);
        await PublishStickerAsync(clientB, userB.Id, stickers[0].Id);

        var clientA = ClientWithToken(tokenA);
        var category = stickers[0].Category;
        var searchResponse = await clientA.GetAsync($"/api/market/stickers?Category={Uri.EscapeDataString(category)}");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var results = await searchResponse.Content.ReadFromJsonAsync<List<MarketStickerResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(results);
        Assert.All(results, r => Assert.Contains(category, r.StickerCategory, StringComparison.OrdinalIgnoreCase));
    }
}
