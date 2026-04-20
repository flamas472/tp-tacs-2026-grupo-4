using FiguritasApi.Model;

namespace FiguritasApi.Repositories;

public class FiguritaRepository
{
    private readonly List<Figurita> figuritas = [];

    public List<Figurita> GetAll()
    {
        return figuritas;
    }

    public void Add(Figurita figurita)
    {
        figuritas.Add(figurita);
    }
}