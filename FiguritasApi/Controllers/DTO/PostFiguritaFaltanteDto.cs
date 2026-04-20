namespace FiguritasApi.Controllers.DTO;

using FiguritasApi.Model;

public class PostFiguritaFaltanteDto
{
    public required Figurita Figurita {get; set;}

    public Figurita ToDomain() 
    {
        return new Figurita {
            id = this.Figurita.id,
            seleccion = this.Figurita.seleccion,
            equipo = this.Figurita.equipo,
            categoria = this.Figurita.categoria,
            numero = this.Figurita.numero
        };
    }
    
}