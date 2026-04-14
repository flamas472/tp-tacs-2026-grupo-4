namespace FiguritasApi.Model;

public class Figurita
{
    public int id {get; set; }

    public Seleccion seleccion {get; set; }

    public Equipo equipo {get; set; }

    public Categoria categoria {get; set; }

    // Hacer que se persista el jugador sin tener un ORM para que se haga on cascade es un quilombo, queda ToDo
    //public Jugador jugador {get; set; }
    
}
