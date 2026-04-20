namespace FiguritasApi.Controllers.DTO;

using FiguritasApi.Model;

public class PostFiguritaRepetidaDto
{
    public required Figurita Figurita {get; set;}
    public bool PuedeIntercambiarse {get; set; } // 0 --> Para subasta, 1 --> Para intercambios
    public bool Activo {get; set; }
    public int Cantidad {get; set; }

    public FiguritaRepetida ToDomain(int usuarioId) 
    {
        return new FiguritaRepetida {
            id = 0, // El ID se asigna automáticamente al agregarlo a la base de datos.
            figurita = this.Figurita,
            puedeIntercambiarse = this.PuedeIntercambiarse,
            activo = this.Activo,
            cantidad = this.Cantidad,
            usuarioID = usuarioId,
        };
    }
    
}