namespace FiguritasApi.Model;
public class FiguritaRepetida
{
    public int id {get; set; }

    public required Figurita figurita {get; set;}

    public int usuarioID {get; set; }

    public bool puedeIntercambiarse {get; set; } // 0 --> Para subasta, 1 --> Para intercambios

    public bool activo {get; set; } //TODO para que sirve este campo?

    public int cantidad {get; set; }
    
}
