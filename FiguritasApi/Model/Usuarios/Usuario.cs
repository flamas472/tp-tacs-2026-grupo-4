namespace FiguritasApi.Model;
public class Usuario
{
    public int ID {get; set; }

    public required string NombreUsuario {get; set;}

    public List<FiguritaRepetida> FiguritasRepetidas {get; set; } = new();

    public List<Figurita> FiguritasFaltantes {get; set; } = new();

    public void AgregarFiguritaRepetida(FiguritaRepetida figurita) 
    {
        FiguritasRepetidas.Add(figurita);
    }

    public void AgregarFiguritaFaltante(Figurita figurita) 
    {
        FiguritasFaltantes.Add(figurita);
    }

}
