using System.Net;
using System.Net.Http.Json;
using Figuritas.Api.Controllers;
using Figuritas.Shared.DTO;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Figuritas.Api.Tests;

/// <summary>
/// Integration tests for User Stories of Delivery 1.
/// Uses WebApplicationFactory to run the API in memory and test REST design.
/// </summary>
public class UserStoriesIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public UserStoriesIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetUsers_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostUser_ReturnsCreated()
    {
        var user = new
        {
            Username = "testuser",
            InventoryFiguritas = new object[] { },
            MissingFiguritas = new object[] { }
        };
        var response = await _client.PostAsJsonAsync("/api/users", user);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task PostFigurita_ReturnsCreated()
    {
        var figurita = new
        {
            Number = 10,
            Selection = "Argentina",
            Team = "River",
            Category = "Player"
        };
        var response = await _client.PostAsJsonAsync("/api/figuritas", figurita);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task US1_PostFigurita_ReturnsCreatedAt()
    {

        var newSticker = new StickerField
        {
            Number = 892,
            NationalTeamDescription = "Argentina",
            TeamDescription = "River",
            CategoryDescription = "Player",
            Description = "Gabriel Mercado"
        };
        var userSticker = new PostUserStickerRequestDTO{
            Sticker = newSticker,
            CanBeExchanged = true,
            Quantity = 1,
        };
        var expectedResult = new PostUserStickerResponseDTO
        {
            UserId = 1,
            Sticker =
            new StickerField{
                Number = 892,
                NationalTeamDescription = "Argentina",
                TeamDescription = "River",
                CategoryDescription = "Player",
                Description = "Gabriel Mercado"
            },
            CanBeExchanged = true,
            Quantity = 1,
        };
        int userId = 1;

        HttpResponseMessage aniadirFiguAColeccion = await _client.PostAsJsonAsync($"/api/Users/{userId}/stickers", userSticker);
        aniadirFiguAColeccion.EnsureSuccessStatusCode();
        var responseContent = await aniadirFiguAColeccion.Content.ReadFromJsonAsync<PostUserStickerResponseDTO>();
        
        Assert.Equal(HttpStatusCode.Created, aniadirFiguAColeccion.StatusCode);
        Assert.Equal(expectedResult, responseContent);
    }

    [Fact]
    public async Task SearchInventoryFiguritas_ReturnsFilteredResults()
    {
        // First create a figurita
        var figurita = new
        {
            Number = 10,
            Selection = "Argentina",
            Team = "Boca",
            Category = "Player"
        };
        var figResponse = await _client.PostAsJsonAsync("/api/figuritas", figurita);
        figResponse.EnsureSuccessStatusCode();

        // Create a user and add to inventory
        var user = new
        {
            Username = "searchuser",
            InventoryFiguritas = new object[] { },
            MissingFiguritas = new object[] { }
        };
        var userResponse = await _client.PostAsJsonAsync("/api/users", user);
        userResponse.EnsureSuccessStatusCode();

        var inventory = new
        {
            Figurita = new { Number = 10, Selection = "Argentina", Team = "Boca", Category = "Player" },
            CanBeExchanged = true,
            Active = true
        };
        var invResponse = await _client.PostAsJsonAsync("/api/users/1/inventory", inventory);
        invResponse.EnsureSuccessStatusCode();

        // Search
        var searchResponse = await _client.GetAsync("/api/search/inventory-figuritas?selection=Argentina&team=Boca");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        var results = await searchResponse.Content.ReadFromJsonAsync<List<dynamic>>();
        Assert.NotNull(results);
        Assert.NotEmpty(results);
    }
}