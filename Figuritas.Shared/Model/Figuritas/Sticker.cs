namespace Figuritas.Shared.Model;

public class Sticker
{
    public int Id { get; set; }

    public required int Number { get; set; }

    public required NationalTeam NationalTeam { get; set; }

    public required Team Team { get; set; }

    public required Category Category { get; set; }

    public required string Description { get; set; }
}
