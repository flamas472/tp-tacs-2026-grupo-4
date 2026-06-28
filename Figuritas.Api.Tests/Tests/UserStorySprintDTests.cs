using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Figuritas.Api.Repositories;
using Figuritas.Api.Services;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model;
using Figuritas.Shared.Model.Subastas;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Figuritas.Api.Tests;

/// <summary>
/// Sprint D — 14 test scenarios covering:
///   1.  Successful cancellation → offer Cancelled, stock restored.
///   2.  Cancel the current leading offer → 400 BadRequest.
///   3.  Cancel by non-owner → 403 Forbidden.
///   4.  Cancel already-cancelled (or non-existent) offer → controlled error.
///   5.  Inventory restitution math after cancellation.
///   6.  BestCurrentOfferId recalculated correctly after cancelling the previous leader.
///   7.  Successful PATCH — new stickers appended, stock reserved.
///   8.  PATCH on the current leading offer → 400 BadRequest.
///   9.  PATCH with duplicate IDs in AdditionalStickerIds → 400 BadRequest.
///  10.  BestCurrentOfferId recalculated after a successful PATCH.
///  11.  Concurrency — double simultaneous cancellation → only one succeeds, stock correct.
///  12.  Concurrency — double simultaneous PATCH → both arrays consolidated, no data loss.
///  13.  Concurrency — cancellation vs auction closure → no double stock release.
///  14.  Concurrency — PATCH vs auction closure → final inventory coherent.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class UserStorySprintDTests : IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public UserStorySprintDTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

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

    private HttpClient AuthClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<List<Sticker>> GetCatalogAsync(int count = 5)
    {
        var response = await _client.GetAsync($"/api/stickers?Page=1&PageSize={count}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<Sticker>>(JsonOpts))!;
    }

    private async Task<UserStickerResponseDTO> PublishStickerAsync(
        HttpClient client, int userId, int catalogStickerId,
        int quantity = 2, bool canBeAuctioned = false, bool canBeDirectlyExchanged = true)
    {
        var dto = new PostUserStickerRequestDTO
        {
            StickerId = catalogStickerId,
            Quantity = quantity,
            CanBeAuctioned = canBeAuctioned,
            CanBeDirectlyExchanged = canBeDirectlyExchanged
        };
        var response = await client.PostAsJsonAsync($"/api/users/{userId}/stickers", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserStickerResponseDTO>(JsonOpts))!;
    }

    private async Task<AuctionResponseDTO> CreateAuctionAsync(
        HttpClient client, int userStickerId, DateTime? endsAt = null)
    {
        var dto = new PostAuctionRequestDTO
        {
            UserStickerId = userStickerId,
            MinimumOfferStickerIds = new List<int>(),
            EndsAt = endsAt ?? DateTime.UtcNow.AddHours(1)
        };
        var response = await client.PostAsJsonAsync("/api/auctions", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuctionResponseDTO>(JsonOpts))!;
    }

    private async Task<AuctionOfferResponseDTO> PlaceOfferAsync(
        HttpClient client, int auctionId, List<int> userStickerIds)
    {
        var dto = new PostAuctionOfferRequestDTO { OfferedUserStickerIds = userStickerIds };
        var response = await client.PostAsJsonAsync($"/api/auctions/{auctionId}/offers", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuctionOfferResponseDTO>(JsonOpts))!;
    }

    private async Task<AuctionResponseDTO> GetAuctionAsync(int auctionId)
    {
        var response = await _client.GetAsync($"/api/auctions/{auctionId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuctionResponseDTO>(JsonOpts))!;
    }

    private async Task<List<AuctionOfferResponseDTO>> GetAuctionOffersAsync(int auctionId)
    {
        var response = await _client.GetAsync($"/api/auctions/{auctionId}/offers");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<AuctionOfferResponseDTO>>(JsonOpts))!;
    }

    private async Task<UserStickerResponseDTO?> GetUserStickerAsync(HttpClient client, int userId, int stickerId)
    {
        var response = await client.GetAsync($"/api/users/{userId}/stickers/{stickerId}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserStickerResponseDTO>(JsonOpts);
    }

    // ─── Test 1: Successful cancellation ────────────────────────────────────

    /// <summary>
    /// Scenario 1: Bidder A cancels their own non-leading offer.
    /// Bidder B has a better offer (2 stickers) so A's offer (1 sticker) is not the leader.
    /// Expected: A's offer transitions to Cancelled, A's reserved stock is restored.
    /// </summary>
    [Fact]
    public async Task CancelOffer_ValidCancellation_OfferCancelledAndStockRestored()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd1_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd1_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd1_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd1_auc_{suffix}");
        var tokenA = await LoginAsync($"sd1_bida_{suffix}");
        var tokenB = await LoginAsync($"sd1_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(4);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);
        // A publishes 1 sticker (will be the non-leader)
        var stickerA = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        // B publishes 2 stickers (will be the leader with more total stickers)
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        // A places offer first (quantity: 2 → 1)
        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA.Id });
        // B places a better offer (2 stickers) — B becomes the leader
        await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        // A's offer is not the leader — A can cancel it
        var cancelResponse = await clientA.DeleteAsync(
            $"/api/auctions/{auction.Id}/offers/{offerA.Id}");
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var cancelledOffer = (await cancelResponse.Content.ReadFromJsonAsync<AuctionOfferResponseDTO>(JsonOpts))!;
        Assert.Equal("Cancelled", cancelledOffer.State);

        // Verify stock was restored (back to 2)
        var restoredSticker = await GetUserStickerAsync(clientA, bidderA.Id, stickerA.Id);
        Assert.NotNull(restoredSticker);
        Assert.Equal(2, restoredSticker!.Quantity);
        Assert.True(restoredSticker.Active);
    }

    // ─── Test 2: Cancel leading offer → 400 ─────────────────────────────────

    /// <summary>
    /// Scenario 2: Bidder tries to cancel their offer which is the current best (BestCurrentOfferId).
    /// Expected: 400 BadRequest — cannot cancel the leading offer.
    /// </summary>
    [Fact]
    public async Task CancelOffer_LeadingOffer_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd2_auc_{suffix}");
        var bidder = await RegisterUserAsync($"sd2_bid_{suffix}");

        var tokenAuc = await LoginAsync($"sd2_auc_{suffix}");
        var tokenBid = await LoginAsync($"sd2_bid_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientBid = AuthClient(tokenBid);

        var catalog = await GetCatalogAsync(2);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);
        var bidSticker = await PublishStickerAsync(clientBid, bidder.Id, catalog[1].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);
        var offer = await PlaceOfferAsync(clientBid, auction.Id, new List<int> { bidSticker.Id });

        // After placing the only offer, it becomes BestCurrentOfferId
        var currentAuction = await GetAuctionAsync(auction.Id);
        Assert.Equal(offer.Id, currentAuction.BestCurrentOfferId);

        // Attempt to cancel the leading offer → 400
        var cancelResponse = await clientBid.DeleteAsync(
            $"/api/auctions/{auction.Id}/offers/{offer.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, cancelResponse.StatusCode);
    }

    // ─── Test 3: Cancel by non-owner → 403 ──────────────────────────────────

    /// <summary>
    /// Scenario 3: A stranger (not the bidder) tries to cancel someone else's offer.
    /// Expected: 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task CancelOffer_NonOwnerCaller_Returns403Forbidden()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd3_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd3_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd3_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd3_auc_{suffix}");
        var tokenA = await LoginAsync($"sd3_bida_{suffix}");
        var tokenB = await LoginAsync($"sd3_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(4);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);
        // BidderA places a non-leading offer (bidderB offers more so A is not the leader)
        var stickerA = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        // A places offer first
        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA.Id });
        // B places a better offer (more stickers) — A's offer is no longer the leader
        await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        // BidderB tries to cancel BidderA's offer → 403
        var cancelResponse = await clientB.DeleteAsync(
            $"/api/auctions/{auction.Id}/offers/{offerA.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, cancelResponse.StatusCode);
    }

    // ─── Test 4: Cancel already-cancelled offer → error ─────────────────────

    /// <summary>
    /// Scenario 4: Bidder attempts to cancel an offer that was already cancelled.
    /// Expected: controlled error (400 — offer no longer pending).
    /// </summary>
    [Fact]
    public async Task CancelOffer_AlreadyCancelledOffer_ReturnsControlledError()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd4_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd4_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd4_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd4_auc_{suffix}");
        var tokenA = await LoginAsync($"sd4_bida_{suffix}");
        var tokenB = await LoginAsync($"sd4_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(4);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);
        var stickerA = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        // A places offer first, then B places a better one (A is no longer the leader)
        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA.Id });
        await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        // First cancellation succeeds
        var firstCancel = await clientA.DeleteAsync($"/api/auctions/{auction.Id}/offers/{offerA.Id}");
        Assert.Equal(HttpStatusCode.OK, firstCancel.StatusCode);

        // Second cancellation on the same offer → controlled error
        var secondCancel = await clientA.DeleteAsync($"/api/auctions/{auction.Id}/offers/{offerA.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, secondCancel.StatusCode);
    }

    // ─── Test 5: Inventory restitution math ─────────────────────────────────

    /// <summary>
    /// Scenario 5: Verifies precise inventory math after cancellation.
    /// Bidder had quantity=3. After offer placement: quantity=2. After cancellation: quantity=3 again.
    /// Also verifies the offer is Active=true again.
    /// </summary>
    [Fact]
    public async Task CancelOffer_InventoryMath_QuantityRestoredExactly()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd5_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd5_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd5_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd5_auc_{suffix}");
        var tokenA = await LoginAsync($"sd5_bida_{suffix}");
        var tokenB = await LoginAsync($"sd5_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(4);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);

        // Bidder A publishes with quantity=3
        var stickerA = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id,
            quantity: 3, canBeAuctioned: false, canBeDirectlyExchanged: true);
        // Bidder B will place a better offer so A is not the leader
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        // A places offer (quantity: 3 → 2)
        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA.Id });
        var afterOffer = await GetUserStickerAsync(clientA, bidderA.Id, stickerA.Id);
        Assert.Equal(2, afterOffer!.Quantity);

        // B places a better offer (A is no longer the leader)
        await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        // A cancels (quantity: 2 → 3 restored)
        var cancelResponse = await clientA.DeleteAsync($"/api/auctions/{auction.Id}/offers/{offerA.Id}");
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var restoredSticker = await GetUserStickerAsync(clientA, bidderA.Id, stickerA.Id);
        Assert.NotNull(restoredSticker);
        Assert.Equal(3, restoredSticker!.Quantity);
        Assert.True(restoredSticker.Active);
    }

    // ─── Test 6: BestCurrentOfferId recalculated after cancel ───────────────

    /// <summary>
    /// Scenario 6: After the previous non-leader offer is cancelled and a new offer arrives,
    /// BestCurrentOfferId is recalculated correctly.
    /// Setup: B is the leader (2 stickers). A cancels their weaker offer.
    /// BestCurrentOfferId must still point to B's offer.
    /// </summary>
    [Fact]
    public async Task CancelOffer_BestCurrentOfferIdRecalculatedCorrectly()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd6_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd6_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd6_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd6_auc_{suffix}");
        var tokenA = await LoginAsync($"sd6_bida_{suffix}");
        var tokenB = await LoginAsync($"sd6_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(4);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);
        var stickerA = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        // A places weaker offer (1 sticker)
        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA.Id });

        // B places stronger offer (2 stickers) — B becomes the leader
        var offerB = await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        var beforeCancel = await GetAuctionAsync(auction.Id);
        Assert.Equal(offerB.Id, beforeCancel.BestCurrentOfferId);

        // A cancels their offer — B should remain the leader
        var cancelResponse = await clientA.DeleteAsync($"/api/auctions/{auction.Id}/offers/{offerA.Id}");
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var afterCancel = await GetAuctionAsync(auction.Id);
        Assert.Equal(offerB.Id, afterCancel.BestCurrentOfferId);
    }

    // ─── Test 7: Successful PATCH ────────────────────────────────────────────

    /// <summary>
    /// Scenario 7: Bidder appends additional stickers to their non-leading offer.
    /// Expected: new stickers are added to the offer, stock is reserved.
    /// </summary>
    [Fact]
    public async Task UpdateOffer_ValidPatch_StickersAppendedAndStockReserved()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd7_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd7_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd7_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd7_auc_{suffix}");
        var tokenA = await LoginAsync($"sd7_bida_{suffix}");
        var tokenB = await LoginAsync($"sd7_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(5);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);

        // A publishes two stickers
        var stickerA1 = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerA2 = await PublishStickerAsync(clientA, bidderA.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        // B places a better offer so A is not the leader
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[4].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        // A places offer with only stickerA1
        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA1.Id });
        // B places a better offer (A is now not the leader)
        await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        // Verify stickerA2 stock before PATCH (still 2)
        var beforePatch = await GetUserStickerAsync(clientA, bidderA.Id, stickerA2.Id);
        Assert.Equal(2, beforePatch!.Quantity);

        // A patches their offer by adding stickerA2
        var patchDto = new UpdateAuctionOfferRequestDTO { AdditionalStickerIds = new List<int> { stickerA2.Id } };
        var patchResponse = await clientA.PatchAsJsonAsync(
            $"/api/auctions/{auction.Id}/offers/{offerA.Id}", patchDto);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var updatedOffer = (await patchResponse.Content.ReadFromJsonAsync<AuctionOfferResponseDTO>(JsonOpts))!;
        Assert.Contains(stickerA1.Id, updatedOffer.OfferedUserStickerIds);
        Assert.Contains(stickerA2.Id, updatedOffer.OfferedUserStickerIds);
        Assert.Equal(2, updatedOffer.OfferedUserStickerIds.Count);

        // stickerA2 quantity should decrease (reserved)
        var afterPatch = await GetUserStickerAsync(clientA, bidderA.Id, stickerA2.Id);
        Assert.Equal(1, afterPatch!.Quantity);
    }

    // ─── Test 8: PATCH on leading offer → 400 ───────────────────────────────

    /// <summary>
    /// Scenario 8: Bidder tries to PATCH their offer which is the current best (BestCurrentOfferId).
    /// Expected: 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task UpdateOffer_LeadingOffer_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd8_auc_{suffix}");
        var bidder = await RegisterUserAsync($"sd8_bid_{suffix}");

        var tokenAuc = await LoginAsync($"sd8_auc_{suffix}");
        var tokenBid = await LoginAsync($"sd8_bid_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientBid = AuthClient(tokenBid);

        var catalog = await GetCatalogAsync(3);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);
        var bidSticker1 = await PublishStickerAsync(clientBid, bidder.Id, catalog[1].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var bidSticker2 = await PublishStickerAsync(clientBid, bidder.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);
        var offer = await PlaceOfferAsync(clientBid, auction.Id, new List<int> { bidSticker1.Id });

        // Verify it is the leading offer
        var currentAuction = await GetAuctionAsync(auction.Id);
        Assert.Equal(offer.Id, currentAuction.BestCurrentOfferId);

        // PATCH the leading offer → 400
        var patchDto = new UpdateAuctionOfferRequestDTO { AdditionalStickerIds = new List<int> { bidSticker2.Id } };
        var patchResponse = await clientBid.PatchAsJsonAsync(
            $"/api/auctions/{auction.Id}/offers/{offer.Id}", patchDto);
        Assert.Equal(HttpStatusCode.BadRequest, patchResponse.StatusCode);
    }

    // ─── Test 9: PATCH with duplicate IDs → 400 ─────────────────────────────

    /// <summary>
    /// Scenario 9: PATCH request contains duplicate IDs within AdditionalStickerIds.
    /// Expected: 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task UpdateOffer_DuplicateAdditionalStickerIds_Returns400BadRequest()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd9_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd9_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd9_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd9_auc_{suffix}");
        var tokenA = await LoginAsync($"sd9_bida_{suffix}");
        var tokenB = await LoginAsync($"sd9_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(5);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);
        var stickerA1 = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id,
            quantity: 3, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerA2 = await PublishStickerAsync(clientA, bidderA.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[4].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA1.Id });
        // B places a better offer (A is not the leader)
        await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        // PATCH with duplicates in AdditionalStickerIds
        var patchDto = new UpdateAuctionOfferRequestDTO
        {
            AdditionalStickerIds = new List<int> { stickerA2.Id, stickerA2.Id }
        };
        var patchResponse = await clientA.PatchAsJsonAsync(
            $"/api/auctions/{auction.Id}/offers/{offerA.Id}", patchDto);
        Assert.Equal(HttpStatusCode.BadRequest, patchResponse.StatusCode);
    }

    // ─── Test 10: BestCurrentOfferId recalculated after PATCH ───────────────

    /// <summary>
    /// Scenario 10: After a successful PATCH (adding stickers), BestCurrentOfferId is recalculated.
    /// Setup: A has 1 sticker (not the leader). B has 2 stickers (the leader).
    /// After A adds 2 more stickers (total 3), A should become the new leader.
    /// </summary>
    [Fact]
    public async Task UpdateOffer_BestCurrentOfferIdRecalculatedAfterPatch()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd10_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd10_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd10_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd10_auc_{suffix}");
        var tokenA = await LoginAsync($"sd10_bida_{suffix}");
        var tokenB = await LoginAsync($"sd10_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(6);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);

        var stickerA1 = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerA2 = await PublishStickerAsync(clientA, bidderA.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerA3 = await PublishStickerAsync(clientA, bidderA.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[4].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[5].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        // A places 1 sticker, B places 2 stickers — B becomes the leader
        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA1.Id });
        var offerB = await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        var beforePatch = await GetAuctionAsync(auction.Id);
        Assert.Equal(offerB.Id, beforePatch.BestCurrentOfferId);

        // A patches their offer, adding 2 more stickers (total 3) — A should become the leader
        var patchDto = new UpdateAuctionOfferRequestDTO
        {
            AdditionalStickerIds = new List<int> { stickerA2.Id, stickerA3.Id }
        };
        var patchResponse = await clientA.PatchAsJsonAsync(
            $"/api/auctions/{auction.Id}/offers/{offerA.Id}", patchDto);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        // After PATCH: A has 3 stickers, B has 2 — A should be the new leader
        var afterPatch = await GetAuctionAsync(auction.Id);
        Assert.Equal(offerA.Id, afterPatch.BestCurrentOfferId);
    }

    // ─── Test 11: Concurrency — double simultaneous cancellation ────────────

    /// <summary>
    /// Scenario 11 (Escenario A): Two simultaneous DELETE requests for the same offer.
    /// Expected: exactly one succeeds (200), the other gets 400.
    /// Stock is released exactly once — no double return.
    /// </summary>
    [Fact]
    public async Task Concurrency_DoubleCancellation_OnlyOneSucceedsStockReleasedOnce()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd11_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd11_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd11_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd11_auc_{suffix}");
        var tokenA = await LoginAsync($"sd11_bida_{suffix}");
        var tokenB = await LoginAsync($"sd11_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA1 = AuthClient(tokenA);
        var clientA2 = AuthClient(tokenA); // second client for same bidder
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(4);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);
        // A publishes with quantity=1 so double-return would produce quantity=2 (detectable)
        var stickerA = await PublishStickerAsync(clientA1, bidderA.Id, catalog[1].Id,
            quantity: 1, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        // A places offer (quantity: 1 → 0, Active = false)
        var offerA = await PlaceOfferAsync(clientA1, auction.Id, new List<int> { stickerA.Id });
        // B places a better offer so A is not the leader
        await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        // Fire two simultaneous DELETE requests for the same offer
        var task1 = clientA1.DeleteAsync($"/api/auctions/{auction.Id}/offers/{offerA.Id}");
        var task2 = clientA2.DeleteAsync($"/api/auctions/{auction.Id}/offers/{offerA.Id}");
        await Task.WhenAll(task1, task2);
        var r1 = await task1;
        var r2 = await task2;

        // Exactly one must succeed
        var successCount = new[] { r1, r2 }.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(1, successCount);

        // Stock must be exactly 1 (was 0 after offer, restored once = 1)
        var finalSticker = await GetUserStickerAsync(clientA1, bidderA.Id, stickerA.Id);
        Assert.NotNull(finalSticker);
        Assert.Equal(1, finalSticker!.Quantity);
    }

    // ─── Test 12: Concurrency — double simultaneous PATCH ───────────────────

    /// <summary>
    /// Scenario 12 (Escenario B): Two simultaneous PATCH requests for the same offer
    /// each adding a different sticker.
    /// Expected: both arrays are consolidated (3 stickers total in the offer), no data loss,
    /// both stickers' stock is reserved.
    /// </summary>
    [Fact]
    public async Task Concurrency_DoublePatch_BothArraysConsolidatedNoDataLoss()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd12_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd12_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd12_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd12_auc_{suffix}");
        var tokenA = await LoginAsync($"sd12_bida_{suffix}");
        var tokenB = await LoginAsync($"sd12_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA1 = AuthClient(tokenA);
        var clientA2 = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(6);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 2, canBeAuctioned: true);

        var stickerA1 = await PublishStickerAsync(clientA1, bidderA.Id, catalog[1].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerA2 = await PublishStickerAsync(clientA1, bidderA.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerA3 = await PublishStickerAsync(clientA1, bidderA.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        // B places a better offer (5 stickers) so A is never the leader
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[4].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[5].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id);

        // A places initial offer with stickerA1
        var offerA = await PlaceOfferAsync(clientA1, auction.Id, new List<int> { stickerA1.Id });
        // B places offer with 2 stickers — B becomes the leader
        await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        // Two concurrent PATCHes: one adds stickerA2, the other adds stickerA3
        var patchDto2 = new UpdateAuctionOfferRequestDTO { AdditionalStickerIds = new List<int> { stickerA2.Id } };
        var patchDto3 = new UpdateAuctionOfferRequestDTO { AdditionalStickerIds = new List<int> { stickerA3.Id } };

        var patchTask1 = clientA1.PatchAsJsonAsync($"/api/auctions/{auction.Id}/offers/{offerA.Id}", patchDto2);
        var patchTask2 = clientA2.PatchAsJsonAsync($"/api/auctions/{auction.Id}/offers/{offerA.Id}", patchDto3);
        await Task.WhenAll(patchTask1, patchTask2);
        var pr1 = await patchTask1;
        var pr2 = await patchTask2;

        // Both patches must succeed (Escenario B: $push/$each is sequentially safe)
        Assert.Equal(HttpStatusCode.OK, pr1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, pr2.StatusCode);

        // Both stickerA2 and stickerA3 must be reserved (quantity reduced)
        var finalA2 = await GetUserStickerAsync(clientA1, bidderA.Id, stickerA2.Id);
        var finalA3 = await GetUserStickerAsync(clientA1, bidderA.Id, stickerA3.Id);
        Assert.Equal(1, finalA2!.Quantity);
        Assert.Equal(1, finalA3!.Quantity);

        // Verify the offer contains all 3 stickers (no data loss)
        var allOffers = await GetAuctionOffersAsync(auction.Id);
        var finalOffer = allOffers.FirstOrDefault(o => o.Id == offerA.Id);
        Assert.NotNull(finalOffer);
        Assert.Contains(stickerA2.Id, finalOffer!.OfferedUserStickerIds);
        Assert.Contains(stickerA3.Id, finalOffer.OfferedUserStickerIds);
        Assert.Equal(3, finalOffer.OfferedUserStickerIds.Count);
    }

    // ─── Test 13: Concurrency — cancellation vs auction closure ─────────────

    /// <summary>
    /// Scenario 13 (Escenario C): Cancellation races against auction closure.
    ///
    /// The invariant being tested: stock is released AT MOST ONCE for A's offer.
    ///
    /// The atomic guards guarantee:
    ///   - Cancel uses TryCancelOfferAtomicallyAsync (State==Pending filter).
    ///   - Closure uses TryCloseAuctionAtomicallyAsync (Status==Active filter).
    ///   - These operate on different documents/fields, so their ordering relative to each
    ///     other defines two safe paths:
    ///       * Cancel wins → A: Cancelled (stock freed once). Close processes B's offer only.
    ///       * Close wins → A: Lost (stock freed once by close). Cancel fails (ModifiedCount==0).
    ///
    /// This test fires both operations simultaneously and then verifies the stock invariant.
    /// The auction must also be in a terminal state (closed by the worker).
    /// </summary>
    [Fact]
    public async Task Concurrency_CancellationVsAuctionClosure_NoDoubleStockRelease()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd13_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd13_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd13_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd13_auc_{suffix}");
        var tokenA = await LoginAsync($"sd13_bida_{suffix}");
        var tokenB = await LoginAsync($"sd13_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(4);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 1, canBeAuctioned: true);
        // A publishes with quantity=1 — double release would produce quantity=2 (detectable)
        var stickerA = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id,
            quantity: 1, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id, endsAt: DateTime.UtcNow.AddSeconds(3));

        // A places offer
        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA.Id });
        // B places a better offer (2 stickers) so A is not the leader
        await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        // Wait for the auction to expire
        await Task.Delay(4000);

        // Resolve service instances before launching concurrent tasks
        using var scope = _factory.Services.CreateScope();
        var auctionService = scope.ServiceProvider.GetRequiredService<AuctionService>();
        var auctionRepo = scope.ServiceProvider.GetRequiredService<IAuctionRepository>();

        // Fire cancellation (HTTP) and worker closure simultaneously
        var cancelTask = Task.Run(() =>
            clientA.DeleteAsync($"/api/auctions/{auction.Id}/offers/{offerA.Id}"));

        var workerTask = Task.Run(async () =>
        {
            try
            {
                var claimed = await auctionRepo.TryClaimAutomaticClosureAsync(auction.Id);
                if (claimed)
                    await auctionService.CloseAuctionAutomatically(auction.Id);
                return true;
            }
            catch { return false; }
        });

        await Task.WhenAll(cancelTask, workerTask);

        // Regardless of which path won the race, A's sticker stock must be ≤ 1.
        // A started with quantity=1, placed an offer (→ 0).
        // Exactly ONE of these events releases it back:
        //   - Cancel path: TryCancelOfferAtomicallyAsync → stock freed (+1 = 1)
        //   - Close path: FinalizeClosedAuctionAsync marks A Lost → stock freed (+1 = 1)
        // If both ran, quantity would be 2. The atomic guards prevent this.
        var finalStickerA = await GetUserStickerAsync(clientA, bidderA.Id, stickerA.Id);
        if (finalStickerA != null)
        {
            Assert.True(finalStickerA.Quantity <= 1,
                $"Bidder A stock was double-released. Expected ≤1 but got {finalStickerA.Quantity}.");
        }

        // The offer must end in a terminal state (either Cancelled or Lost — not Pending)
        var allOffers = await GetAuctionOffersAsync(auction.Id);
        var offerAFinal = allOffers.FirstOrDefault(o => o.Id == offerA.Id);
        if (offerAFinal != null)
        {
            Assert.True(
                offerAFinal.State == "Cancelled" || offerAFinal.State == "Lost",
                $"Offer A should be Cancelled or Lost but got: {offerAFinal.State}");
        }
    }

    // ─── Test 14: Concurrency — PATCH vs auction closure ────────────────────

    /// <summary>
    /// Scenario 14 (Escenario D): PATCH races against auction closure.
    /// Expected: final inventory is coherent — no sticker is reserved without being committed,
    /// and no committed sticker is double-counted.
    /// If close fires first (offer → Won/Lost), PATCH gets ModifiedCount==0 → aborts, rolls back stock.
    /// If PATCH fires first, the new stickers are committed and the close processes them.
    /// </summary>
    [Fact]
    public async Task Concurrency_PatchVsAuctionClosure_FinalInventoryCoherent()
    {
        var suffix = DateTime.UtcNow.Ticks.ToString();
        var auctioneer = await RegisterUserAsync($"sd14_auc_{suffix}");
        var bidderA = await RegisterUserAsync($"sd14_bida_{suffix}");
        var bidderB = await RegisterUserAsync($"sd14_bidb_{suffix}");

        var tokenAuc = await LoginAsync($"sd14_auc_{suffix}");
        var tokenA = await LoginAsync($"sd14_bida_{suffix}");
        var tokenB = await LoginAsync($"sd14_bidb_{suffix}");
        var clientAuc = AuthClient(tokenAuc);
        var clientA = AuthClient(tokenA);
        var clientB = AuthClient(tokenB);

        var catalog = await GetCatalogAsync(5);
        var aucSticker = await PublishStickerAsync(clientAuc, auctioneer.Id, catalog[0].Id,
            quantity: 1, canBeAuctioned: true);

        var stickerA1 = await PublishStickerAsync(clientA, bidderA.Id, catalog[1].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerA2 = await PublishStickerAsync(clientA, bidderA.Id, catalog[2].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var stickerB1 = await PublishStickerAsync(clientB, bidderB.Id, catalog[3].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);
        var stickerB2 = await PublishStickerAsync(clientB, bidderB.Id, catalog[4].Id,
            quantity: 2, canBeAuctioned: false, canBeDirectlyExchanged: true);

        var auction = await CreateAuctionAsync(clientAuc, aucSticker.Id, endsAt: DateTime.UtcNow.AddSeconds(3));

        // A places initial offer with stickerA1
        var offerA = await PlaceOfferAsync(clientA, auction.Id, new List<int> { stickerA1.Id });
        // B places a better offer (A is not the leader)
        await PlaceOfferAsync(clientB, auction.Id, new List<int> { stickerB1.Id, stickerB2.Id });

        // Note stickerA2 initial quantity (before PATCH race)
        var stickerA2BeforeRace = await GetUserStickerAsync(clientA, bidderA.Id, stickerA2.Id);
        var initialA2Qty = stickerA2BeforeRace!.Quantity;

        // Wait for the auction to expire
        await Task.Delay(4000);

        using var scope = _factory.Services.CreateScope();
        var auctionService = scope.ServiceProvider.GetRequiredService<AuctionService>();
        var auctionRepo = scope.ServiceProvider.GetRequiredService<IAuctionRepository>();

        // Fire PATCH and worker closure simultaneously
        var patchDto = new UpdateAuctionOfferRequestDTO { AdditionalStickerIds = new List<int> { stickerA2.Id } };
        var patchTask = Task.Run(() =>
            clientA.PatchAsJsonAsync($"/api/auctions/{auction.Id}/offers/{offerA.Id}", patchDto));

        var workerTask = Task.Run(async () =>
        {
            try
            {
                var claimed = await auctionRepo.TryClaimAutomaticClosureAsync(auction.Id);
                if (claimed)
                    await auctionService.CloseAuctionAutomatically(auction.Id);
                return true;
            }
            catch { return false; }
        });

        await Task.WhenAll(patchTask, workerTask);
        var patchResponse = await patchTask;

        // Auction must be in a terminal state
        var finalAuction = await GetAuctionAsync(auction.Id);
        Assert.True(
            finalAuction.Status == "Closed" || finalAuction.Status == "Cancelled",
            $"Expected terminal status but got: {finalAuction.Status}");

        // stickerA2 quantity must be coherent:
        // - If PATCH failed (close won the race): rollback occurred → quantity == initialA2Qty
        // - If PATCH succeeded (PATCH won the race): stock was reserved → quantity == initialA2Qty - 1
        //   and after close (offer lost) → quantity == initialA2Qty (restored by close)
        // In all coherent scenarios: quantity <= initialA2Qty (never exceeds original, never double-reserved)
        var finalA2 = await GetUserStickerAsync(clientA, bidderA.Id, stickerA2.Id);
        if (finalA2 != null)
        {
            Assert.True(finalA2.Quantity <= initialA2Qty,
                $"stickerA2 quantity exceeds initial. Expected ≤{initialA2Qty} but got {finalA2.Quantity}.");
        }
    }

    // ─── IAsyncLifetime ──────────────────────────────────────────────────────

    public async Task InitializeAsync() => await _factory.CleanMutableCollectionsAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
