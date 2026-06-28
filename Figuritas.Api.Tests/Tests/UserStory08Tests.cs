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
public class UserStory08Tests : IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public UserStory08Tests(IntegrationTestFactory factory)
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
        return (await response.Content.ReadFromJsonAsync<UserResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }))!;
    }

    private async Task<string> LoginAsync(string username, string password)
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
        bool canBeDirectlyExchanged = true)
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
        return (await response.Content.ReadFromJsonAsync<UserStickerResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }))!;
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
        return (await response.Content.ReadFromJsonAsync<AuctionResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }))!;
    }

    private async Task<ExchangeProposalResponseDTO> CreateProposalAsync(
        HttpClient authenticatedClient,
        int proposedUserId,
        int requestedUserStickerId,
        List<int> offeredUserStickerIds)
    {
        var dto = new PostExchangeProposalRequestDTO
        {
            ProposedUserId = proposedUserId,
            RequestedUserStickerId = requestedUserStickerId,
            OfferedUserStickerIds = offeredUserStickerIds
        };
        var response = await authenticatedClient.PostAsJsonAsync("/api/exchange-proposals", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExchangeProposalResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }))!;
    }

    // ─── US08 Tests ──────────────────────────────────────────────────────────

    /// <summary>
    /// Test 1: GET /api/dashboard/stickers con token válido devuelve solo los stickers del caller.
    /// </summary>
    [Fact]
    public async Task GetMyStickers_WithValidToken_ReturnsOnlyCallerStickers()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us08_stk_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us08_stk_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us08_stk_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us08_stk_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        await PublishUserStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id);

        var response = await clientA.GetAsync("/api/dashboard/stickers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<MyPublishedStickerResponseDTO>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.NotEmpty(result!);
        Assert.All(result!, s => Assert.True(s.StickerId > 0));
        // All stickers returned belong to userA — none to userB
        // We verify the count: A published 1 sticker
        Assert.Single(result!);
    }

    /// <summary>
    /// Test 2: GET /api/dashboard/stickers sin token → HTTP 401.
    /// </summary>
    [Fact]
    public async Task GetMyStickers_WithoutToken_Returns401()
    {
        var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.GetAsync("/api/dashboard/stickers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Test 3: Paginación de stickers — Page=1 y Page=2 no se solapan.
    /// </summary>
    [Fact]
    public async Task GetMyStickers_Pagination_Page2_ReturnsCorrectSubset()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var user = await RegisterUserAsync($"us08_pg_stk_{suffix}", "Password123");
        var token = await LoginAsync($"us08_pg_stk_{suffix}", "Password123");
        var client = ClientWithToken(token);

        // Publish 3 stickers with pageSize=2 so page1 has 2 and page2 has 1
        var catalogStickers = await GetCatalogStickersAsync(1, 3);
        Assert.True(catalogStickers.Count >= 3, "Need at least 3 catalog stickers for pagination test.");

        await PublishUserStickerAsync(client, user.Id, catalogStickers[0].Id);
        await PublishUserStickerAsync(client, user.Id, catalogStickers[1].Id);
        await PublishUserStickerAsync(client, user.Id, catalogStickers[2].Id);

        var page1Response = await client.GetAsync("/api/dashboard/stickers?Page=1&PageSize=2");
        var page2Response = await client.GetAsync("/api/dashboard/stickers?Page=2&PageSize=2");

        Assert.Equal(HttpStatusCode.OK, page1Response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, page2Response.StatusCode);

        var page1 = await page1Response.Content.ReadFromJsonAsync<List<MyPublishedStickerResponseDTO>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var page2 = await page2Response.Content.ReadFromJsonAsync<List<MyPublishedStickerResponseDTO>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(page1);
        Assert.NotNull(page2);
        Assert.Equal(2, page1!.Count);
        Assert.Single(page2!);

        // No overlap
        var page1Ids = page1!.Select(s => s.StickerId).ToHashSet();
        var page2Ids = page2!.Select(s => s.StickerId).ToHashSet();
        Assert.Empty(page1Ids.Intersect(page2Ids));
    }

    /// <summary>
    /// Test 4: Page=0 o PageSize=0 → HTTP 400.
    /// </summary>
    [Fact]
    public async Task GetMyStickers_InvalidPageSize_Returns400()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var user = await RegisterUserAsync($"us08_inv_pg_{suffix}", "Password123");
        var token = await LoginAsync($"us08_inv_pg_{suffix}", "Password123");
        var client = ClientWithToken(token);

        var responsePageZero = await client.GetAsync("/api/dashboard/stickers?Page=0&PageSize=20");
        var responsePageSizeZero = await client.GetAsync("/api/dashboard/stickers?Page=1&PageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, responsePageZero.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, responsePageSizeZero.StatusCode);
    }

    /// <summary>
    /// Test 5: GET /api/dashboard/proposals/sent con token válido devuelve solo las propuestas enviadas por el caller.
    /// </summary>
    [Fact]
    public async Task GetMySentProposals_WithValidToken_ReturnsOnlyCallerProposals()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us08_sent_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us08_sent_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us08_sent_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us08_sent_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerB = await PublishUserStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id);

        // A→B proposal
        await CreateProposalAsync(clientA, userB.Id, stickerB.Id, new List<int> { stickerA.Id });

        // B→A proposal (should NOT appear in A's sent list)
        await CreateProposalAsync(clientB, userA.Id, stickerA.Id, new List<int> { stickerB.Id });

        var response = await clientA.GetAsync("/api/dashboard/proposals/sent");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<ExchangeProposalResponseDTO>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.All(result!, p => Assert.Equal(userA.Id, p.ProponentUserId));
    }

    /// <summary>
    /// Test 6: Sin filtro de estado → aparecen propuestas de todos los estados (accepted, rejected, pending).
    /// </summary>
    [Fact]
    public async Task GetMySentProposals_IncludesAllStates_WhenNoStateFilter()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us08_allst_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us08_allst_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us08_allst_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us08_allst_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 3);
        Assert.True(catalogStickers.Count >= 3, "Need at least 3 catalog stickers.");

        var stickerA1 = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id, quantity: 3);
        var stickerA2 = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[1].Id, quantity: 3);
        var stickerA3 = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[2].Id, quantity: 3);
        var stickerB = await PublishUserStickerAsync(clientB, userB.Id, catalogStickers[0].Id, quantity: 3);

        // Create 3 proposals from A→B
        var p1 = await CreateProposalAsync(clientA, userB.Id, stickerB.Id, new List<int> { stickerA1.Id });
        var p2 = await CreateProposalAsync(clientA, userB.Id, stickerB.Id, new List<int> { stickerA2.Id });
        var p3 = await CreateProposalAsync(clientA, userB.Id, stickerB.Id, new List<int> { stickerA3.Id });

        // B accepts p1
        var acceptResponse = await clientB.PostAsync($"/api/exchange-proposals/{p1.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        // B rejects p2
        var rejectResponse = await clientB.PostAsync($"/api/exchange-proposals/{p2.Id}/reject", null);
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);

        // p3 stays Pending

        // A queries sent proposals without state filter — all 3 should appear
        var response = await clientA.GetAsync("/api/dashboard/proposals/sent");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<List<ExchangeProposalResponseDTO>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);

        var ids = result!.Select(p => p.Id).ToHashSet();
        Assert.Contains(p1.Id, ids);
        Assert.Contains(p2.Id, ids);
        Assert.Contains(p3.Id, ids);
    }

    /// <summary>
    /// Test 7: GET /api/dashboard/proposals/received devuelve solo las propuestas recibidas por el caller.
    /// </summary>
    [Fact]
    public async Task GetMyReceivedProposals_WithValidToken_ReturnsOnlyReceivedByCallerProposals()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us08_recv_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us08_recv_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us08_recv_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us08_recv_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerB = await PublishUserStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id);

        // B→A proposal (A receives it)
        await CreateProposalAsync(clientB, userA.Id, stickerA.Id, new List<int> { stickerB.Id });

        // A→B proposal (A should NOT see this in received)
        await CreateProposalAsync(clientA, userB.Id, stickerB.Id, new List<int> { stickerA.Id });

        var response = await clientA.GetAsync("/api/dashboard/proposals/received");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<ExchangeProposalResponseDTO>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.All(result!, p => Assert.Equal(userA.Id, p.ProposedUserId));
    }

    /// <summary>
    /// Test 8: GET /api/dashboard/proposals/received sin token → HTTP 401.
    /// </summary>
    [Fact]
    public async Task GetMyReceivedProposals_WithoutToken_Returns401()
    {
        var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.GetAsync("/api/dashboard/proposals/received");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Test 9: GET /api/dashboard/auctions con token válido devuelve solo las subastas del caller.
    /// </summary>
    [Fact]
    public async Task GetMyActiveAuctions_WithValidToken_ReturnsOnlyCallerActiveAuctions()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us08_auct_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us08_auct_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us08_auct_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us08_auct_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerB = await PublishUserStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id);

        var auctionA = await CreateAuctionAsync(clientA, stickerA.Id);
        var auctionB = await CreateAuctionAsync(clientB, stickerB.Id);

        var response = await clientA.GetAsync("/api/dashboard/auctions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<AuctionResponseDTO>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.NotEmpty(result!);
        Assert.All(result!, a => Assert.Equal(userA.Id, a.AuctioneerId));
    }

    /// <summary>
    /// Test 10: La respuesta de subastas de A no contiene ningún Id de subasta de B.
    /// </summary>
    [Fact]
    public async Task GetMyActiveAuctions_ExcludesOtherUsersAuctions()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us08_excl_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us08_excl_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us08_excl_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us08_excl_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerB = await PublishUserStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id);

        await CreateAuctionAsync(clientA, stickerA.Id);
        var auctionB = await CreateAuctionAsync(clientB, stickerB.Id);

        var response = await clientA.GetAsync("/api/dashboard/auctions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<List<AuctionResponseDTO>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);

        var returnedIds = result!.Select(a => a.Id).ToHashSet();
        Assert.DoesNotContain(auctionB.Id, returnedIds);
    }

    /// <summary>
    /// Test 11: GET /api/dashboard/auctions sin token → HTTP 401.
    /// </summary>
    [Fact]
    public async Task GetMyActiveAuctions_WithoutToken_Returns401()
    {
        var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.GetAsync("/api/dashboard/auctions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Test 12: Page=0 → HTTP 400.
    /// </summary>
    [Fact]
    public async Task GetMyActiveAuctions_Pagination_Returns400OnInvalidRange()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var user = await RegisterUserAsync($"us08_inv_auct_{suffix}", "Password123");
        var token = await LoginAsync($"us08_inv_auct_{suffix}", "Password123");
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/dashboard/auctions?Page=0&PageSize=20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Test 13: GET /api/dashboard/proposals/sent con State=INVALID_VALUE → HTTP 400.
    /// El Model Binder rechaza el string inválido porque State es ExchangeProposalState? (enum).
    /// </summary>
    [Fact]
    public async Task GetMyProposals_InvalidStateFilter_Returns400()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var user = await RegisterUserAsync($"us08_invstate_{suffix}", "Password123");
        var token = await LoginAsync($"us08_invstate_{suffix}", "Password123");
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/dashboard/proposals/sent?State=INVALID_VALUE");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Test 14: GET /api/users/{B.Id}/stickers was the legacy deprecated endpoint.
    /// The GET verb has been removed — the route only exists for POST, so the framework
    /// returns 405 Method Not Allowed. Sticker inventory is now exclusively available
    /// via GET /api/dashboard/stickers.
    /// </summary>
    [Fact]
    public async Task GetUserStickers_LegacyEndpoint_IsRemoved_Returns405()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us08_legA_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us08_legB_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us08_legA_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);

        // GET /api/users/{userId}/stickers is not registered — only POST exists on this route,
        // so the framework responds with 405 Method Not Allowed.
        var response = await clientA.GetAsync($"/api/users/{userB.Id}/stickers");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    // ─── IAsyncLifetime ──────────────────────────────────────────────────────

    public async Task InitializeAsync() => await _factory.CleanMutableCollectionsAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
