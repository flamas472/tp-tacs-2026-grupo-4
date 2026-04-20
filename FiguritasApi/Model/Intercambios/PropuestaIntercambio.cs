namespace FiguritasApi.Model;

public class Intercambio
{
    public int ID {get; set; }
    public required Usuario Proponente {get; set; }
    public required Usuario Propuesto {get; set; }
    public required List<FiguritaRepetida> FiguritasOfrecidas {get; set; }
    public required List<Figurita> FiguritasARecibir {get; set; }
    public Intercambio? ContraOferta {get; set; }
    public DateTime FechaPropuesta {get; set; }
    public DateTime FechaCancelacion {get; set; }
    public DateTime FechaAceptacion {get; set; }
    public DateTime FechaRechazo {get; set; }
    public DateTime FechaContraofertada {get; set; }

    public EstadoIntercambio Estado =>
        ContraOferta != null ? EstadoIntercambio.Contraofertada :
        FechaAceptacion != default ? EstadoIntercambio.Aceptada :
        FechaRechazo != default ? EstadoIntercambio.Rechazada :
        FechaCancelacion != default ? EstadoIntercambio.Cancelada :
        FechaContraofertada != default ? EstadoIntercambio.Contraofertada : EstadoIntercambio.Pendiente;
}
