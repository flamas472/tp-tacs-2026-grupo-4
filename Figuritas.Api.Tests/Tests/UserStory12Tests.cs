using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Figuritas.Api.Repositories;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Enums;
using Figuritas.Shared.Model;
using Figuritas.Shared.Model.Intercambios;
using Figuritas.Shared.Model.Notificaciones;
using Figuritas.Shared.Model.Subastas;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Figuritas.Api.Tests;

[Collection(nameof(IntegrationTestCollection))]
public class UserStory12Tests : IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public UserStory12Tests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.CleanMutableCollectionsAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<UserResponseDTO> RegisterUserAsync(string username, string password = "Pass1234")
    {
        var dto = new { Username = username, Password = password };
        var response = await _client.PostAsJsonAsync("/api/auth/register", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserResponseDTO>(JsonOpts))!;
    }

    private async Task<string> LoginAsync(string username, string password = "Pass1234")
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

    /// <summary>
    /// Inserts a user with Admin role directly via IUserRepository.
    /// The public API hardcodes Role = UserRole.User, so we bypass it for test setup.
    /// </summary>
    private async Task<(User user, string token)> RegisterAdminAsync(string username, UserRole role = UserRole.Admin)
    {
        using var scope = _factory.Services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var user = new User
        {
            Username = username,
            HashedPassword = BCrypt.Net.BCrypt.HashPassword("pass123"),
            Role = role
        };
        userRepo.Add(user);

        var token = await LoginAsync(username, "pass123");
        return (user, token);
    }

    private async Task<List<Sticker>> GetCatalogStickersAsync()
    {
        var response = await _client.GetAsync("/api/stickers?Page=1&PageSize=5");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<Sticker>>(JsonOpts))!;
    }

    private async Task<UserStickerResponseDTO> PublishStickerAsync(HttpClient auth, int userId, int stickerId)
    {
        var dto = new PostUserStickerRequestDTO
        {
            StickerId = stickerId,
            Quantity = 2,
            CanBeDirectlyExchanged = true,
            CanBeAuctioned = false
        };
        var response = await auth.PostAsJsonAsync($"/api/users/{userId}/stickers", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserStickerResponseDTO>(JsonOpts))!;
    }

    /// <summary>
    /// Inserts ExchangeProposals directly via repository for analytics seeding.
    /// </summary>
    private void SeedProposals(int count, ExchangeProposalState state, int proponentId, int proposedId)
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IExchangeProposalRepository>();
        for (int i = 0; i < count; i++)
        {
            repo.Add(new ExchangeProposal
            {
                ProponentID = proponentId,
                ProposedID = proposedId,
                RequestedUserStickerId = 1,
                OfferedUserStickerIds = [1],
                State = state
            });
        }
    }

    /// <summary>
    /// Inserts Auctions directly via repository for analytics seeding.
    /// </summary>
    private void SeedAuctions(int count, AuctionStatus status, int auctioneerId, bool withWinner = false)
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAuctionRepository>();
        for (int i = 0; i < count; i++)
        {
            repo.Add(new Auction
            {
                AuctioneerId = auctioneerId,
                UserStickerId = 1,
                Status = status,
                EndsAt = DateTime.UtcNow.AddHours(1),
                BestCurrentOfferId = (status == AuctionStatus.Closed && withWinner) ? 99 : null
            });
        }
    }

    /// <summary>
    /// Inserts Notifications directly via repository for analytics seeding.
    /// </summary>
    private async Task SeedNotificationsAsync(int count, NotificationType type, int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        for (int i = 0; i < count; i++)
        {
            await repo.AddAsync(new Notification
            {
                UserId = userId,
                Type = type,
                Title = "Test",
                Message = "Test notification"
            });
        }
    }

    // ─── A: Authorization Tests ──────────────────────────────────────────────

    [Fact]
    public async Task A1_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/api/admin/analytics/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A2_RegularUser_Returns403()
    {
        var user = await RegisterUserAsync("us12_a2_user");
        var token = await LoginAsync("us12_a2_user");
        var auth = ClientWithToken(token);

        var response = await auth.GetAsync("/api/admin/analytics/summary");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A3_Admin_Returns200()
    {
        var (_, token) = await RegisterAdminAsync("us12_a3_admin", UserRole.Admin);
        var auth = ClientWithToken(token);

        var response = await auth.GetAsync("/api/admin/analytics/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A4_SuperAdmin_Returns200()
    {
        var (_, token) = await RegisterAdminAsync("us12_a4_superadmin", UserRole.SuperAdmin);
        var auth = ClientWithToken(token);

        var response = await auth.GetAsync("/api/admin/analytics/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ─── B: Metrics Correctness Tests ────────────────────────────────────────

    [Fact]
    public async Task B1_RegisteredUsersCount_IsCorrect()
    {
        // Register 3 regular users + 1 admin
        await RegisterUserAsync("us12_b1_u1");
        await RegisterUserAsync("us12_b1_u2");
        await RegisterUserAsync("us12_b1_u3");
        var (_, adminToken) = await RegisterAdminAsync("us12_b1_admin");

        var auth = ClientWithToken(adminToken);
        var response = await auth.GetAsync("/api/admin/analytics/summary");
        var summary = (await response.Content.ReadFromJsonAsync<PlatformSummaryResponseDTO>(JsonOpts))!;

        // 3 regular + 1 admin = 4 total
        Assert.Equal(4, summary.UserActivity.TotalRegisteredUsers);
    }

    [Fact]
    public async Task B2_ProposalCounts_AreCorrect()
    {
        var user1 = await RegisterUserAsync("us12_b2_u1");
        var user2 = await RegisterUserAsync("us12_b2_u2");
        var (_, adminToken) = await RegisterAdminAsync("us12_b2_admin");

        SeedProposals(3, ExchangeProposalState.Accepted, user1.Id, user2.Id);
        SeedProposals(2, ExchangeProposalState.Rejected, user1.Id, user2.Id);
        SeedProposals(1, ExchangeProposalState.Cancelled, user1.Id, user2.Id);

        var auth = ClientWithToken(adminToken);
        var response = await auth.GetAsync("/api/admin/analytics/summary");
        var summary = (await response.Content.ReadFromJsonAsync<PlatformSummaryResponseDTO>(JsonOpts))!;

        Assert.Equal(6, summary.ExchangeMetrics.TotalProposalsCreated);
        Assert.Equal(3, summary.ExchangeMetrics.AcceptedProposals);
        Assert.Equal(2, summary.ExchangeMetrics.RejectedProposals);
        Assert.Equal(1, summary.ExchangeMetrics.CancelledProposals);
        // 3/6 = 50%
        Assert.Equal(50.0, summary.ExchangeMetrics.AcceptanceRatePercent);
    }

    [Fact]
    public async Task B3_AuctionCounts_AreCorrect()
    {
        var user = await RegisterUserAsync("us12_b3_u1");
        var (_, adminToken) = await RegisterAdminAsync("us12_b3_admin");

        SeedAuctions(2, AuctionStatus.Active, user.Id);
        SeedAuctions(3, AuctionStatus.Closed, user.Id, withWinner: true);
        SeedAuctions(1, AuctionStatus.Closed, user.Id, withWinner: false);
        SeedAuctions(2, AuctionStatus.Cancelled, user.Id);

        var auth = ClientWithToken(adminToken);
        var response = await auth.GetAsync("/api/admin/analytics/summary");
        var summary = (await response.Content.ReadFromJsonAsync<PlatformSummaryResponseDTO>(JsonOpts))!;

        Assert.Equal(8, summary.AuctionVolume.TotalAuctionsCreated);
        Assert.Equal(2, summary.AuctionVolume.ActiveAuctions);
        Assert.Equal(3, summary.AuctionVolume.ClosedAuctionsWithWinner);
        Assert.Equal(1, summary.AuctionVolume.ClosedAuctionsWithoutWinner);
        Assert.Equal(2, summary.AuctionVolume.CancelledAuctions);
    }

    [Fact]
    public async Task B4_NotificationCounts_AreCorrect()
    {
        var user = await RegisterUserAsync("us12_b4_u1");
        var (_, adminToken) = await RegisterAdminAsync("us12_b4_admin");

        await SeedNotificationsAsync(4, NotificationType.NewProposal, user.Id);
        await SeedNotificationsAsync(2, NotificationType.AuctionEnding, user.Id);
        await SeedNotificationsAsync(3, NotificationType.MissingStickerAvailable, user.Id);

        var auth = ClientWithToken(adminToken);
        var response = await auth.GetAsync("/api/admin/analytics/summary");
        var summary = (await response.Content.ReadFromJsonAsync<PlatformSummaryResponseDTO>(JsonOpts))!;

        Assert.Equal(9, summary.NotificationTraffic.TotalNotificationsSent);
        Assert.Equal(4, summary.NotificationTraffic.NewProposalNotifications);
        Assert.Equal(2, summary.NotificationTraffic.AuctionEndingNotifications);
        Assert.Equal(3, summary.NotificationTraffic.MissingStickerAvailableNotifications);
    }

    [Fact]
    public async Task B5_Admin_CannotCreateAdmin_Returns403()
    {
        var (_, adminToken) = await RegisterAdminAsync("us12_b5_admin");
        var auth = ClientWithToken(adminToken);

        var dto = new CreateAdminRequestDTO { Username = "us12_b5_new_admin", Password = "Admin1234" };
        var response = await auth.PostAsJsonAsync("/api/admin/admins", dto);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task B6_SuperAdmin_CanCreateAdmin_Returns201()
    {
        var (_, superAdminToken) = await RegisterAdminAsync("us12_b6_superadmin", UserRole.SuperAdmin);
        var auth = ClientWithToken(superAdminToken);

        var dto = new CreateAdminRequestDTO { Username = "us12_b6_new_admin", Password = "Admin1234" };
        var response = await auth.PostAsJsonAsync("/api/admin/admins", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<AdminUserResponseDTO>(JsonOpts))!;
        Assert.Equal("us12_b6_new_admin", created.Username);
        Assert.Equal(UserRole.Admin, created.Role);
    }

    [Fact]
    public async Task B7_SuperAdmin_CanPromoteAdminToSuperAdmin_PersistsCorrectly()
    {
        var (_, superAdminToken) = await RegisterAdminAsync("us12_b7_superadmin", UserRole.SuperAdmin);
        var superAuth = ClientWithToken(superAdminToken);

        // Create a regular admin
        var createDto = new CreateAdminRequestDTO { Username = "us12_b7_admin", Password = "Admin1234" };
        var createResp = await superAuth.PostAsJsonAsync("/api/admin/admins", createDto);
        createResp.EnsureSuccessStatusCode();
        var adminUser = (await createResp.Content.ReadFromJsonAsync<AdminUserResponseDTO>(JsonOpts))!;

        // Promote to SuperAdmin
        var patchDto = new PatchAdminRoleRequestDTO { Role = UserRole.SuperAdmin };
        var patchResp = await superAuth.PatchAsJsonAsync($"/api/admin/admins/{adminUser.Id}/role", patchDto);
        Assert.Equal(HttpStatusCode.OK, patchResp.StatusCode);

        var updated = (await patchResp.Content.ReadFromJsonAsync<AdminUserResponseDTO>(JsonOpts))!;
        Assert.Equal(UserRole.SuperAdmin, updated.Role);

        // Verify via GET /admins list
        var listResp = await superAuth.GetAsync("/api/admin/admins");
        var admins = (await listResp.Content.ReadFromJsonAsync<List<AdminUserResponseDTO>>(JsonOpts))!;
        var promotedAdmin = admins.FirstOrDefault(a => a.Id == adminUser.Id);
        Assert.NotNull(promotedAdmin);
        Assert.Equal(UserRole.SuperAdmin, promotedAdmin.Role);
    }
}
