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
public class UserStory05Tests : IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public UserStory05Tests(IntegrationTestFactory factory)
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
        var user = await response.Content.ReadFromJsonAsync<UserResponseDTO>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return user!;
    }

    private async Task<string> LoginAsync(string username, string password)
    {
        var dto = new { Username = username, Password = password };
        var response = await _client.PostAsJsonAsync("/api/auth/login", dto);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
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

    private async Task<UserStickerResponseDTO> PublishStickerAsync(HttpClient authenticatedClient, int userId, int catalogStickerId, bool canBeDirectlyExchanged = true, int quantity = 2)
    {
        var dto = new PostUserStickerRequestDTO
        {
            StickerId = catalogStickerId,
            Quantity = quantity,
            CanBeDirectlyExchanged = canBeDirectlyExchanged,
            CanBeAuctioned = false
        };
        var response = await authenticatedClient.PostAsJsonAsync($"/api/users/{userId}/stickers", dto);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<UserStickerResponseDTO>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return created!;
    }

    // ─── US05 Tests ──────────────────────────────────────────────────────────

    /// <summary>
    /// Escenario 1: Propuesta exitosa — HTTP 201, State = "Pending", IDs correctos.
    /// </summary>
    [Fact]
    public async Task US05_CreateProposal_ValidScenario_Returns201WithPendingState()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us05_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us05_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us05_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us05_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerX = await PublishStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerY = await PublishStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id);

        var dto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerX.Id },
            RequestedUserStickerId = stickerY.Id,
            ProposedUserId = userB.Id
        };
        var response = await clientA.PostAsJsonAsync("/api/exchange-proposals", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ExchangeProposalResponseDTO>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.Equal("Pending", result!.State);
        Assert.Equal(userA.Id, result.ProponentUserId);
        Assert.Equal(userB.Id, result.ProposedUserId);
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
    /// Escenario 3: Sticker ofrecido con Quantity = 0 (pre-patched) → 400 BadRequest.
    /// Además verifica que crear una propuesta válida descuenta el stock del sticker ofrecido.
    /// </summary>
    [Fact]
    public async Task US05_CreateProposal_OfferedStickerWithZeroQuantity_Returns400()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us05_qty_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us05_qty_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us05_qty_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us05_qty_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerX = await PublishStickerAsync(clientA, userA.Id, catalogStickers[0].Id, quantity: 1);
        var stickerY = await PublishStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id);

        // Patch to zero manually to verify that a sticker with Qty=0 cannot be offered
        var patchDto = new { Quantity = 0 };
        var patchResponse = await clientA.PatchAsJsonAsync($"/api/users/{userA.Id}/stickers/{stickerX.Id}", patchDto);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var dto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerX.Id },
            RequestedUserStickerId = stickerY.Id,
            ProposedUserId = userB.Id
        };
        var response = await clientA.PostAsJsonAsync("/api/exchange-proposals", dto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Escenario 3b: Crear propuesta válida descuenta el stock del sticker ofrecido automáticamente.
    /// </summary>
    [Fact]
    public async Task US05_CreateProposal_ValidScenario_DecrementsOfferedStickerStock()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us05_res_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us05_res_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us05_res_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us05_res_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        // Publish with quantity=2 so after reservation Qty=1 remains
        var stickerX = await PublishStickerAsync(clientA, userA.Id, catalogStickers[0].Id, quantity: 2);
        var stickerY = await PublishStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id);

        Assert.Equal(2, stickerX.Quantity);

        var dto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerX.Id },
            RequestedUserStickerId = stickerY.Id,
            ProposedUserId = userB.Id
        };
        var createResponse = await clientA.PostAsJsonAsync("/api/exchange-proposals", dto);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        // Verify stock was decremented by reservation
        var stickerResponse = await clientA.GetAsync($"/api/users/{userA.Id}/stickers/{stickerX.Id}");
        Assert.Equal(HttpStatusCode.OK, stickerResponse.StatusCode);
        var updatedX = await stickerResponse.Content.ReadFromJsonAsync<UserStickerResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(updatedX);
        Assert.Equal(1, updatedX!.Quantity);
    }

    /// <summary>
    /// Escenario 4: Auto-propuesta (proponente == receptor) → 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task US05_CreateProposal_SelfProposal_Returns400()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us05_self_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us05_self_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerX = await PublishStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerY = await PublishStickerAsync(clientA, userA.Id, catalogStickers[1 % catalogStickers.Count].Id);

        var dto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerX.Id },
            RequestedUserStickerId = stickerY.Id,
            ProposedUserId = userA.Id
        };
        var response = await clientA.PostAsJsonAsync("/api/exchange-proposals", dto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Escenario 5: Sticker ofrecido no pertenece al proponente → 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task US05_CreateProposal_OfferedStickerNotOwnedByProponent_Returns400()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us05_own_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us05_own_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us05_own_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us05_own_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerOfB = await PublishStickerAsync(clientB, userB.Id, catalogStickers[0].Id);
        var stickerOfB2 = await PublishStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id);

        // UserA intenta ofrecer el sticker de UserB
        var dto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerOfB.Id },
            RequestedUserStickerId = stickerOfB2.Id,
            ProposedUserId = userB.Id
        };
        var response = await clientA.PostAsJsonAsync("/api/exchange-proposals", dto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Escenario 6: Sticker ofrecido con CanBeDirectlyExchanged = false → 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task US05_CreateProposal_OfferedStickerNotExchangeable_Returns400()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us05_noex_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us05_noex_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us05_noex_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us05_noex_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerX = await PublishStickerAsync(clientA, userA.Id, catalogStickers[0].Id, canBeDirectlyExchanged: false);
        var stickerY = await PublishStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id);

        var dto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerX.Id },
            RequestedUserStickerId = stickerY.Id,
            ProposedUserId = userB.Id
        };
        var response = await clientA.PostAsJsonAsync("/api/exchange-proposals", dto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Escenario 7: Sticker solicitado con CanBeDirectlyExchanged = false → 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task US05_CreateProposal_RequestedStickerNotExchangeable_Returns400()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us05_reqnoex_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us05_reqnoex_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us05_reqnoex_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us05_reqnoex_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerX = await PublishStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerY = await PublishStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id, canBeDirectlyExchanged: false);

        var dto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerX.Id },
            RequestedUserStickerId = stickerY.Id,
            ProposedUserId = userB.Id
        };
        var response = await clientA.PostAsJsonAsync("/api/exchange-proposals", dto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Escenario 8: GET /sent y /received devuelven DTOs planos con campos primitivos.
    /// </summary>
    [Fact]
    public async Task US05_GetSentAndReceived_ReturnsFlatDtos()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us05_list_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us05_list_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us05_list_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us05_list_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerX = await PublishStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerY = await PublishStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id);

        var dto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerX.Id },
            RequestedUserStickerId = stickerY.Id,
            ProposedUserId = userB.Id
        };
        var createResponse = await clientA.PostAsJsonAsync("/api/exchange-proposals", dto);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var sentResponse = await clientA.GetAsync("/api/dashboard/proposals/sent");
        Assert.Equal(HttpStatusCode.OK, sentResponse.StatusCode);
        var sentList = await sentResponse.Content.ReadFromJsonAsync<List<ExchangeProposalResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(sentList);
        var sentProposal = sentList!.FirstOrDefault(p => p.ProposedUserId == userB.Id);
        Assert.NotNull(sentProposal);
        Assert.Equal(userA.Id, sentProposal!.ProponentUserId);
        Assert.IsType<int>(sentProposal.RequestedUserStickerId);
        Assert.IsType<List<int>>(sentProposal.OfferedUserStickerIds);
        Assert.Equal("Pending", sentProposal.State);

        var receivedResponse = await clientB.GetAsync("/api/dashboard/proposals/received");
        Assert.Equal(HttpStatusCode.OK, receivedResponse.StatusCode);
        var receivedList = await receivedResponse.Content.ReadFromJsonAsync<List<ExchangeProposalResponseDTO>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(receivedList);
        var receivedProposal = receivedList!.FirstOrDefault(p => p.ProponentUserId == userA.Id);
        Assert.NotNull(receivedProposal);
        Assert.Equal(userB.Id, receivedProposal!.ProposedUserId);
        Assert.IsType<int>(receivedProposal.RequestedUserStickerId);
        Assert.IsType<List<int>>(receivedProposal.OfferedUserStickerIds);
    }

    /// <summary>
    /// Escenario 9: GET /{id} por participante legítimo → 200 OK con DTO plano.
    /// </summary>
    [Fact]
    public async Task US05_GetProposalById_ByParticipant_Returns200WithFlatDto()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us05_gbid_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us05_gbid_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us05_gbid_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us05_gbid_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerX = await PublishStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerY = await PublishStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id);

        var dto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerX.Id },
            RequestedUserStickerId = stickerY.Id,
            ProposedUserId = userB.Id
        };
        var createResponse = await clientA.PostAsJsonAsync("/api/exchange-proposals", dto);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ExchangeProposalResponseDTO>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(created);

        // El proponente puede consultar
        var getResponse = await clientA.GetAsync($"/api/exchange-proposals/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var result = await getResponse.Content.ReadFromJsonAsync<ExchangeProposalResponseDTO>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.Equal(created.Id, result!.Id);
        Assert.Equal("Pending", result.State);
        Assert.IsType<int>(result.RequestedUserStickerId);
        Assert.IsType<List<int>>(result.OfferedUserStickerIds);

        // El receptor también puede consultar
        var getResponseB = await clientB.GetAsync($"/api/exchange-proposals/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponseB.StatusCode);
    }

    /// <summary>
    /// Escenario 10: GET /{id} por usuario no participante → 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task US05_GetProposalById_ByNonParticipant_Returns403()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us05_403_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us05_403_b_{suffix}", "Password123");
        var userC = await RegisterUserAsync($"us05_403_c_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us05_403_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us05_403_b_{suffix}", "Password123");
        var tokenC = await LoginAsync($"us05_403_c_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);
        var clientC = ClientWithToken(tokenC);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerX = await PublishStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerY = await PublishStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id);

        var dto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerX.Id },
            RequestedUserStickerId = stickerY.Id,
            ProposedUserId = userB.Id
        };
        var createResponse = await clientA.PostAsJsonAsync("/api/exchange-proposals", dto);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ExchangeProposalResponseDTO>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(created);

        var getResponse = await clientC.GetAsync($"/api/exchange-proposals/{created!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);
    }

    /// <summary>
    /// Escenario 11: GET /{id} con ID inexistente → 404 NotFound.
    /// </summary>
    [Fact]
    public async Task US05_GetProposalById_NonExistentId_Returns404()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us05_404_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us05_404_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);

        var getResponse = await clientA.GetAsync("/api/exchange-proposals/999999999");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    /// <summary>
    /// Escenario 12: Aceptar propuesta → 200 OK, estado "Accepted".
    /// </summary>
    [Fact]
    public async Task AcceptProposal_WithValidData_ExecutesTradeSuccessfully()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us05_acc_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us05_acc_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us05_acc_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us05_acc_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerX = await PublishStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerY = await PublishStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id);

        var proposalDto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerX.Id },
            RequestedUserStickerId = stickerY.Id,
            ProposedUserId = userB.Id
        };
        var createResponse = await clientA.PostAsJsonAsync("/api/exchange-proposals", proposalDto);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ExchangeProposalResponseDTO>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(created);

        // Usuario A es el receptor (ProposedUserId = userB, pero la propuesta es de A hacia B,
        // entonces userB es el receptor — clientB acepta)
        var acceptResponse = await clientB.PostAsync($"/api/exchange-proposals/{created!.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var getResponse = await clientA.GetAsync($"/api/exchange-proposals/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var result = await getResponse.Content.ReadFromJsonAsync<ExchangeProposalResponseDTO>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.Equal("Accepted", result!.State);
    }

    /// <summary>
    /// Escenario 13: Rechazar propuesta → 200 OK, estado "Rejected".
    /// </summary>
    [Fact]
    public async Task RejectProposal_ByLegitimateRecipient_ChangesStateToRejected()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us05_rej_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us05_rej_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us05_rej_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us05_rej_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerX = await PublishStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerY = await PublishStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id);

        var proposalDto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerX.Id },
            RequestedUserStickerId = stickerY.Id,
            ProposedUserId = userB.Id
        };
        var createResponse = await clientA.PostAsJsonAsync("/api/exchange-proposals", proposalDto);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ExchangeProposalResponseDTO>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(created);

        // userB es el receptor de la propuesta (ProposedID = userB.Id)
        var rejectResponse = await clientB.PostAsync($"/api/exchange-proposals/{created!.Id}/reject", null);
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);

        var getResponse = await clientA.GetAsync($"/api/exchange-proposals/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var result = await getResponse.Content.ReadFromJsonAsync<ExchangeProposalResponseDTO>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.Equal("Rejected", result!.State);
    }

    /// <summary>
    /// Escenario 14: Cancelar propuesta por el proponente → 200 OK, estado "Cancelled".
    /// </summary>
    [Fact]
    public async Task CancelProposal_ByOriginalProponent_ChangesStateToCancelled()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us05_can_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us05_can_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us05_can_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us05_can_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerX = await PublishStickerAsync(clientA, userA.Id, catalogStickers[0].Id);
        var stickerY = await PublishStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id);

        var proposalDto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerX.Id },
            RequestedUserStickerId = stickerY.Id,
            ProposedUserId = userB.Id
        };
        var createResponse = await clientA.PostAsJsonAsync("/api/exchange-proposals", proposalDto);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ExchangeProposalResponseDTO>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(created);

        // userA es el proponente (ProponentID = userA.Id)
        var cancelResponse = await clientA.PostAsync($"/api/exchange-proposals/{created!.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var getResponse = await clientA.GetAsync($"/api/exchange-proposals/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var result = await getResponse.Content.ReadFromJsonAsync<ExchangeProposalResponseDTO>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.Equal("Cancelled", result!.State);
    }

    /// <summary>
    /// GAP-06 — Concurrency: double simultaneous accept of the same ExchangeProposal.
    ///
    /// Two requests fire POST /api/exchange-proposals/{id}/accept at the same time.
    /// The AcceptProposalAtomically guard (MongoDB $set conditioned on State == Pending) ensures
    /// that exactly one request transitions the proposal to Accepted and the other receives a
    /// controlled error (409 Conflict or 400 BadRequest).
    ///
    /// Invariants verified after both tasks complete:
    ///   1. Exactly one accept succeeded (HTTP 200).
    ///   2. The proposal state is "Accepted".
    ///   3. The stock of UserA and UserB is correct — no double-transfer.
    /// </summary>
    [Fact]
    public async Task Concurrency_DoubleAccept_OnlyOneSucceeds_StockCorrect()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var userA = await RegisterUserAsync($"us05_da_a_{suffix}", "Password123");
        var userB = await RegisterUserAsync($"us05_da_b_{suffix}", "Password123");
        var tokenA = await LoginAsync($"us05_da_a_{suffix}", "Password123");
        var tokenB = await LoginAsync($"us05_da_b_{suffix}", "Password123");
        var clientA = ClientWithToken(tokenA);
        var clientB = ClientWithToken(tokenB);

        // Use quantity=1 so any double-transfer is detectable (quantity would become negative or wrong)
        var catalogStickers = await GetCatalogStickersAsync(1, 2);
        var stickerX = await PublishStickerAsync(clientA, userA.Id, catalogStickers[0].Id, quantity: 1);
        var stickerY = await PublishStickerAsync(clientB, userB.Id, catalogStickers[1 % catalogStickers.Count].Id, quantity: 1);

        var proposalDto = new PostExchangeProposalRequestDTO
        {
            OfferedUserStickerIds = new List<int> { stickerX.Id },
            RequestedUserStickerId = stickerY.Id,
            ProposedUserId = userB.Id
        };
        var createResponse = await clientA.PostAsJsonAsync("/api/exchange-proposals", proposalDto);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ExchangeProposalResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(created);

        // Fire two simultaneous accept requests from two different clients for userB
        var clientB1 = ClientWithToken(tokenB);
        var clientB2 = ClientWithToken(tokenB);

        var task1 = clientB1.PostAsync($"/api/exchange-proposals/{created!.Id}/accept", null);
        var task2 = clientB2.PostAsync($"/api/exchange-proposals/{created.Id}/accept", null);

        await Task.WhenAll(task1, task2);

        var r1 = await task1;
        var r2 = await task2;

        // Invariant 1: exactly one request must have succeeded (HTTP 200 OK)
        var successCount = new[] { r1.StatusCode, r2.StatusCode }
            .Count(s => s == HttpStatusCode.OK);
        Assert.Equal(1, successCount);

        // The other must be a controlled error (400 or 409 — not 500)
        var errorStatus = r1.StatusCode == HttpStatusCode.OK ? r2.StatusCode : r1.StatusCode;
        Assert.True(
            errorStatus == HttpStatusCode.BadRequest || errorStatus == HttpStatusCode.Conflict,
            $"Expected 400 or 409 for second accept but got {(int)errorStatus}.");

        // Invariant 2: proposal state must be "Accepted"
        var getResponse = await clientA.GetAsync($"/api/exchange-proposals/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var proposal = await getResponse.Content.ReadFromJsonAsync<ExchangeProposalResponseDTO>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(proposal);
        Assert.Equal("Accepted", proposal!.State);

        // Invariant 3: stock must be coherent — no double transfer.
        // stickerX (UserA offered, qty=1): after reservation it was qty=0. After trade, it moves to UserB.
        // stickerY (UserB offered, qty=1): after trade it moves to UserA.
        // UserA should now have stickerY (or an equivalent for catalogStickers[1]).
        // UserA should NOT still see stickerX as active (transferred to B).
        var stickerXAfter = await clientA.GetAsync($"/api/users/{userA.Id}/stickers/{stickerX.Id}");
        // stickerX was offered and accepted → quantity=0, Active=false
        // GetById filters Active=true, so it may return 404 or a sticker with qty=0
        // Either outcome is acceptable (stock not doubled)
        if (stickerXAfter.StatusCode == HttpStatusCode.OK)
        {
            var xDto = await stickerXAfter.Content.ReadFromJsonAsync<UserStickerResponseDTO>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            // If still visible, quantity must be 0 (fully consumed)
            Assert.True(xDto == null || xDto.Quantity == 0,
                $"stickerX should be consumed but Quantity={xDto?.Quantity}.");
        }

        // stickerY belongs to UserB; after trade it was decremented. Check it is not double-decremented.
        var stickerYAfter = await clientB.GetAsync($"/api/users/{userB.Id}/stickers/{stickerY.Id}");
        if (stickerYAfter.StatusCode == HttpStatusCode.OK)
        {
            var yDto = await stickerYAfter.Content.ReadFromJsonAsync<UserStickerResponseDTO>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            // yDto.Quantity must be >= 0 (not negative from double-decrement)
            Assert.True(yDto == null || yDto.Quantity >= 0,
                $"stickerY quantity cannot be negative but got {yDto?.Quantity}.");
        }
    }

    // ─── IAsyncLifetime ──────────────────────────────────────────────────────

    public async Task InitializeAsync() => await _factory.CleanMutableCollectionsAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
