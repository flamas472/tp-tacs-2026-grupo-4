namespace FiguritasApi.Model;

public class OfertaSubasta
{
    public int id {get; set; }

    public required Usuario ofertante {get; set; }

    public required List<FiguritaRepetida> oferta {get; set; }
    
    public required Subasta subasta {get; set; }
}
