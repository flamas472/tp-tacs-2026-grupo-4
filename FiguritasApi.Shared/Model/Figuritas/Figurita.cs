namespace FiguritasApi.Shared.Model;

public class Figurita
{
    public int Id { get; set; }

    public int Number { get; set; }

    public Seleccion Selection { get; set; }

    public Equipo Team { get; set; }

    public Categoria Category { get; set; }

    // Persisting the player without ORM for cascade is complicated, TODO
    //public Jugador Player { get; set; }
}
