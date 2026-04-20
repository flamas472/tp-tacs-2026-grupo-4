namespace FiguritasApi.Model;

public class OfertaSubasta
{
    public int ID {get; set; }

    public required Usuario Ofertante {get; set; }

    public required List<Figurita> FiguritasOfertadas {get; set; }
    
    public DateTime FechaOferta {get; set; } 
    
}
