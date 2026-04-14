namespace FiguritasApi.Model;
public class FiguritaRepetida
{
    public int id {get; set; }

    public required Figurita figurita {get; set;}

    public required Usuario usuario {get; set; }

    public bool puedeIntercambiarse {get; set; } // 0 --> Para subasta, 1 --> Para intercambios

    public bool activo {get; set; }
    
}
