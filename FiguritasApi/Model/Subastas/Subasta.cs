namespace FiguritasApi.Model;

public class Subasta
{
    public int ID {get; set; }

    public required Usuario Subastador {get; set; }

    public DateTime FechaInicio {get; set; }

    public DateTime FechaFin {get; set; }

    public required List<Figurita> OfertaMinima {get; set; }
    
    public required List<Figurita> FiguritasSubastadas {get; set; }

    public required List<OfertaSubasta> Ofertas {get; set; }
}
