namespace Figuritas.Shared.Model;

using System.ComponentModel.DataAnnotations;
public class User
{
    public int Id { get; set; }

    public required string Username { get; set; }

    public required string HashedPassword {get; set;}

    public required bool isAdmin {get; set;}

    public List<Sticker> MissingStickers { get; set; } = [];

    public List<Rate>? Ratings {get; set; }

    [Range(1, 10)]
    public double Reputation => Ratings?.Average(r => r.Score) ?? 0;

    public void AddMissingSticker(Sticker sticker)
    {
        MissingStickers.Add(sticker);
    }

    public bool HasMissingSticker(Sticker sticker)
    {
        return MissingStickers.Any(s => s.Equals(sticker));
    }
    // Voy a probar si realmente es necesaria la bidireccionalidad. Caso contrario, esto vuela.
    //public List<UserSticker> InventoryStickers { get; set; } = new();

    /*
    public void AddUserSticker(UserSticker sticker)
    {
        InventoryStickers.Add(sticker);
    }

    */
}
