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
public class UserStory10Tests : IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public UserStory10Tests(IntegrationTestFactory factory)
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
        return body.GetProperty("token").GetString()!;
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

    private async Task<UserStickerResponseDTO> PublishStickerAsync(
        HttpClient authenticatedClient,
        int userId,
        int catalogStickerId,
        bool canBeDirectlyExchanged = true,
        bool canBeAuctioned = false,
        int quantity = 2)
    {
        var dto = new PostUserStickerRequestDTO
        {
            StickerId = catalogStickerId,
            Quantity = quantity,
            CanBeDirectlyExchanged = canBeDirectlyExchanged,
            CanBeAuctioned = canBeAuctioned
        };
        var response = await authenticatedClient.PostAsJsonAsync($"/api/users/{userId}/stickers", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserStickerResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }))!;
    }

    private async Task<ExchangeProposalResponseDTO> CreateProposalAsync(
        HttpClient authenticatedClient,
        List<int> offeredIds,
        int requestedId,
        int proposedUserId)
    {
        var dto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = offeredIds,
            RequestedUserStickerId = requestedId,
            ProposedUserId = proposedUserId
        };
        var response = await authenticatedClient.PostAsJsonAsync("/api/exchange-proposals", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExchangeProposalResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }))!;
    }

    private async Task<int> GetExchangeIdFromAcceptedProposalAsync(HttpClient authenticatedClient, int proposalId)
    {
        var response = await authenticatedClient.GetAsync($"/api/exchange-proposals/{proposalId}/exchange");
        if (!response.IsSuccessStatusCode)
            return -1;

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (body.TryGetProperty("id", out var idProp))
            return idProp.GetInt32();

        return -1;
    }

    // ─── Grupo A — Éxito ──────────────────────────────────────────────────────

    /// <summary>
    /// A1: rating válido con comentario → 201 Created.
    /// </summary>
    [Fact]
    public async Task US10_PostRating_ValidData_Returns201()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us10a1_a_{suffix}", "password123");
        var userB = await RegisterUserAsync($"us10a1_b_{suffix}", "password123");
        var tokenA = await LoginAsync($"us10a1_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us10a1_b_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerA = await PublishStickerAsync(clientA, userA.Id, catalogStickers[0].Id, quantity: 2);
        var stickerB = await PublishStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id, quantity: 2);

        var proposal = await CreateProposalAsync(clientA, new List<int> { stickerA.Id }, stickerB.Id, userB.Id);
        var acceptResponse = await clientB.PostAsync($"/api/exchange-proposals/{proposal.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var exchangeId = await GetExchangeIdFromAcceptedProposalAsync(clientA, proposal.Id);
        Assert.True(exchangeId > 0, "Exchange ID must be retrievable after acceptance");

        var ratingDto = new PostRatingRequestDTO
        {
            ExchangeId = exchangeId,
            TargetUserId = userB.Id,
            Stars = 4,
            Comment = "Great trade!"
        };

        var response = await clientA.PostAsJsonAsync("/api/ratings", ratingDto);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<RatingResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.Equal(4, result!.Stars);
        Assert.Equal("Great trade!", result.Comment);
        Assert.Equal(userA.Id, result.EvaluatorUserId);
        Assert.Equal(userB.Id, result.TargetUserId);
    }

    /// <summary>
    /// A2: rating sin comentario (campo opcional) → 201 Created.
    /// </summary>
    [Fact]
    public async Task US10_PostRating_NoComment_Returns201()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us10a2_a_{suffix}", "password123");
        var userB = await RegisterUserAsync($"us10a2_b_{suffix}", "password123");
        var tokenA = await LoginAsync($"us10a2_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us10a2_b_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerA = await PublishStickerAsync(clientA, userA.Id, catalogStickers[0].Id, quantity: 2);
        var stickerB = await PublishStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id, quantity: 2);

        var proposal = await CreateProposalAsync(clientA, new List<int> { stickerA.Id }, stickerB.Id, userB.Id);
        var acceptResponse = await clientB.PostAsync($"/api/exchange-proposals/{proposal.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var exchangeId = await GetExchangeIdFromAcceptedProposalAsync(clientA, proposal.Id);
        Assert.True(exchangeId > 0);

        var ratingDto = new PostRatingRequestDTO
        {
            ExchangeId = exchangeId,
            TargetUserId = userB.Id,
            Stars = 5,
            Comment = null
        };

        var response = await clientA.PostAsJsonAsync("/api/ratings", ratingDto);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ─── Grupo B — Validación de rango ────────────────────────────────────────

    /// <summary>
    /// B1: Stars = 0 (bajo el rango mínimo) → 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task US10_PostRating_StarsBelowRange_Returns400()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us10b1_a_{suffix}", "password123");
        var tokenA = await LoginAsync($"us10b1_a_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);

        var ratingDto = new { ExchangeId = 1, TargetUserId = 99, Stars = 0, Comment = "test" };
        var response = await clientA.PostAsJsonAsync("/api/ratings", ratingDto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// B2: Stars = 6 (sobre el rango máximo) → 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task US10_PostRating_StarsAboveRange_Returns400()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us10b2_a_{suffix}", "password123");
        var tokenA = await LoginAsync($"us10b2_a_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);

        var ratingDto = new { ExchangeId = 1, TargetUserId = 99, Stars = 6, Comment = "test" };
        var response = await clientA.PostAsJsonAsync("/api/ratings", ratingDto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── Grupo C — Seguridad ──────────────────────────────────────────────────

    /// <summary>
    /// C1: Sin token JWT → 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task US10_PostRating_Unauthenticated_Returns401()
    {
        var anonymousClient = _factory.CreateClient();
        var ratingDto = new { ExchangeId = 1, TargetUserId = 2, Stars = 3, Comment = "test" };
        var response = await anonymousClient.PostAsJsonAsync("/api/ratings", ratingDto);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// C2: Auto-calificación (TargetUserId == raterId) → 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task US10_PostRating_SelfRating_Returns400()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us10c2_a_{suffix}", "password123");
        var tokenA = await LoginAsync($"us10c2_a_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);

        var ratingDto = new PostRatingRequestDTO
        {
            ExchangeId = 1,
            TargetUserId = userA.Id,
            Stars = 5,
            Comment = "I'm the best"
        };

        var response = await clientA.PostAsJsonAsync("/api/ratings", ratingDto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// C3: Usuario que no participó en el intercambio → 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task US10_PostRating_NonParticipant_Returns403()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us10c3_a_{suffix}", "password123");
        var userB = await RegisterUserAsync($"us10c3_b_{suffix}", "password123");
        var userC = await RegisterUserAsync($"us10c3_c_{suffix}", "password123");
        var tokenA = await LoginAsync($"us10c3_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us10c3_b_{suffix}", "password123");
        var tokenC = await LoginAsync($"us10c3_c_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);
        var clientC = ClientWithToken(tokenC);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerA = await PublishStickerAsync(clientA, userA.Id, catalogStickers[0].Id, quantity: 2);
        var stickerB = await PublishStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id, quantity: 2);

        var proposal = await CreateProposalAsync(clientA, new List<int> { stickerA.Id }, stickerB.Id, userB.Id);
        var acceptResponse = await clientB.PostAsync($"/api/exchange-proposals/{proposal.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var exchangeId = await GetExchangeIdFromAcceptedProposalAsync(clientA, proposal.Id);
        Assert.True(exchangeId > 0);

        // UserC was not part of this exchange
        var ratingDto = new PostRatingRequestDTO
        {
            ExchangeId = exchangeId,
            TargetUserId = userB.Id,
            Stars = 3
        };

        var response = await clientC.PostAsJsonAsync("/api/ratings", ratingDto);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── Grupo D — Anti-duplicación ────────────────────────────────────────────

    /// <summary>
    /// D1: intentar calificar dos veces el mismo intercambio → 409 Conflict.
    /// </summary>
    [Fact]
    public async Task US10_PostRating_DuplicateRating_Returns409()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us10d1_a_{suffix}", "password123");
        var userB = await RegisterUserAsync($"us10d1_b_{suffix}", "password123");
        var tokenA = await LoginAsync($"us10d1_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us10d1_b_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerA = await PublishStickerAsync(clientA, userA.Id, catalogStickers[0].Id, quantity: 2);
        var stickerB = await PublishStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id, quantity: 2);

        var proposal = await CreateProposalAsync(clientA, new List<int> { stickerA.Id }, stickerB.Id, userB.Id);
        var acceptResponse = await clientB.PostAsync($"/api/exchange-proposals/{proposal.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var exchangeId = await GetExchangeIdFromAcceptedProposalAsync(clientA, proposal.Id);
        Assert.True(exchangeId > 0);

        var ratingDto = new PostRatingRequestDTO
        {
            ExchangeId = exchangeId,
            TargetUserId = userB.Id,
            Stars = 4,
            Comment = "First rating"
        };

        // First rating — must succeed
        var firstResponse = await clientA.PostAsJsonAsync("/api/ratings", ratingDto);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Second rating on same exchange by same rater — must conflict
        var secondResponse = await clientA.PostAsJsonAsync("/api/ratings", ratingDto);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    // ─── Grupo E — Reputación agregada ────────────────────────────────────────

    /// <summary>
    /// E1: Dos calificaciones 3+5 → reputación promedio = 4.0.
    /// </summary>
    [Fact]
    public async Task US10_GetReputation_AverageScore_IsCorrect()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us10e1_a_{suffix}", "password123");
        var userB = await RegisterUserAsync($"us10e1_b_{suffix}", "password123");
        var userC = await RegisterUserAsync($"us10e1_c_{suffix}", "password123");
        var tokenA = await LoginAsync($"us10e1_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us10e1_b_{suffix}", "password123");
        var tokenC = await LoginAsync($"us10e1_c_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);
        var clientC = ClientWithToken(tokenC);

        var catalogStickers = await GetCatalogStickersAsync(1, 3);

        // Exchange 1: A vs B — A rates B with 3 stars
        var stickerA1 = await PublishStickerAsync(clientA, userA.Id, catalogStickers[0].Id, quantity: 2);
        var stickerB1 = await PublishStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id, quantity: 2);
        var proposal1 = await CreateProposalAsync(clientA, new List<int> { stickerA1.Id }, stickerB1.Id, userB.Id);
        var accept1 = await clientB.PostAsync($"/api/exchange-proposals/{proposal1.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept1.StatusCode);
        var exchangeId1 = await GetExchangeIdFromAcceptedProposalAsync(clientA, proposal1.Id);
        Assert.True(exchangeId1 > 0);

        var rating1 = new PostRatingRequestDTO
        {
            ExchangeId = exchangeId1,
            TargetUserId = userB.Id,
            Stars = 3
        };
        var r1Response = await clientA.PostAsJsonAsync("/api/ratings", rating1);
        Assert.Equal(HttpStatusCode.Created, r1Response.StatusCode);

        // Exchange 2: C vs B — C rates B with 5 stars
        var stickerC2 = await PublishStickerAsync(clientC, userC.Id, catalogStickers[2 % catalogStickers.Count].Id, quantity: 2);
        // UserB needs to publish a new sticker for exchange 2
        // We need a catalog sticker not used yet — use index 0 again since different user
        var catalogStickers2 = await GetCatalogStickersAsync(2, 2);
        var stickerB2 = await PublishStickerAsync(clientB, userB.Id,
            catalogStickers2.Count > 0 ? catalogStickers2[0].Id : catalogStickers[0].Id, quantity: 2);
        var proposal2 = await CreateProposalAsync(clientC, new List<int> { stickerC2.Id }, stickerB2.Id, userB.Id);
        var accept2 = await clientB.PostAsync($"/api/exchange-proposals/{proposal2.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accept2.StatusCode);
        var exchangeId2 = await GetExchangeIdFromAcceptedProposalAsync(clientC, proposal2.Id);
        Assert.True(exchangeId2 > 0);

        var rating2 = new PostRatingRequestDTO
        {
            ExchangeId = exchangeId2,
            TargetUserId = userB.Id,
            Stars = 5
        };
        var r2Response = await clientC.PostAsJsonAsync("/api/ratings", rating2);
        Assert.Equal(HttpStatusCode.Created, r2Response.StatusCode);

        // Verify reputation of userB = (3+5)/2 = 4.0
        var reputationResponse = await _client.GetAsync($"/api/users/{userB.Id}/reputation");
        Assert.Equal(HttpStatusCode.OK, reputationResponse.StatusCode);

        var reputationValue = await reputationResponse.Content.ReadFromJsonAsync<double>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal(4.0, reputationValue, precision: 1);
    }

    // ─── Grupo F — Fix inventario ──────────────────────────────────────────────

    /// <summary>
    /// F1: Sticker con Active=false (reservado por propuesta) no debe aparecer
    /// en el listado paginado de mis stickers publicados (GET /api/dashboard/stickers).
    /// </summary>
    [Fact]
    public async Task US10_GetMyStickers_AfterProposal_DoesNotReturnReservedSticker()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us10f1_a_{suffix}", "password123");
        var userB = await RegisterUserAsync($"us10f1_b_{suffix}", "password123");
        var tokenA = await LoginAsync($"us10f1_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us10f1_b_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);

        // Publish with exactly 1 unit — after reservation it becomes Active=false
        var stickerA = await PublishStickerAsync(clientA, userA.Id, catalogStickers[0].Id, quantity: 1);
        var stickerB = await PublishStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id, quantity: 2);

        Assert.Equal(1, stickerA.Quantity);

        // Create a proposal — this reserves the last unit, setting Active=false
        await CreateProposalAsync(clientA, new List<int> { stickerA.Id }, stickerB.Id, userB.Id);

        // The reserved sticker (Active=false) must NOT appear in the paginated dashboard listing
        var dashboardResponse = await clientA.GetAsync("/api/dashboard/stickers?Page=1&PageSize=50");
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);

        var myStickers = await dashboardResponse.Content.ReadFromJsonAsync<List<JsonElement>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(myStickers);

        // The reserved sticker ID should NOT appear in the results
        var reservedStickerInList = myStickers!.Any(s =>
            s.TryGetProperty("stickerId", out var id) && id.GetInt32() == stickerA.Id);
        Assert.False(reservedStickerInList,
            "A sticker with Active=false (reserved) must not appear in the paginated sticker listing.");
    }

    // ─── IAsyncLifetime ──────────────────────────────────────────────────────

    public async Task InitializeAsync() => await _factory.CleanMutableCollectionsAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
