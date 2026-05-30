using Figuritas.Shared.Model;
using Xunit;

namespace Figuritas.Api.Tests.Domain;

public class UserTests
{
    private User CreateBaseUser()
    {
        return new User
        {
            Id = 1,
            Username = "TestUser",
            HashedPassword = "hashedpassword",
            IsAdmin = false
        };
    }

    private Sticker CreateBaseSticker(int id)
    {
        return new Sticker
        {
            Id = id,
            Number = 10,
            NationalTeam = "Argentina",
            Team = "AFA",
            Description = "Messi",
            Category = "Legend"
        };
    }

    [Fact]
    public void AddMissingSticker_ShouldAddStickerToList_CP6_1()
    {
        // Arrange
        var user = CreateBaseUser();
        var sticker = CreateBaseSticker(1);

        // Act
        user.AddMissingSticker(sticker);

        // Assert
        Assert.Contains(sticker, user.MissingStickers);
    }

    [Fact]
    public void HasMissingSticker_ShouldReturnTrueIfStickerExists_CP6_2()
    {
        // Arrange
        var user = CreateBaseUser();
        var sticker = CreateBaseSticker(1);
        user.AddMissingSticker(sticker);

        // Act
        var result = user.HasMissingSticker(sticker);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void RemoveMissingSticker_ShouldRemoveStickerFromList_CP6_3()
    {
        // Arrange
        var user = CreateBaseUser();
        var sticker = CreateBaseSticker(1);
        user.AddMissingSticker(sticker);

        // Act
        user.RemoveMissingSticker(1);

        // Assert
        Assert.DoesNotContain(sticker, user.MissingStickers);
    }

    [Fact]
    public void Reputation_WithRatings_ShouldCalculateAverage_CP6_4()
    {
        // Arrange
        var user = CreateBaseUser();
        user.Ratings.Add(new Rate { Score = 8, ExchangeID = 1, RaterID = 2 });
        user.Ratings.Add(new Rate { Score = 10, ExchangeID = 2, RaterID = 3 });

        // Act
        var reputation = user.Reputation;

        // Assert
        Assert.Equal(9.0, reputation);
    }

    [Fact]
    public void Reputation_WithoutRatings_ShouldReturnZero_CP6_4()
    {
        // Arrange
        var user = CreateBaseUser();

        // Act
        var reputation = user.Reputation;

        // Assert
        Assert.Equal(0, reputation);
    }
}