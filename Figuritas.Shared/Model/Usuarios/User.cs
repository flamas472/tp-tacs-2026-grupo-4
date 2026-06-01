namespace Figuritas.Shared.Model;

using System.ComponentModel.DataAnnotations;
public class User
{
    public int Id { get; set; }

    public required string Username { get; set; }

    public required string HashedPassword {get; set;}

    public required bool IsAdmin {get; set;}

    public List<Rate> Ratings { get; set; } = [];

    [Range(0, 5)]
    public double Reputation => Ratings.Count > 0 ? Ratings.Average(r => r.Stars) : 0;

    // Notification preferences
    public bool AlertOnMissingStickerAvailable { get; set; } = true;
    public bool AlertOnAuctionEnding { get; set; } = true;
    public bool AlertOnNewProposal { get; set; } = true;
}
