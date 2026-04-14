namespace FiguritasApi.Model;
public class Usuario
{
    public int id {get; set; }

    public required string nombreUsuario {get; set;}

    public List<FiguritaRepetida>? figuritasRepetidas {get; set; }

    public List<Figurita>? figuritasFaltantes {get; set; }

    public int reputacion {get; set; }
    
}
