using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Figuritas.Shared.DTO;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Figuritas.Api.Tests;

/// <summary>
/// Integration tests for User Story 05 — Exchange proposals.
/// Uses WebApplicationFactory to run the API in memory.
/// Requires a running MongoDB instance (same as the app).
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class UserStories05Tests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public UserStories05Tests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<(int Id, string Username)> RegisterUserAsync(string username, string password)
    {
        var dto = new { Username = username, Password = password };
        var response = await _client.PostAsJsonAsync("/api/users", dto);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return (body.GetProperty("id").GetInt32(), username);
    }

    private async Task<string> LoginAsync(string username, string password)
    {
        var dto = new { Username = username, Password = password };
        var response = await _client.PostAsJsonAsync("/api/auth/login", dto);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return body.GetProperty("token").GetString()!;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<UserSticker> PublishStickerAsync(HttpClient authenticatedClient, int userId, int stickerNumber, bool canBeExchanged = true, int quantity = 2)
    {
        var dto = new PostUserStickerRequestDTO
        {
            Sticker = new StickerField
            {
                Number = stickerNumber,
                NationalTeam = "Argentina",
                Team = "TestTeam",
                Category = "Player",
                Description = $"Player {stickerNumber}"
            },
            CanBeExchanged = canBeExchanged,
            Quantity = quantity
        };
        var response = await authenticatedClient.PostAsJsonAsync($"/api/users/{userId}/stickers", dto);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<UserSticker>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return created!;
    }

    // ─── US05 Tests ──────────────────────────────────────────────────────────

    /// <summary>
    /// Escenario 1: Propuesta exitosa.
    /// UserA publica StickerX con CanBeExchanged=true, Quantity>=1.
    /// UserB publica StickerY con CanBeExchanged=true.
    /// UserA hace POST a /api/exchange-proposals.
    /// Resultado esperado: HTTP 201, body con State = "Pending",
    /// ProponentUserId == UserA.Id, ProposedUserId == UserB.Id.
    /// </summary>
    [Fact]
    public async Task US05_CreateProposal_ValidScenario_Returns201WithPendingState()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var (userAId, _) = await RegisterUserAsync($"us05_a_{suffix}", "password123");
        var (userBId, _) = await RegisterUserAsync($"us05_b_{suffix}", "password123");
        var tokenA = await LoginAsync($"us05_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us05_b_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var stickerX = await PublishStickerAsync(clientA, userAId, 1001 + (int)(DateTime.UtcNow.Ticks % 1000), canBeExchanged: true, quantity: 2);
        var stickerY = await PublishStickerAsync(clientB, userBId, 2001 + (int)(DateTime.UtcNow.Ticks % 1000), canBeExchanged: true, quantity: 2);

        var dto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerX.Id },
            RequestedUserStickerId = stickerY.Id,
            ProposedUserId = userBId
        };
        var response = await clientA.PostAsJsonAsync("/api/exchange-proposals", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ExchangeProposalResponseDTO>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.Equal("Pending", result!.State);
        Assert.Equal(userAId, result.ProponentUserId);
        Assert.Equal(userBId, result.ProposedUserId);
        Assert.Equal(stickerY.Id, result.RequestedUserStickerId);
        Assert.Contains(stickerX.Id, result.OfferedUserStickerIds);
    }

    /// <summary>
    /// Escenario 2: Sin token JWT → 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task US05_CreateProposal_WithoutToken_Returns401()
    {
        var anonymousClient = _factory.CreateClient();
        var dto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { 1 },
            RequestedUserStickerId = 2,
            ProposedUserId = 99
        };
        var response = await anonymousClient.PostAsJsonAsync("/api/exchange-proposals", dto);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Escenario 3: Stock cero en el sticker ofrecido → 400 BadRequest.
    /// UserA publica un sticker con Quantity=2, luego lo parchea a Quantity=0,
    /// y después intenta ofrecerlo en una propuesta.
    /// </summary>
    [Fact]
    public async Task US05_CreateProposal_OfferedStickerWithZeroQuantity_Returns400()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var (userAId, _) = await RegisterUserAsync($"us05_qty_a_{suffix}", "password123");
        var (userBId, _) = await RegisterUserAsync($"us05_qty_b_{suffix}", "password123");
        var tokenA = await LoginAsync($"us05_qty_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us05_qty_b_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var stickerX = await PublishStickerAsync(clientA, userAId, 3001 + (int)(DateTime.UtcNow.Ticks % 1000), canBeExchanged: true, quantity: 2);
        var stickerY = await PublishStickerAsync(clientB, userBId, 4001 + (int)(DateTime.UtcNow.Ticks % 1000), canBeExchanged: true, quantity: 2);

        // Bajar la cantidad del sticker ofrecido a 0
        var patchDto = new { Quantity = 0 };
        var patchResponse = await clientA.PatchAsJsonAsync($"/api/users/{userAId}/stickers/{stickerX.Id}", patchDto);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var dto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerX.Id },
            RequestedUserStickerId = stickerY.Id,
            ProposedUserId = userBId
        };
        var response = await clientA.PostAsJsonAsync("/api/exchange-proposals", dto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Escenario 4: Auto-propuesta — UserA intenta proponerse a sí mismo → 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task US05_CreateProposal_SelfProposal_Returns400()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var (userAId, _) = await RegisterUserAsync($"us05_self_{suffix}", "password123");
        var tokenA = await LoginAsync($"us05_self_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);

        var stickerX = await PublishStickerAsync(clientA, userAId, 5001 + (int)(DateTime.UtcNow.Ticks % 1000), canBeExchanged: true, quantity: 2);
        var stickerY = await PublishStickerAsync(clientA, userAId, 5501 + (int)(DateTime.UtcNow.Ticks % 1000), canBeExchanged: true, quantity: 2);

        var dto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerX.Id },
            RequestedUserStickerId = stickerY.Id,
            ProposedUserId = userAId // mismo usuario
        };
        var response = await clientA.PostAsJsonAsync("/api/exchange-proposals", dto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Escenario 5: Violación de ownership — UserA intenta ofrecer el UserSticker de UserB → 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task US05_CreateProposal_OfferedStickerNotOwnedByProponent_Returns400()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var (userAId, _) = await RegisterUserAsync($"us05_own_a_{suffix}", "password123");
        var (userBId, _) = await RegisterUserAsync($"us05_own_b_{suffix}", "password123");
        var tokenA = await LoginAsync($"us05_own_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us05_own_b_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        // B publica un sticker — A intentará ofrecer el sticker de B
        var stickerOfB = await PublishStickerAsync(clientB, userBId, 6001 + (int)(DateTime.UtcNow.Ticks % 1000), canBeExchanged: true, quantity: 2);
        var stickerOfB2 = await PublishStickerAsync(clientB, userBId, 6501 + (int)(DateTime.UtcNow.Ticks % 1000), canBeExchanged: true, quantity: 2);

        var dto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerOfB.Id }, // sticker de B ofrecido por A
            RequestedUserStickerId = stickerOfB2.Id,
            ProposedUserId = userBId
        };
        var response = await clientA.PostAsJsonAsync("/api/exchange-proposals", dto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Escenario 6: Ofrecido con CanBeExchanged=false → 400 BadRequest.
    /// UserA publica un sticker con CanBeExchanged=false e intenta ofrecerlo.
    /// </summary>
    [Fact]
    public async Task US05_CreateProposal_OfferedStickerNotExchangeable_Returns400()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var (userAId, _) = await RegisterUserAsync($"us05_noex_a_{suffix}", "password123");
        var (userBId, _) = await RegisterUserAsync($"us05_noex_b_{suffix}", "password123");
        var tokenA = await LoginAsync($"us05_noex_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us05_noex_b_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        // A publica un sticker con CanBeExchanged=false
        var stickerX = await PublishStickerAsync(clientA, userAId, 7001 + (int)(DateTime.UtcNow.Ticks % 1000), canBeExchanged: false, quantity: 2);
        var stickerY = await PublishStickerAsync(clientB, userBId, 7501 + (int)(DateTime.UtcNow.Ticks % 1000), canBeExchanged: true, quantity: 2);

        var dto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerX.Id },
            RequestedUserStickerId = stickerY.Id,
            ProposedUserId = userBId
        };
        var response = await clientA.PostAsJsonAsync("/api/exchange-proposals", dto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Escenario 7: Solicitado con CanBeExchanged=false → 400 BadRequest.
    /// UserB publica con ese flag en false. UserA intenta solicitarlo.
    /// </summary>
    [Fact]
    public async Task US05_CreateProposal_RequestedStickerNotExchangeable_Returns400()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var (userAId, _) = await RegisterUserAsync($"us05_reqnoex_a_{suffix}", "password123");
        var (userBId, _) = await RegisterUserAsync($"us05_reqnoex_b_{suffix}", "password123");
        var tokenA = await LoginAsync($"us05_reqnoex_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us05_reqnoex_b_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        // A publica normalmente; B publica con CanBeExchanged=false
        var stickerX = await PublishStickerAsync(clientA, userAId, 8001 + (int)(DateTime.UtcNow.Ticks % 1000), canBeExchanged: true, quantity: 2);
        var stickerY = await PublishStickerAsync(clientB, userBId, 8501 + (int)(DateTime.UtcNow.Ticks % 1000), canBeExchanged: false, quantity: 2);

        var dto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerX.Id },
            RequestedUserStickerId = stickerY.Id,
            ProposedUserId = userBId
        };
        var response = await clientA.PostAsJsonAsync("/api/exchange-proposals", dto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Escenario 8: Listados devuelven DTOs planos.
    /// Reutiliza la propuesta del escenario 1.
    /// GET /api/exchange-proposals/sent → 200 con campos primitivos (no objetos anidados).
    /// GET /api/exchange-proposals/received → 200 con campos primitivos.
    /// </summary>
    [Fact]
    public async Task US05_GetSentAndReceived_ReturnsFlatDtos()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var (userAId, _) = await RegisterUserAsync($"us05_list_a_{suffix}", "password123");
        var (userBId, _) = await RegisterUserAsync($"us05_list_b_{suffix}", "password123");
        var tokenA = await LoginAsync($"us05_list_a_{suffix}", "password123");
        var tokenB = await LoginAsync($"us05_list_b_{suffix}", "password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var stickerX = await PublishStickerAsync(clientA, userAId, 9001 + (int)(DateTime.UtcNow.Ticks % 500), canBeExchanged: true, quantity: 2);
        var stickerY = await PublishStickerAsync(clientB, userBId, 9501 + (int)(DateTime.UtcNow.Ticks % 500), canBeExchanged: true, quantity: 2);

        var dto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerX.Id },
            RequestedUserStickerId = stickerY.Id,
            ProposedUserId = userBId
        };
        var createResponse = await clientA.PostAsJsonAsync("/api/exchange-proposals", dto);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        // Verificar GET /sent para UserA
        var sentResponse = await clientA.GetAsync("/api/exchange-proposals/sent");
        Assert.Equal(HttpStatusCode.OK, sentResponse.StatusCode);
        var sentList = await sentResponse.Content.ReadFromJsonAsync<List<ExchangeProposalResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(sentList);
        var sentProposal = sentList!.FirstOrDefault(p => p.ProposedUserId == userBId);
        Assert.NotNull(sentProposal);
        Assert.Equal(userAId, sentProposal!.ProponentUserId);
        Assert.IsType<int>(sentProposal.RequestedUserStickerId);
        Assert.IsType<List<int>>(sentProposal.OfferedUserStickerIds);
        Assert.Equal("Pending", sentProposal.State);

        // Verificar GET /received para UserB
        var receivedResponse = await clientB.GetAsync("/api/exchange-proposals/received");
        Assert.Equal(HttpStatusCode.OK, receivedResponse.StatusCode);
        var receivedList = await receivedResponse.Content.ReadFromJsonAsync<List<ExchangeProposalResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(receivedList);
        var receivedProposal = receivedList!.FirstOrDefault(p => p.ProponentUserId == userAId);
        Assert.NotNull(receivedProposal);
        Assert.Equal(userBId, receivedProposal!.ProposedUserId);
        Assert.IsType<int>(receivedProposal.RequestedUserStickerId);
        Assert.IsType<List<int>>(receivedProposal.OfferedUserStickerIds);
    }

    // ─── IAsyncLifetime ──────────────────────────────────────────────────────

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;
}
