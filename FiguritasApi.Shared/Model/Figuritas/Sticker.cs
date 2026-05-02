namespace FiguritasApi.Shared.Model;

public class Sticker
{
    public int Id { get; set; }

    public required int Number { get; set; }

    public NationalTeam NationalTeam { get; set; }

    public Team Team { get; set; }

    public Category Category { get; set; }

    public required string Player { get; set; }
}
