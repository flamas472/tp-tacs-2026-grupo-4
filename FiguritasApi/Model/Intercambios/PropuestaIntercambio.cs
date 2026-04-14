namespace FiguritasApi.Model;

public class PropuestaIntercambio
{
    public int id {get; set; }

    public required Usuario proponente {get; set; }

    public required Usuario propuesto {get; set; }

    public required List<FiguritaRepetida> figuritasOfrecidas {get; set; }

    public required List<Figurita> figuritasARecibir {get; set; }

    public EstadoPropuestaIntercambio estado {get; set; }
    
}
