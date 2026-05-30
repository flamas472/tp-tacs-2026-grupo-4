using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Figuritas.Shared.DTO;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.Model;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Figuritas.Api.Tests;

/// <summary>
/// Integration tests for User Stories of Delivery 1.
/// Uses WebApplicationFactory to run the API in memory and test REST design.
/// Requires a running MongoDB instance (same as the app).
/// </summary>
public class UserStoriesIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public UserStoriesIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<User> RegisterUserAsync(string username, string password)
    {
        var dto = new { Username = username, Password = password };
        var response = await _client.PostAsJsonAsync("/api/users", dto);
        response.EnsureSuccessStatusCode();
        var user = await response.Content.ReadFromJsonAsync<User>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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

    // ─── US01 Tests ──────────────────────────────────────────────────────────

    /// <summary>
    /// Escenario 1: Publicación exitosa → 201 Created con Active = true.
    /// </summary>
    [Fact]
    public async Task US01_PostUserSticker_ReturnsCreated_WithActiveTrue()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var username = $"us01_user_{uniqueSuffix}";
        var user = await RegisterUserAsync(username, "password123");
        var token = await LoginAsync(username, "password123");

        var stickerId = await GetFirstCatalogStickerIdAsync();

        var dto = new PostUserStickerRequestDTO
        {
            StickerId = stickerId,
            Quantity = 2,
            CanBeDirectlyExchanged = true,
            CanBeAuctioned = false
        };

        var authenticatedClient = ClientWithToken(token);
        var response = await authenticatedClient.PostAsJsonAsync($"/api/users/{user.Id}/stickers", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<UserSticker>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(created);
        Assert.True(created!.Active);
        Assert.Equal(2, created.Quantity);
        Assert.True(created.CanBeDirectlyExchanged);
        Assert.False(created.CanBeAuctioned);
        Assert.True(created.Id > 0);
    }

    /// <summary>
    /// Escenario 1b: GET del sticker recién creado devuelve el mismo recurso.
    /// </summary>
    [Fact]
    public async Task US01_GetUserStickerById_ReturnsOk_AfterPost()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var username = $"us01_get_{uniqueSuffix}";
        var user = await RegisterUserAsync(username, "password123");
        var token = await LoginAsync(username, "password123");
        var stickerId = await GetFirstCatalogStickerIdAsync();

        var authenticatedClient = ClientWithToken(token);
        var postDto = new PostUserStickerRequestDTO
        {
            StickerId = stickerId,
            Quantity = 1,
            CanBeDirectlyExchanged = true,
            CanBeAuctioned = false
        };
        var postResponse = await authenticatedClient.PostAsJsonAsync($"/api/users/{user.Id}/stickers", postDto);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var locationHeader = postResponse.Headers.Location?.ToString();
        Assert.NotNull(locationHeader);

        var getResponse = await authenticatedClient.GetAsync(locationHeader);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await getResponse.Content.ReadFromJsonAsync<UserSticker>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(fetched);
        Assert.Equal(user.Id, fetched!.UserId);
    }

    /// <summary>
    /// Escenario 2: Quantity = 0 → 400 BadRequest (validación de modelo).
    /// </summary>
    [Fact]
    public async Task US01_PostUserSticker_WithZeroQuantity_Returns400()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var username = $"us01_qty_{uniqueSuffix}";
        var user = await RegisterUserAsync(username, "password123");
        var token = await LoginAsync(username, "password123");
        var stickerId = await GetFirstCatalogStickerIdAsync();

        var authenticatedClient = ClientWithToken(token);
        var dto = new { StickerId = stickerId, Quantity = 0, CanBeDirectlyExchanged = true, CanBeAuctioned = false };
        var response = await authenticatedClient.PostAsJsonAsync($"/api/users/{user.Id}/stickers", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Escenario 3: JWT de usuario A usado para publicar en cuenta de usuario B → 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task US01_PostUserSticker_WithDifferentUserId_Returns403()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us01_a_{uniqueSuffix}", "password123");
        var userB = await RegisterUserAsync($"us01_b_{uniqueSuffix}", "password123");

        var tokenA = await LoginAsync($"us01_a_{uniqueSuffix}", "password123");
        var stickerId = await GetFirstCatalogStickerIdAsync();

        var authenticatedClient = ClientWithToken(tokenA);
        var dto = new PostUserStickerRequestDTO
        {
            StickerId = stickerId,
            Quantity = 1,
            CanBeDirectlyExchanged = true,
            CanBeAuctioned = false
        };

        // User A intenta publicar en la cuenta de User B
        var response = await authenticatedClient.PostAsJsonAsync($"/api/users/{userB.Id}/stickers", dto);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Escenario 4: StickerId inexistente en catálogo → 404 Not Found.
    /// </summary>
    [Fact]
    public async Task US01_PostUserSticker_WithInvalidStickerId_Returns404()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var username = $"us01_inv_{uniqueSuffix}";
        var user = await RegisterUserAsync(username, "password123");
        var token = await LoginAsync(username, "password123");

        var authenticatedClient = ClientWithToken(token);
        var dto = new PostUserStickerRequestDTO
        {
            StickerId = 999999,
            Quantity = 1,
            CanBeDirectlyExchanged = true,
            CanBeAuctioned = false
        };
        var response = await authenticatedClient.PostAsJsonAsync($"/api/users/{user.Id}/stickers", dto);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Escenario 5: Publicar el mismo sticker dos veces → segunda llamada devuelve 409 Conflict.
    /// </summary>
    [Fact]
    public async Task US01_PostUserSticker_Duplicate_Returns409()
    {
        var uniqueSuffix = DateTime.UtcNow.Ticks.ToString();
        var username = $"us01_dup_{uniqueSuffix}";
        var user = await RegisterUserAsync(username, "password123");
        var token = await LoginAsync(username, "password123");
        var stickerId = await GetFirstCatalogStickerIdAsync();

        var authenticatedClient = ClientWithToken(token);
        var dto = new PostUserStickerRequestDTO
        {
            StickerId = stickerId,
            Quantity = 1,
            CanBeDirectlyExchanged = true,
            CanBeAuctioned = false
        };

        var first = await authenticatedClient.PostAsJsonAsync($"/api/users/{user.Id}/stickers", dto);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await authenticatedClient.PostAsJsonAsync($"/api/users/{user.Id}/stickers", dto);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    /// <summary>
    /// Sin token → 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task US01_PostUserSticker_WithoutToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var stickerId = await GetFirstCatalogStickerIdAsync();

        var dto = new PostUserStickerRequestDTO
        {
            StickerId = stickerId,
            Quantity = 1,
            CanBeDirectlyExchanged = true,
            CanBeAuctioned = false
        };
        var response = await _client.PostAsJsonAsync("/api/users/1/stickers", dto);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
