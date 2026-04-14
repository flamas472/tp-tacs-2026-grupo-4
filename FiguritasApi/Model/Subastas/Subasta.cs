namespace FiguritasApi.Model;

public class Subasta
{
    public int id {get; set; }

    public required Usuario subastador {get; set; }

    public DateTime fechaInicio {get; set; }

    public DateTime fechaFin {get; set; }

    public required List<Figurita> ofertaMinima {get; set; }
    
    public required Figurita figuritaSubastada {get; set; }

    public OfertaSubasta? mejorOfertaActual {get; set; }
}
