using FiguritasApi.Model;

// Repo para persistencia en memoria. 
public class FiguritaRepetidaRepository
{
    private readonly List<FiguritaRepetida> figuritasRepetidas = new();

    public List<FiguritaRepetida> GetAll()
    {
        return figuritasRepetidas;
    }

    public void Add(FiguritaRepetida figuritaRepetida)
    {
        figuritasRepetidas.Add(figuritaRepetida);
    }
}