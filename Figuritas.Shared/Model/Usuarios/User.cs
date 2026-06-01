namespace Figuritas.Shared.Model;

using System.ComponentModel.DataAnnotations;
using Figuritas.Shared.Enums;

public class User
{
    public int Id { get; set; }

    public required string Username { get; set; }

    public required string HashedPassword { get; set; }

    /// <summary>
    /// Persisted as an enum string in MongoDB thanks to JsonStringEnumConverter and BsonClassMap.
    /// Existing documents that lack this field will default to UserRole.User via SetIgnoreExtraElements.
    /// </summary>
    public UserRole Role { get; set; } = UserRole.User;

    public List<Rate> Ratings { get; set; } = [];

    [Range(0, 5)]
    public double Reputation => Ratings.Count > 0 ? Ratings.Average(r => r.Stars) : 0;

    // Notification preferences
    public bool AlertOnMissingStickerAvailable { get; set; } = true;
    public bool AlertOnAuctionEnding { get; set; } = true;
    public bool AlertOnNewProposal { get; set; } = true;
}
