namespace FiguritasApi.Model;
public class FiguritaRepetida
{
    public int id {get; set; }

    public required Figurita figurita {get; set;}

    // Se lleva mal con la API si le pongo la clase Usuario. Habría que analizar (otra vez) si hace falta la bidireccionalidad.
    public required int usuarioID {get; set; } 

    public bool puedeIntercambiarse {get; set; } // 0 --> Para subasta, 1 --> Para intercambios

    public bool activo {get; set; }
    
}
