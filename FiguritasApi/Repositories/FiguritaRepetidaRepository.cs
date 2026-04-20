using FiguritasApi.Model;

namespace FiguritasApi.Repositories;

public class FiguritaRepetidaRepository
{
    private readonly List<FiguritaRepetida> figuritasRepetidas = [];

    public List<FiguritaRepetida> GetAll(Func<FiguritaRepetida, bool> predicate, int page, int pageSize)
    {
        return figuritasRepetidas.Where(predicate).Skip((page - 1) * pageSize).Take(pageSize).ToList();
    }

    public void Add(FiguritaRepetida figuritaRepetida)
    {
        figuritasRepetidas.Add(figuritaRepetida);
    }
}