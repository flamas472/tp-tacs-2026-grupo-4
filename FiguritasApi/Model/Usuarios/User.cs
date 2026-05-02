namespace FiguritasApi.Model;

using System.ComponentModel.DataAnnotations;
public class User
{
    public int Id { get; set; }

    public required string Username { get; set; }

    public required string HashedPassword {get; set;}

    public required bool isAdmin {get; set;}

    // TODO: Analizar cómo habría que modelar los missing stickers.    
    // public List<Sticker> MissingStickers { get; set; } = new();

    public List<Rate>? Ratings {get; set; }

    [Range(1, 10)]
    public double Reputation => Ratings?.Average(r => r.Score) ?? 0;

    // Voy a probar si realmente es necesaria la bidireccionalidad. Caso contrario, esto vuela.
    //public List<UserSticker> InventoryStickers { get; set; } = new();

    /*
    public void AddUserSticker(UserSticker sticker)
    {
        InventoryStickers.Add(sticker);
    }

    public void AddMissingSticker(Sticker sticker)
    {
        MissingStickers.Add(sticker);
    }
    */
}
