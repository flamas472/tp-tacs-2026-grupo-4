namespace FiguritasApi.Controllers.DTO;

using FiguritasApi.Model;

public class PostPropuestaIntercambioDto
{
    public required int UsuarioProponenteID {get; set; }

    public required List<FiguritaRepetida> FiguritasOfrecidas {get; set; }

    public required List<Figurita> FiguritasARecibir {get; set; }

    public Intercambio ToDomain(Usuario proponente, Usuario propuesto) 
    {
        return new Intercambio {
            ID = 0, // El ID se asigna automáticamente al agregarlo a la base de datos.
            Proponente = proponente,
            Propuesto= propuesto,
            FiguritasOfrecidas = this.FiguritasOfrecidas,
            FiguritasARecibir = this.FiguritasARecibir,
            ContraOferta = null,
        };
    }

}