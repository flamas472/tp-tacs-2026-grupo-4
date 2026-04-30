namespace FiguritasApi.Model;

public class Sticker
{
    public int Id { get; set; }

    public int Number { get; set; }

    public NationalTeam NationalTeam { get; set; }

    public Team Team { get; set; }

    public Category Category { get; set; }

    // Persisting the player without ORM for cascade is complicated, TODO
    //public Jugador Player { get; set; }
}
