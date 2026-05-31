using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Figuritas.Shared.DTO;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Figuritas.Api.Tests;

/// <summary>
/// Integration tests for User Story 02 — Missing stickers management.
/// Uses WebApplicationFactory to run the API in memory.
/// Requires a running MongoDB instance (same as the app).
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class UserStory02Tests
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public UserStory02Tests(WebApplicationFactory<Program> factory)
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

    private async Task<int> GetFirstCatalogStickerIdAsync()
    {
        var response = await _client.GetAsync("/api/stickers?Page=1&PageSize=1");
        response.EnsureSuccessStatusCode();
        var stickers = await response.Content.ReadFromJsonAsync<List<Sticker>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(stickers);
        Assert.NotEmpty(stickers);
        return stickers![0].Id;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ─── US02 Tests ──────────────────────────────────────────────────────────

    /// <summary>
    /// Escenario 1: Agregar faltante válido → 201 Created con UserId, StickerId, RegisteredAt.
    /// Header Location apunta a GET /api/users/{userId}/missing-stickers.
    /// </summary>
    [Fact]
    public async Task US02_PostMissingSticker_ReturnsCreated()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var username = $"us02_post_{uniqueSuffix}";
        var user = await RegisterUserAsync(username, "password123");
        var token = await LoginAsync(username, "password123");
        var stickerId = await GetFirstCatalogStickerIdAsync();

        var authenticatedClient = ClientWithToken(token);
        var dto = new PostMissingStickerRequestDTO { StickerId = stickerId };

        var response = await authenticatedClient.PostAsJsonAsync($"/api/users/{user.Id}/missing-stickers", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal(user.Id, body.GetProperty("userId").GetInt32());
        Assert.Equal(stickerId, body.GetProperty("stickerId").GetInt32());
        Assert.True(body.TryGetProperty("registeredAt", out _));

        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains($"/api/users/{user.Id}/missing-stickers", location, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Escenario 2: Agregar el mismo sticker dos veces → segunda vez retorna 409 Conflict.
    /// </summary>
    [Fact]
    public async Task US02_PostMissingSticker_Duplicate_ReturnsConflict()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var username = $"us02_dup_{uniqueSuffix}";
        var user = await RegisterUserAsync(username, "password123");
        var token = await LoginAsync(username, "password123");
        var stickerId = await GetFirstCatalogStickerIdAsync();

        var authenticatedClient = ClientWithToken(token);
        var dto = new PostMissingStickerRequestDTO { StickerId = stickerId };

        var first = await authenticatedClient.PostAsJsonAsync($"/api/users/{user.Id}/missing-stickers", dto);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await authenticatedClient.PostAsJsonAsync($"/api/users/{user.Id}/missing-stickers", dto);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    /// <summary>
    /// Escenario 3: StickerId inexistente en catálogo → 404 Not Found.
    /// </summary>
    [Fact]
    public async Task US02_PostMissingSticker_InvalidSticker_ReturnsNotFound()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var username = $"us02_inv_{uniqueSuffix}";
        var user = await RegisterUserAsync(username, "password123");
        var token = await LoginAsync(username, "password123");

        var authenticatedClient = ClientWithToken(token);
        var dto = new PostMissingStickerRequestDTO { StickerId = 999999 };

        var response = await authenticatedClient.PostAsJsonAsync($"/api/users/{user.Id}/missing-stickers", dto);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Escenario 4: GET después de agregar un faltante → 200 Ok con la lista que contiene el sticker.
    /// </summary>
    [Fact]
    public async Task US02_GetMissingStickers_ReturnsOk()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var username = $"us02_get_{uniqueSuffix}";
        var user = await RegisterUserAsync(username, "password123");
        var token = await LoginAsync(username, "password123");
        var stickerId = await GetFirstCatalogStickerIdAsync();

        var authenticatedClient = ClientWithToken(token);
        var dto = new PostMissingStickerRequestDTO { StickerId = stickerId };
        var postResponse = await authenticatedClient.PostAsJsonAsync($"/api/users/{user.Id}/missing-stickers", dto);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var getResponse = await authenticatedClient.GetAsync($"/api/users/{user.Id}/missing-stickers");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var list = await getResponse.Content.ReadFromJsonAsync<List<MissingSticker>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(list);
        Assert.NotEmpty(list);
        Assert.Contains(list, ms => ms.StickerId == stickerId && ms.UserId == user.Id);
    }

    /// <summary>
    /// Escenario 5: DELETE faltante → 204 No Content; GET posterior devuelve lista vacía.
    /// </summary>
    [Fact]
    public async Task US02_DeleteMissingSticker_ReturnsNoContent()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var username = $"us02_del_{uniqueSuffix}";
        var user = await RegisterUserAsync(username, "password123");
        var token = await LoginAsync(username, "password123");
        var stickerId = await GetFirstCatalogStickerIdAsync();

        var authenticatedClient = ClientWithToken(token);
        var dto = new PostMissingStickerRequestDTO { StickerId = stickerId };
        var postResponse = await authenticatedClient.PostAsJsonAsync($"/api/users/{user.Id}/missing-stickers", dto);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var deleteResponse = await authenticatedClient.DeleteAsync($"/api/users/{user.Id}/missing-stickers/{stickerId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await authenticatedClient.GetAsync($"/api/users/{user.Id}/missing-stickers");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var list = await getResponse.Content.ReadFromJsonAsync<List<MissingSticker>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(list);
        Assert.Empty(list);
    }

    /// <summary>
    /// Escenario 6: JWT de usuario A sobre userId de usuario B → 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task US02_PostMissingSticker_WithDifferentUserId_Returns403()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us02_a_{uniqueSuffix}", "password123");
        var userB = await RegisterUserAsync($"us02_b_{uniqueSuffix}", "password123");

        var tokenA = await LoginAsync($"us02_a_{uniqueSuffix}", "password123");
        var stickerId = await GetFirstCatalogStickerIdAsync();

        var authenticatedClientA = ClientWithToken(tokenA);
        var dto = new PostMissingStickerRequestDTO { StickerId = stickerId };

        var response = await authenticatedClientA.PostAsJsonAsync($"/api/users/{userB.Id}/missing-stickers", dto);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Escenario 7: Sin JWT → 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task US02_PostMissingSticker_WithoutToken_Returns401()
    {
        var anonymousClient = _factory.CreateClient();
        var stickerId = await GetFirstCatalogStickerIdAsync();

        var dto = new PostMissingStickerRequestDTO { StickerId = stickerId };
        var response = await anonymousClient.PostAsJsonAsync("/api/users/1/missing-stickers", dto);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
