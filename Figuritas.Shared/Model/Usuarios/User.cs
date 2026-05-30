using System.ComponentModel.DataAnnotations;

namespace Figuritas.Shared.Model;

public class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string HashedPassword { get; set; }
    public required bool IsAdmin { get; set; }
    public List<Sticker> MissingStickers { get; set; } = [];
    
    public List<Rate> Ratings { get; set; } = [];

    [Range(1, 10)]
    public double Reputation => Ratings.Count != 0 ? Ratings.Average(r => r.Score) : 0;

    public void AddMissingSticker(Sticker sticker)
    {
        MissingStickers.Add(sticker);
    }

    public bool HasMissingSticker(Sticker sticker)
    {
        return MissingStickers.Any(s => s.Equals(sticker));
    }
    
    public void RemoveMissingSticker(int stickerId)
    {
        MissingStickers.RemoveAll(s => s.Id == stickerId);
    }
}