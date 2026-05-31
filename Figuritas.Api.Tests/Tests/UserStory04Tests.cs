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
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public UserStory04Tests(WebApplicationFactory<Program> factory)
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

    private async Task<UserSticker> PublishStickerAsync(HttpClient authenticatedClient, int userId, int stickerId, int quantity = 2, bool canBeDirectlyExchanged = true, bool canBeAuctioned = false)
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
        var created = await response.Content.ReadFromJsonAsync<UserSticker>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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
        var userA = await RegisterUserAsync($"us04_nomissing_{uniqueSuffix}", "password123");
        var tokenA = await LoginAsync($"us04_nomissing_{uniqueSuffix}", "password123");
        var clientA = ClientWithToken(tokenA);

        var response = await clientA.GetAsync("/api/market/suggestions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = await response.Content.ReadFromJsonAsync<List<ExchangeSuggestionResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    /// <summary>
    /// Escenario 3: Match simple — A tiene faltante stickerX, B publica stickerX con CanBeDirectlyExchanged=true,
    /// Quantity>0. Verificar que la respuesta incluye IsPerfectMatch=false.
    /// </summary>
    [Fact]
    public async Task US04_GetSuggestions_SimpleMatch_ReturnsIsPerfectMatchFalse()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us04_simple_a_{uniqueSuffix}", "password123");
        var userB = await RegisterUserAsync($"us04_simple_b_{uniqueSuffix}", "password123");
        var tokenA = await LoginAsync($"us04_simple_a_{uniqueSuffix}", "password123");
        var tokenB = await LoginAsync($"us04_simple_b_{uniqueSuffix}", "password123");

        // Obtener dos stickers del catálogo: X para que B publique y A tenga de faltante
        var stickers = await GetCatalogStickersAsync(1, 2);
        var stickerX = stickers[0];

        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        // B publica stickerX
        await PublishStickerAsync(clientB, userB.Id, stickerX.Id, quantity: 2, canBeDirectlyExchanged: true);

        // A registra stickerX como faltante (A no publica nada → no puede haber match perfecto)
        await AddMissingStickerAsync(clientA, userA.Id, stickerX.Id);

        var response = await clientA.GetAsync("/api/market/suggestions?PageSize=500");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = await response.Content.ReadFromJsonAsync<List<ExchangeSuggestionResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(results);
        Assert.NotEmpty(results);

        var suggestion = results.FirstOrDefault(r => r.SuggestedUserId == userB.Id && r.StickerId == stickerX.Id);
        Assert.NotNull(suggestion);
        Assert.False(suggestion.IsPerfectMatch);
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
        var userA = await RegisterUserAsync($"us04_perfect_a_{uniqueSuffix}", "password123");
        var userB = await RegisterUserAsync($"us04_perfect_b_{uniqueSuffix}", "password123");
        var tokenA = await LoginAsync($"us04_perfect_a_{uniqueSuffix}", "password123");
        var tokenB = await LoginAsync($"us04_perfect_b_{uniqueSuffix}", "password123");

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
        Assert.NotNull(suggestion.CallerMatchedStickerNumber);
    }

    /// <summary>
    /// Escenario 5: Exclusión Quantity=0 — B publica stickerX con Quantity=0.
    /// A tiene stickerX como faltante. Respuesta vacía (sin resultados de B).
    /// </summary>
    [Fact]
    public async Task US04_GetSuggestions_ExcludesZeroQuantityListings()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us04_qty_a_{uniqueSuffix}", "password123");
        var userB = await RegisterUserAsync($"us04_qty_b_{uniqueSuffix}", "password123");
        var tokenA = await LoginAsync($"us04_qty_a_{uniqueSuffix}", "password123");
        var tokenB = await LoginAsync($"us04_qty_b_{uniqueSuffix}", "password123");

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
        var userA = await RegisterUserAsync($"us04_noex_a_{uniqueSuffix}", "password123");
        var userB = await RegisterUserAsync($"us04_noex_b_{uniqueSuffix}", "password123");
        var tokenA = await LoginAsync($"us04_noex_a_{uniqueSuffix}", "password123");
        var tokenB = await LoginAsync($"us04_noex_b_{uniqueSuffix}", "password123");

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
        var userA = await RegisterUserAsync($"us04_self_{uniqueSuffix}", "password123");
        var tokenA = await LoginAsync($"us04_self_{uniqueSuffix}", "password123");
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

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;
    // Cleanup not implemented: no admin endpoint exists to delete test data from shared MongoDB.
    // To implement: add DELETE /api/admin/users/{id} or expose a test-only cleanup method,
    // then delete all UserStickers and MissingStickers created by test users here.

    // ─── US04 Tests (continued) ──────────────────────────────────────────────

    /// <summary>
    /// Escenario 8: Orden — perfectos primero. Hay un match simple y un match perfecto;
    /// el perfecto aparece antes en la lista.
    /// </summary>
    [Fact]
    public async Task US04_GetSuggestions_PerfectMatchAppearsFirst()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us04_order_a_{uniqueSuffix}", "password123");
        var userB = await RegisterUserAsync($"us04_order_b_{uniqueSuffix}", "password123");
        var userC = await RegisterUserAsync($"us04_order_c_{uniqueSuffix}", "password123");
        var tokenA = await LoginAsync($"us04_order_a_{uniqueSuffix}", "password123");
        var tokenB = await LoginAsync($"us04_order_b_{uniqueSuffix}", "password123");
        var tokenC = await LoginAsync($"us04_order_c_{uniqueSuffix}", "password123");

        var stickers = await GetCatalogStickersAsync(1, 3);
        var stickerX = stickers[0];
        var stickerY = stickers[1];
        var stickerZ = stickers[2];

        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);
        var clientC = ClientWithToken(tokenC);

        // A tiene stickerX y stickerY como faltantes
        await AddMissingStickerAsync(clientA, userA.Id, stickerX.Id);
        await AddMissingStickerAsync(clientA, userA.Id, stickerY.Id);

        // A publica stickerZ para intercambio (para el match perfecto con B)
        await PublishStickerAsync(clientA, userA.Id, stickerZ.Id, quantity: 1, canBeDirectlyExchanged: true);

        // B publica stickerX (match simple: B no tiene faltantes que A pueda cubrir)
        await PublishStickerAsync(clientB, userB.Id, stickerX.Id, quantity: 1, canBeDirectlyExchanged: true);

        // C publica stickerY (match perfecto: C tiene stickerZ como faltante, que A posee)
        await PublishStickerAsync(clientC, userC.Id, stickerY.Id, quantity: 1, canBeDirectlyExchanged: true);
        await AddMissingStickerAsync(clientC, userC.Id, stickerZ.Id);

        var response = await clientA.GetAsync("/api/market/suggestions?PageSize=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = await response.Content.ReadFromJsonAsync<List<ExchangeSuggestionResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(results);
        Assert.NotEmpty(results);

        // Verificar que hay al menos un match perfecto y uno simple
        Assert.Contains(results, r => r.IsPerfectMatch);
        Assert.Contains(results, r => !r.IsPerfectMatch);

        // El primer elemento de la lista debe ser un match perfecto
        Assert.True(results.First().IsPerfectMatch, "The first suggestion should be a perfect match.");
    }
}
