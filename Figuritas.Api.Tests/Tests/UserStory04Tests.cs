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
/// Integration tests for User Story 04 — Automatic exchange suggestions.
/// Uses WebApplicationFactory to run the API in memory.
/// Requires a running MongoDB instance (same as the app).
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class UserStory04Tests : IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public UserStory04Tests(IntegrationTestFactory factory)
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
        var user = await response.Content.ReadFromJsonAsync<UserResponseDTO>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return user!;
    }

    private async Task<string> LoginAsync(string username, string password)
    {
        var dto = new { Username = username, Password = password };
        var response = await _client.PostAsJsonAsync("/api/auth/login", dto);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
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

    private async Task AddMissingStickerAsync(HttpClient authenticatedClient, int userId, int stickerId)
    {
        var dto = new { StickerId = stickerId };
        var response = await authenticatedClient.PostAsJsonAsync($"/api/users/{userId}/missing-stickers", dto);
        response.EnsureSuccessStatusCode();
    }

    // ─── US04 Tests ──────────────────────────────────────────────────────────

    /// <summary>
    /// Escenario 1: GET /api/market/suggestions sin token JWT → 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task US04_GetSuggestions_WithoutToken_Returns401()
    {
        var anonymousClient = _factory.CreateClient();
        var response = await anonymousClient.GetAsync("/api/market/suggestions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Escenario 2: Usuario sin faltantes registrados → lista vacía.
    /// </summary>
    [Fact]
    public async Task US04_GetSuggestions_NoMissingStickers_ReturnsEmptyList()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us04_nomissing_{uniqueSuffix}", "Password123");
        var tokenA = await LoginAsync($"us04_nomissing_{uniqueSuffix}", "Password123");
        var clientA = ClientWithToken(tokenA);

        var response = await clientA.GetAsync("/api/market/suggestions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = await response.Content.ReadFromJsonAsync<List<ExchangeSuggestionResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(results);
        Assert.Empty(results);
    }


    /// <summary>
    /// Escenario 4: Match perfecto — A tiene faltante stickerX y publica stickerY.
    /// B publica stickerX y tiene como faltante stickerY. Verificar IsPerfectMatch=true
    /// y que MatchedStickerNumber no es null.
    /// </summary>
    [Fact]
    public async Task US04_GetSuggestions_PerfectMatch_ReturnsIsPerfectMatchTrue()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us04_perfect_a_{uniqueSuffix}", "Password123");
        var userB = await RegisterUserAsync($"us04_perfect_b_{uniqueSuffix}", "Password123");
        var tokenA = await LoginAsync($"us04_perfect_a_{uniqueSuffix}", "Password123");
        var tokenB = await LoginAsync($"us04_perfect_b_{uniqueSuffix}", "Password123");

        var stickers = await GetCatalogStickersAsync(1, 2);
        var stickerX = stickers[0];
        var stickerY = stickers[1];

        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        // A publica stickerY (para intercambio directo)
        await PublishStickerAsync(clientA, userA.Id, stickerY.Id, quantity: 1, canBeDirectlyExchanged: true);
        // A tiene stickerX como faltante
        await AddMissingStickerAsync(clientA, userA.Id, stickerX.Id);

        // B publica stickerX (para intercambio directo)
        await PublishStickerAsync(clientB, userB.Id, stickerX.Id, quantity: 1, canBeDirectlyExchanged: true);
        // B tiene stickerY como faltante
        await AddMissingStickerAsync(clientB, userB.Id, stickerY.Id);

        var response = await clientA.GetAsync("/api/market/suggestions?PageSize=500");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = await response.Content.ReadFromJsonAsync<List<ExchangeSuggestionResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(results);
        Assert.NotEmpty(results);

        var suggestion = results.FirstOrDefault(r => r.SuggestedUserId == userB.Id && r.StickerId == stickerX.Id);
        Assert.NotNull(suggestion);
        Assert.True(suggestion.IsPerfectMatch);
        Assert.NotEmpty(suggestion.CallerMatchedStickers);
    }

    /// <summary>
    /// Escenario 5: Exclusión Quantity=0 — B publica stickerX con Quantity=0.
    /// A tiene stickerX como faltante. Respuesta vacía (sin resultados de B).
    /// </summary>
    [Fact]
    public async Task US04_GetSuggestions_ExcludesZeroQuantityListings()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us04_qty_a_{uniqueSuffix}", "Password123");
        var userB = await RegisterUserAsync($"us04_qty_b_{uniqueSuffix}", "Password123");
        var tokenA = await LoginAsync($"us04_qty_a_{uniqueSuffix}", "Password123");
        var tokenB = await LoginAsync($"us04_qty_b_{uniqueSuffix}", "Password123");

        var stickers = await GetCatalogStickersAsync(1, 1);
        var stickerX = stickers[0];

        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        // B publica stickerX con quantity=2, luego la baja a 0
        var published = await PublishStickerAsync(clientB, userB.Id, stickerX.Id, quantity: 2, canBeDirectlyExchanged: true);
        var patchDto = new { Quantity = 0 };
        var patchResponse = await clientB.PatchAsJsonAsync($"/api/users/{userB.Id}/stickers/{published.Id}", patchDto);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        // A registra stickerX como faltante
        await AddMissingStickerAsync(clientA, userA.Id, stickerX.Id);

        var response = await clientA.GetAsync("/api/market/suggestions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = await response.Content.ReadFromJsonAsync<List<ExchangeSuggestionResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(results);
        Assert.DoesNotContain(results, r => r.SuggestedUserId == userB.Id && r.StickerId == stickerX.Id);
    }

    /// <summary>
    /// Escenario 6: Exclusión CanBeDirectlyExchanged=false — B publica stickerX sin permitir intercambio directo.
    /// A tiene stickerX como faltante. No aparece en sugerencias.
    /// </summary>
    [Fact]
    public async Task US04_GetSuggestions_ExcludesNonDirectlyExchangeableListings()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us04_noex_a_{uniqueSuffix}", "Password123");
        var userB = await RegisterUserAsync($"us04_noex_b_{uniqueSuffix}", "Password123");
        var tokenA = await LoginAsync($"us04_noex_a_{uniqueSuffix}", "Password123");
        var tokenB = await LoginAsync($"us04_noex_b_{uniqueSuffix}", "Password123");

        var stickers = await GetCatalogStickersAsync(1, 1);
        var stickerX = stickers[0];

        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        // B publica stickerX con CanBeDirectlyExchanged=false
        await PublishStickerAsync(clientB, userB.Id, stickerX.Id, quantity: 2, canBeDirectlyExchanged: false);

        // A registra stickerX como faltante
        await AddMissingStickerAsync(clientA, userA.Id, stickerX.Id);

        var response = await clientA.GetAsync("/api/market/suggestions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = await response.Content.ReadFromJsonAsync<List<ExchangeSuggestionResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(results);
        Assert.DoesNotContain(results, r => r.SuggestedUserId == userB.Id && r.StickerId == stickerX.Id);
    }

    /// <summary>
    /// Escenario 7: El caller no es candidato de sí mismo —
    /// A publica stickerX y A tiene stickerX como faltante. A no aparece en las sugerencias.
    /// </summary>
    [Fact]
    public async Task US04_GetSuggestions_CallerIsNotSuggestedToHimself()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us04_self_{uniqueSuffix}", "Password123");
        var tokenA = await LoginAsync($"us04_self_{uniqueSuffix}", "Password123");
        var clientA = ClientWithToken(tokenA);

        var stickers = await GetCatalogStickersAsync(1, 1);
        var stickerX = stickers[0];

        // A publica stickerX
        await PublishStickerAsync(clientA, userA.Id, stickerX.Id, quantity: 2, canBeDirectlyExchanged: true);

        // A registra stickerX como faltante
        await AddMissingStickerAsync(clientA, userA.Id, stickerX.Id);

        var response = await clientA.GetAsync("/api/market/suggestions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = await response.Content.ReadFromJsonAsync<List<ExchangeSuggestionResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(results);
        Assert.DoesNotContain(results, r => r.SuggestedUserId == userA.Id);
    }

    // ─── IAsyncLifetime ──────────────────────────────────────────────────────

    public async Task InitializeAsync() => await _factory.CleanMutableCollectionsAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ─── US04 Tests (continued) ──────────────────────────────────────────────

    /// <summary>
    /// Scenario 8: The service only returns perfect matches (SuggestionService skips
    /// non-perfect matches via an early continue). A has stickerY as missing and
    /// publishes stickerZ. C publishes stickerY (which A needs) and has stickerZ as
    /// missing (which A has) — forming a perfect match. Verifies that results contain
    /// at least one perfect match and that the first result is a perfect match.
    /// </summary>
    [Fact]
    public async Task US04_GetSuggestions_OnlyPerfectMatchesReturned_AndFirstIsPerfectMatch()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us04_order_a_{uniqueSuffix}", "Password123");
        var userC = await RegisterUserAsync($"us04_order_c_{uniqueSuffix}", "Password123");
        var tokenA = await LoginAsync($"us04_order_a_{uniqueSuffix}", "Password123");
        var tokenC = await LoginAsync($"us04_order_c_{uniqueSuffix}", "Password123");

        var stickers = await GetCatalogStickersAsync(1, 2);
        var stickerY = stickers[0];
        var stickerZ = stickers[1];

        var clientA = ClientWithToken(tokenA);
        var clientC = ClientWithToken(tokenC);

        // A has stickerY as missing
        await AddMissingStickerAsync(clientA, userA.Id, stickerY.Id);

        // A publishes stickerZ for direct exchange (enables a perfect match with C)
        await PublishStickerAsync(clientA, userA.Id, stickerZ.Id, quantity: 1, canBeDirectlyExchanged: true);

        // C publishes stickerY (which A needs) and has stickerZ as missing (which A has) — perfect match
        await PublishStickerAsync(clientC, userC.Id, stickerY.Id, quantity: 1, canBeDirectlyExchanged: true);
        await AddMissingStickerAsync(clientC, userC.Id, stickerZ.Id);

        var response = await clientA.GetAsync("/api/market/suggestions?PageSize=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = await response.Content.ReadFromJsonAsync<List<ExchangeSuggestionResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(results);
        Assert.NotEmpty(results);

        // The service only returns perfect matches
        Assert.Contains(results, r => r.IsPerfectMatch);

        // The first element of the list must be a perfect match
        Assert.True(results.First().IsPerfectMatch, "The first suggestion should be a perfect match.");
    }
}
