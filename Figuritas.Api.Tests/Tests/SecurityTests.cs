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
/// Integration tests that verify ownership enforcement on sensitive endpoints.
/// These tests protect against IDOR regressions (SEC-001, SEC-002).
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class SecurityTests : IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SecurityTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.CleanMutableCollectionsAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<UserResponseDTO> RegisterUserAsync(string username, string password = "password123")
    {
        var dto = new { Username = username, Password = password };
        var response = await _client.PostAsJsonAsync("/api/auth/register", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserResponseDTO>(JsonOpts))!;
    }

    private async Task<string> LoginAsync(string username, string password = "password123")
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

    private async Task<int> GetFirstCatalogStickerIdAsync()
    {
        var response = await _client.GetAsync("/api/stickers?Page=1&PageSize=1");
        response.EnsureSuccessStatusCode();
        var stickers = await response.Content.ReadFromJsonAsync<List<Sticker>>(JsonOpts);
        return stickers![0].Id;
    }

    private async Task<UserStickerResponseDTO> PublishStickerAsync(HttpClient auth, int userId, int catalogStickerId)
    {
        var dto = new PostUserStickerRequestDTO
        {
            StickerId = catalogStickerId,
            Quantity = 2,
            CanBeDirectlyExchanged = true,
            CanBeAuctioned = false
        };
        var response = await auth.PostAsJsonAsync($"/api/users/{userId}/stickers", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserStickerResponseDTO>(JsonOpts))!;
    }

    // ─── SEC-001: IDOR on DELETE /api/users/{userId}/stickers/{stickerId} ───

    /// <summary>
    /// SEC-001-A: User A attempts to delete a sticker belonging to User B.
    /// Expected result: 403 Forbidden. The sticker must remain untouched.
    /// </summary>
    [Fact]
    public async Task SEC001_DeleteSticker_ByNonOwner_Returns403()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sec001a_a_{suffix}");
        var userB = await RegisterUserAsync($"sec001a_b_{suffix}");
        var tokenA = await LoginAsync($"sec001a_a_{suffix}");
        var tokenB = await LoginAsync($"sec001a_b_{suffix}");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickerId = await GetFirstCatalogStickerIdAsync();

        // User B publishes a sticker
        var stickerB = await PublishStickerAsync(clientB, userB.Id, catalogStickerId);

        // User A attempts to delete User B's sticker using B's userId in the path
        var deleteResponse = await clientA.DeleteAsync($"/api/users/{userB.Id}/stickers/{stickerB.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }

    /// <summary>
    /// SEC-001-B: User A attempts to delete User B's sticker by providing A's own userId in the path
    /// but the stickerId belongs to B. The ownership check must be enforced against the actual
    /// sticker record from persistence, not against the URL userId.
    /// Expected result: 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task SEC001_DeleteSticker_WithSpoofedUrlUserId_Returns403()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sec001b_a_{suffix}");
        var userB = await RegisterUserAsync($"sec001b_b_{suffix}");
        var tokenA = await LoginAsync($"sec001b_a_{suffix}");
        var tokenB = await LoginAsync($"sec001b_b_{suffix}");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickerId = await GetFirstCatalogStickerIdAsync();

        // User B publishes a sticker
        var stickerB = await PublishStickerAsync(clientB, userB.Id, catalogStickerId);

        // User A crafts the URL using A's own userId but references B's stickerId
        var deleteResponse = await clientA.DeleteAsync($"/api/users/{userA.Id}/stickers/{stickerB.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }

    /// <summary>
    /// SEC-001-C: User A successfully deletes their own sticker. Ownership check must not block
    /// legitimate owner operations.
    /// Expected result: 200 OK.
    /// </summary>
    [Fact]
    public async Task SEC001_DeleteSticker_ByOwner_Returns200()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sec001c_a_{suffix}");
        var tokenA = await LoginAsync($"sec001c_a_{suffix}");
        var clientA = ClientWithToken(tokenA);

        var catalogStickerId = await GetFirstCatalogStickerIdAsync();
        var stickerA = await PublishStickerAsync(clientA, userA.Id, catalogStickerId);

        var deleteResponse = await clientA.DeleteAsync($"/api/users/{userA.Id}/stickers/{stickerA.Id}");

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
    }

    // ─── SEC-002: IDOR on PATCH /api/users/{id} ─────────────────────────────

    /// <summary>
    /// SEC-002-A: User A attempts to modify User B's username.
    /// Expected result: 403 Forbidden. User B's account must remain unchanged.
    /// </summary>
    [Fact]
    public async Task SEC002_PatchUser_ByNonOwner_Returns403()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sec002a_a_{suffix}");
        var userB = await RegisterUserAsync($"sec002a_b_{suffix}");
        var tokenA = await LoginAsync($"sec002a_a_{suffix}");
        var clientA = ClientWithToken(tokenA);

        var patchDto = new { Username = "hacked_username" };

        var patchResponse = await clientA.PatchAsJsonAsync($"/api/users/{userB.Id}", patchDto);

        Assert.Equal(HttpStatusCode.Forbidden, patchResponse.StatusCode);
    }

    /// <summary>
    /// SEC-002-B: User A attempts to modify User B's password.
    /// Expected result: 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task SEC002_PatchUserPassword_ByNonOwner_Returns403()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sec002b_a_{suffix}");
        var userB = await RegisterUserAsync($"sec002b_b_{suffix}");
        var tokenA = await LoginAsync($"sec002b_a_{suffix}");
        var clientA = ClientWithToken(tokenA);

        var patchDto = new { Password = "hacked_password" };

        var patchResponse = await clientA.PatchAsJsonAsync($"/api/users/{userB.Id}", patchDto);

        Assert.Equal(HttpStatusCode.Forbidden, patchResponse.StatusCode);
    }

    /// <summary>
    /// SEC-002-C: User A successfully modifies their own username. Ownership check must not block
    /// legitimate owner operations.
    /// Expected result: 200 OK.
    /// </summary>
    [Fact]
    public async Task SEC002_PatchUser_ByOwner_Returns200()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sec002c_a_{suffix}");
        var tokenA = await LoginAsync($"sec002c_a_{suffix}");
        var clientA = ClientWithToken(tokenA);

        var patchDto = new { Username = $"sec002c_updated_{suffix}" };

        var patchResponse = await clientA.PatchAsJsonAsync($"/api/users/{userA.Id}", patchDto);

        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var updated = (await patchResponse.Content.ReadFromJsonAsync<UserResponseDTO>(JsonOpts))!;
        Assert.Equal($"sec002c_updated_{suffix}", updated.Username);
    }

    // ─── GET /api/users?username= coverage ──────────────────────────────────

    /// <summary>
    /// GET /api/users?username={existing} returns 200 OK with the matching user.
    /// Verifies that Id and Username are correct, and that sensitive fields
    /// (Role, HashedPassword) are not exposed in the response.
    /// </summary>
    [Fact]
    public async Task GetUserByUsername_ExistingUser_Returns200WithSafeFields()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var username = $"sec_gbu_a_{suffix}";
        var registered = await RegisterUserAsync(username);

        var response = await _client.GetAsync($"/api/users?username={Uri.EscapeDataString(username)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(registered.Id, body.GetProperty("id").GetInt32());
        Assert.Equal(username, body.GetProperty("username").GetString());

        // Sensitive fields must not be present in the response
        Assert.False(body.TryGetProperty("role", out _), "Response must not expose 'role'.");
        Assert.False(body.TryGetProperty("hashedPassword", out _), "Response must not expose 'hashedPassword'.");
        Assert.False(body.TryGetProperty("password", out _), "Response must not expose 'password'.");
    }

    /// <summary>
    /// GET /api/users?username={nonexistent} returns 404 Not Found.
    /// </summary>
    [Fact]
    public async Task GetUserByUsername_NonExistentUser_Returns404()
    {
        var response = await _client.GetAsync("/api/users?username=this_user_does_not_exist_xyz");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// GET /api/users without the username parameter returns 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task GetUserByUsername_MissingParameter_Returns400()
    {
        var response = await _client.GetAsync("/api/users?username=");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
