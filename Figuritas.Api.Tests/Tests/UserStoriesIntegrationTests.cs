using System.Net;
using System.Net.Http.Json;
using Figuritas.Api.Controllers;
using Figuritas.Shared.DTO;
using Figuritas.Shared.Model;
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
        var expectedResult = new UserSticker
        {
            UserId = 1,
            Sticker =
            new Sticker{
                Number = 892,
                NationalTeam = "Argentina",
                Team = "River",
                Category = "Player",
                Description = "Gabriel Mercado"
            },
            CanBeExchanged = true,
            Quantity = 1,
        };
        int userId = 1;
        var user = new
        {
            Username = "TestUser",
            Password = "123"
        };
        HttpResponseMessage postUserResponse = await _client.PostAsJsonAsync("/api/users", user); 
        postUserResponse.EnsureSuccessStatusCode();

        HttpResponseMessage aniadirFiguAColeccion = await _client.PostAsJsonAsync($"/api/Users/{userId}/stickers", userSticker);
        aniadirFiguAColeccion.EnsureSuccessStatusCode();
        var responseContent = await aniadirFiguAColeccion.Content.ReadFromJsonAsync<UserSticker>();
        
       // En lugar de Assert.Equal(userSticker, responseContent);
        Assert.Equal(HttpStatusCode.Created, aniadirFiguAColeccion.StatusCode);
        Assert.Equal(userSticker.Sticker.Description, responseContent?.Sticker.Description);
        Assert.Equal(userSticker.Quantity, responseContent?.Quantity);
        Assert.True(responseContent?.Id > 0); // Solo verificamos que se haya generado un ID
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