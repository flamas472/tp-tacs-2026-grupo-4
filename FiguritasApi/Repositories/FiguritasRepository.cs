using FiguritasApi.Model;

// Repo para persistencia en memoria. 
public class FiguritaRepository
{
    private readonly List<Figurita> figuritas = new();

    public List<Figurita> GetAll()
    {
        return figuritas;
    }

    public void Add(Figurita figurita)
    {
        figuritas.Add(figurita);
    }
}