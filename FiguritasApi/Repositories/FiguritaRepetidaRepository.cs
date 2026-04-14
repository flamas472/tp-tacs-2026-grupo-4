using FiguritasApi.Model;

// Repo para persistencia en memoria. 
public class FiguritaRepetidaRepository
{
    private readonly List<FiguritaRepetida> figuritasRepetidas = new();

    public List<FiguritaRepetida> GetAll(Func<FiguritaRepetida, bool> predicate)
    {
        return figuritasRepetidas.Where(predicate).ToList();
    }

    public void Add(FiguritaRepetida figuritaRepetida)
    {
        figuritasRepetidas.Add(figuritaRepetida);
    }
}