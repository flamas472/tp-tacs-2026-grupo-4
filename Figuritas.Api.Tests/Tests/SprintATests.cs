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
/// Sprint A integration tests covering:
/// - BLOQUE 1: GET /api/auctions authentication and own-auction exclusion.
/// - BLOQUE 2: Duplicate sticker ID validation for auction offers and exchange proposals.
/// - BLOQUE 3/4: UserSelectedBestOfferId presence in auction responses.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class SprintATests : IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SprintATests(IntegrationTestFactory factory)
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

    private async Task<List<Sticker>> GetCatalogStickersAsync(int page, int pageSize)
    {
        var response = await _client.GetAsync($"/api/stickers?Page={page}&PageSize={pageSize}");
        response.EnsureSuccessStatusCode();
        var stickers = await response.Content.ReadFromJsonAsync<List<Sticker>>(JsonOpts);
        Assert.NotNull(stickers);
        Assert.NotEmpty(stickers);
        return stickers!;
    }

    private async Task<UserStickerResponseDTO> PublishUserStickerAsync(
        HttpClient auth,
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
        var response = await auth.PostAsJsonAsync($"/api/users/{userId}/stickers", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserStickerResponseDTO>(JsonOpts))!;
    }

    private async Task<AuctionResponseDTO> CreateAuctionAsync(
        HttpClient auth,
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
        var response = await auth.PostAsJsonAsync("/api/auctions", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuctionResponseDTO>(JsonOpts))!;
    }

    // ─── BLOQUE 1: GET /api/auctions auth & own-auction exclusion ───────────

    /// <summary>
    /// SA-01: GET /api/auctions without JWT returns 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task GetAuctions_WithoutJwtToken_Returns401Unauthorized()
    {
        var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.GetAsync("/api/auctions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// SA-02: Authenticated user creating an auction does not see it in GET /api/auctions.
    /// Another user listing the same endpoint does see it.
    /// </summary>
    [Fact]
    public async Task GetAuctions_AuthenticatedUser_DoesNotSeeOwnAuctions()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sa02_a_{suffix}");
        var userB = await RegisterUserAsync($"sa02_b_{suffix}");
        var tokenA = await LoginAsync($"sa02_a_{suffix}");
        var tokenB = await LoginAsync($"sa02_b_{suffix}");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickerId = await GetFirstCatalogStickerIdAsync();
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickerId);
        var auction = await CreateAuctionAsync(clientA, stickerA.Id);

        // UserA lists auctions — their own auction must NOT appear
        var responseA = await clientA.GetAsync("/api/auctions?page=1&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
        var auctionsForA = await responseA.Content.ReadFromJsonAsync<List<AuctionResponseDTO>>(JsonOpts);
        Assert.NotNull(auctionsForA);
        Assert.DoesNotContain(auctionsForA!, a => a.Id == auction.Id);

        // UserB lists auctions — UserA's auction MUST appear
        var responseB = await clientB.GetAsync("/api/auctions?page=1&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);
        var auctionsForB = await responseB.Content.ReadFromJsonAsync<List<AuctionResponseDTO>>(JsonOpts);
        Assert.NotNull(auctionsForB);
        Assert.Contains(auctionsForB!, a => a.Id == auction.Id);
    }

    /// <summary>
    /// SA-03: Pagination parameters are preserved when filtering by authenticated user.
    /// Verifies backward compatibility: response is still a list (not null/error).
    /// </summary>
    [Fact]
    public async Task GetAuctions_WithPaginationParams_ReturnsOkWithList()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sa03_a_{suffix}");
        var tokenA = await LoginAsync($"sa03_a_{suffix}");
        var clientA = ClientWithToken(tokenA);

        var response = await clientA.GetAsync("/api/auctions?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auctions = await response.Content.ReadFromJsonAsync<List<AuctionResponseDTO>>(JsonOpts);
        Assert.NotNull(auctions);
    }

    // ─── BLOQUE 2: Duplicate sticker ID validation — auction offers ──────────

    /// <summary>
    /// SA-04: Creating an auction offer with duplicate sticker IDs returns 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task CreateOffer_WithDuplicateStickerIds_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sa04_a_{suffix}");
        var userB = await RegisterUserAsync($"sa04_b_{suffix}");
        var tokenA = await LoginAsync($"sa04_a_{suffix}");
        var tokenB = await LoginAsync($"sa04_b_{suffix}");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerB = await PublishUserStickerAsync(clientB, userB.Id,
            catalogStickers[1 % catalogStickers.Count].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientA, stickerA.Id);

        // Duplicate: stickerB.Id appears twice
        var offerDto = new PostAuctionOfferRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerB.Id, stickerB.Id }
        };

        var response = await clientB.PostAsJsonAsync($"/api/auctions/{auction.Id}/offers", offerDto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// SA-05: Creating an auction offer with duplicate IDs among three entries returns 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task CreateOffer_WithThreeDuplicateStickerIds_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sa05_a_{suffix}");
        var userB = await RegisterUserAsync($"sa05_b_{suffix}");
        var tokenA = await LoginAsync($"sa05_a_{suffix}");
        var tokenB = await LoginAsync($"sa05_b_{suffix}");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerB = await PublishUserStickerAsync(clientB, userB.Id,
            catalogStickers[1 % catalogStickers.Count].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientA, stickerA.Id);

        // [B, B, B] — all three are the same ID
        var offerDto = new PostAuctionOfferRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerB.Id, stickerB.Id, stickerB.Id }
        };

        var response = await clientB.PostAsJsonAsync($"/api/auctions/{auction.Id}/offers", offerDto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// SA-06 (regression): Creating an auction offer with distinct sticker IDs succeeds (201 Created).
    /// Ensures the duplicate guard does not block legitimate offers.
    /// </summary>
    [Fact]
    public async Task CreateOffer_WithDistinctStickerIds_Returns201Created()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sa06_a_{suffix}");
        var userB = await RegisterUserAsync($"sa06_b_{suffix}");
        var tokenA = await LoginAsync($"sa06_a_{suffix}");
        var tokenB = await LoginAsync($"sa06_b_{suffix}");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerB = await PublishUserStickerAsync(clientB, userB.Id,
            catalogStickers[1 % catalogStickers.Count].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientA, stickerA.Id);

        // Single unique ID — must succeed
        var offerDto = new PostAuctionOfferRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerB.Id }
        };

        var response = await clientB.PostAsJsonAsync($"/api/auctions/{auction.Id}/offers", offerDto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ─── BLOQUE 2: Duplicate sticker ID validation — exchange proposals ──────

    /// <summary>
    /// SA-07: Creating an exchange proposal with duplicate offered sticker IDs returns 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task CreateExchangeProposal_WithDuplicateStickerIds_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sa07_a_{suffix}");
        var userB = await RegisterUserAsync($"sa07_b_{suffix}");
        var tokenA = await LoginAsync($"sa07_a_{suffix}");
        var tokenB = await LoginAsync($"sa07_b_{suffix}");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB = await PublishUserStickerAsync(clientB, userB.Id,
            catalogStickers[1 % catalogStickers.Count].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);

        // UserA proposes to UserB, offering stickerA.Id twice
        var proposalDto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerA.Id, stickerA.Id },
            RequestedUserStickerId = stickerB.Id,
            ProposedUserId = userB.Id
        };

        var response = await clientA.PostAsJsonAsync("/api/exchange-proposals", proposalDto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// SA-08 (regression): Creating an exchange proposal with distinct sticker IDs succeeds (201 Created).
    /// </summary>
    [Fact]
    public async Task CreateExchangeProposal_WithDistinctStickerIds_Returns201Created()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sa08_a_{suffix}");
        var userB = await RegisterUserAsync($"sa08_b_{suffix}");
        var tokenA = await LoginAsync($"sa08_a_{suffix}");
        var tokenB = await LoginAsync($"sa08_b_{suffix}");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickers[0].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB = await PublishUserStickerAsync(clientB, userB.Id,
            catalogStickers[1 % catalogStickers.Count].Id,
            canBeAuctioned: false, canBeDirectlyExchanged: true);

        var proposalDto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerA.Id },
            RequestedUserStickerId = stickerB.Id,
            ProposedUserId = userB.Id
        };

        var response = await clientA.PostAsJsonAsync("/api/exchange-proposals", proposalDto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ─── BLOQUE 3/4: UserSelectedBestOfferId in responses ───────────────────

    /// <summary>
    /// SA-09: Newly created auction has UserSelectedBestOfferId == null in GET /api/auctions/{id}.
    /// </summary>
    [Fact]
    public async Task GetAuction_NewAuction_UserSelectedBestOfferIdIsNull()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sa09_a_{suffix}");
        var tokenA = await LoginAsync($"sa09_a_{suffix}");
        var clientA = ClientWithToken(tokenA);

        var catalogStickerId = await GetFirstCatalogStickerIdAsync();
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickerId);
        var auction = await CreateAuctionAsync(clientA, stickerA.Id);

        var response = await clientA.GetAsync($"/api/auctions/{auction.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.Content.ReadFromJsonAsync<AuctionResponseDTO>(JsonOpts);

        Assert.NotNull(fetched);
        Assert.Null(fetched!.UserSelectedBestOfferId);
    }

    /// <summary>
    /// SA-10: GET /api/auctions list includes UserSelectedBestOfferId (null) for all returned auctions.
    /// </summary>
    [Fact]
    public async Task GetAuctions_ResponseDtoContainsUserSelectedBestOfferId_NullByDefault()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sa10_a_{suffix}");
        var userB = await RegisterUserAsync($"sa10_b_{suffix}");
        var tokenA = await LoginAsync($"sa10_a_{suffix}");
        var tokenB = await LoginAsync($"sa10_b_{suffix}");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickerId = await GetFirstCatalogStickerIdAsync();
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickerId);
        var createdAuction = await CreateAuctionAsync(clientA, stickerA.Id);

        // UserB lists — should see UserA's auction with UserSelectedBestOfferId == null
        var response = await clientB.GetAsync("/api/auctions?page=1&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auctions = await response.Content.ReadFromJsonAsync<List<AuctionResponseDTO>>(JsonOpts);

        Assert.NotNull(auctions);
        var target = auctions!.FirstOrDefault(a => a.Id == createdAuction.Id);
        Assert.NotNull(target);
        Assert.Null(target!.UserSelectedBestOfferId);
    }

    /// <summary>
    /// SA-11: UserSelectedBestOfferId field is present in the JSON body as null (not absent).
    /// Verifies serialization includes the property even when null.
    /// </summary>
    [Fact]
    public async Task GetAuction_JsonBody_ContainsUserSelectedBestOfferIdProperty()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"sa11_a_{suffix}");
        var tokenA = await LoginAsync($"sa11_a_{suffix}");
        var clientA = ClientWithToken(tokenA);

        var catalogStickerId = await GetFirstCatalogStickerIdAsync();
        var stickerA = await PublishUserStickerAsync(clientA, userA.Id, catalogStickerId);
        var auction = await CreateAuctionAsync(clientA, stickerA.Id);

        var response = await clientA.GetAsync($"/api/auctions/{auction.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rawJson = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(rawJson);

        // The property must be present in the JSON — its value must be null (JsonValueKind.Null)
        Assert.True(
            doc.RootElement.TryGetProperty("userSelectedBestOfferId", out var prop),
            "Expected 'userSelectedBestOfferId' property to be present in the JSON response.");
        Assert.Equal(JsonValueKind.Null, prop.ValueKind);
    }
}
