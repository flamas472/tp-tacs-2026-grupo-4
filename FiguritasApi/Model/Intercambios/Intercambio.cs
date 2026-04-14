namespace FiguritasApi.Model;

public class Intercambio
{
    public int id {get; set; }

    public DateTime fechaRealizado {get; set; }

    public required List<FiguritaRepetida> figuritasUsuario1 {get; set; }
    
    public required List<FiguritaRepetida> figuritasUsuario2 {get; set; }

    public required PropuestaIntercambio propuestaIntercambio {get; set; }
}
