using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FiguritasApi.Tests;

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