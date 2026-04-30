namespace FiguritasApi.Model;
public class User
{
    public int Id { get; set; }

    public required string Username { get; set; }

    public List<UserSticker> InventoryStickers { get; set; } = new();

    public List<Sticker> MissingStickers { get; set; } = new();

    public void AddUserSticker(UserSticker sticker)
    {
        InventoryStickers.Add(sticker);
    }

    public void AddMissingSticker(Sticker sticker)
    {
        MissingStickers.Add(sticker);
    }
}
