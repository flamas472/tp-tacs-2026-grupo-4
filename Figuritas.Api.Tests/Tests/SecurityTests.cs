using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model;
using Microsoft.Extensions.DependencyInjection;
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

        var patchDto = new { Password = "Hacked_P4ssword" };

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

    // ─── AUTH-001: Login failure must return 401, never 200 ─────────────────

    /// <summary>
    /// AUTH-001-A: Login with a valid username but wrong password returns 401.
    /// Regression guard: the old endpoint used PostUserDTO as the request body,
    /// which applied password-complexity model validation. A mismatch between
    /// registration-time and login-time validation could cause a 400 to be
    /// returned instead of the expected 401.
    /// </summary>
    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var username = $"auth001a_{suffix}";
        await RegisterUserAsync(username);

        var dto = new { Username = username, Password = "WrongPassword999" };
        var response = await _client.PostAsJsonAsync("/api/auth/login", dto);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// AUTH-001-B: Login with a username that does not exist returns 401.
    /// </summary>
    [Fact]
    public async Task Login_WithNonExistentUser_Returns401()
    {
        var dto = new { Username = "user_that_does_not_exist_xyz", Password = "AnyPassword123" };
        var response = await _client.PostAsJsonAsync("/api/auth/login", dto);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// AUTH-001-C: A banned user with correct credentials must not receive a token.
    /// Regression guard: the old ValidateCredentials did not check user.Banned, so
    /// banned users could still obtain a valid JWT.
    /// The ban is applied via direct repository access because CleanMutableCollectionsAsync
    /// clears the Users collection (including the seeded SuperAdmin) before every test.
    /// </summary>
    [Fact]
    public async Task Login_WithBannedUser_Returns401()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var username = $"auth001c_{suffix}";
        const string password = "Password123";
        var registered = await RegisterUserAsync(username, password);

        // Set the Banned flag directly on the persisted user.
        using (var scope = _factory.Services.CreateScope())
        {
            var userRepo = scope.ServiceProvider.GetRequiredService<Figuritas.Api.Repositories.IUserRepository>();
            var user = userRepo.GetById(registered.Id)!;
            user.Banned = true;
            userRepo.Update(user);
        }

        var dto = new { Username = username, Password = password };
        var response = await _client.PostAsJsonAsync("/api/auth/login", dto);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─── SEC-004: Active JWT must be invalidated when the user is banned ─────

    /// <summary>
    /// SEC-004: A valid JWT held by a user must be rejected (401) as soon as the
    /// user is banned, without waiting for the token to expire naturally.
    ///
    /// The mechanism:
    ///   - AuthService.GenerateToken embeds an explicit "iat" claim (Unix timestamp).
    ///   - BanUserAsync sets user.TokenValidFrom = DateTime.UtcNow and persists it.
    ///   - OnTokenValidated in Program.cs compares iat vs TokenValidFrom and calls
    ///     context.Fail() when the token predates the security event.
    ///
    /// This test simulates the ban via direct repository mutation to avoid the need
    /// for a seeded SuperAdmin (which is wiped by CleanMutableCollectionsAsync).
    /// </summary>
    [Fact]
    public async Task SEC004_BannedUser_ExistingJwtIsRejectedImmediately()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var username = $"sec004_{suffix}";
        const string password = "Password123";

        var registered = await RegisterUserAsync(username, password);
        var token = await LoginAsync(username, password);
        var authedClient = ClientWithToken(token);

        // Verify the token is accepted on a protected endpoint before the ban.
        // GET /api/users/{userId}/missing-stickers is [Authorize] at the controller level
        // and does NOT carry [AllowAnonymous], so authentication failure returns 401.
        var preban = await authedClient.GetAsync($"/api/users/{registered.Id}/missing-stickers");
        Assert.Equal(HttpStatusCode.OK, preban.StatusCode);

        // Apply the ban: mark the user as banned and advance TokenValidFrom past
        // the issued-at time of the token already held by authedClient.
        using (var scope = _factory.Services.CreateScope())
        {
            var userRepo = scope.ServiceProvider
                .GetRequiredService<Figuritas.Api.Repositories.IUserRepository>();
            var user = userRepo.GetById(registered.Id)!;
            user.Banned = true;
            user.TokenValidFrom = DateTime.UtcNow;
            userRepo.Update(user);
        }

        // The previously valid JWT must now be rejected because OnTokenValidated
        // detects user.Banned == true and calls context.Fail(), which the [Authorize]
        // attribute converts into HTTP 401.
        var postban = await authedClient.GetAsync($"/api/users/{registered.Id}/missing-stickers");
        Assert.Equal(HttpStatusCode.Unauthorized, postban.StatusCode);
    }
}
