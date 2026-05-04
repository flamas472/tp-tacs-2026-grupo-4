namespace Figuritas.Shared.Model;

public class Sticker
{
    public int Id { get; set; }

    public required int Number { get; set; }

    public required string NationalTeam { get; set; }

    public required string Team { get; set; }

    public required string Category { get; set; }

    public required string Description { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public bool Equals(Sticker sticker)
    {
        return 
            Category.Equals(sticker.Category)
            && Description.Equals(sticker.Description)
            && Number.Equals(sticker.Number)
            && Team.Equals(sticker.Team)
            && NationalTeam.Equals(sticker.NationalTeam);

    }
}
