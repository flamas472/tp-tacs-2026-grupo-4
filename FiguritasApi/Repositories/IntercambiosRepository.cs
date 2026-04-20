using FiguritasApi.Model;

namespace FiguritasApi.Repositories;

public class IntercambiosRepository
{
    private readonly List<Intercambio> intercambios = [];

    public List<Intercambio> GetAll(Func<Intercambio, bool> predicate)
    {
        return intercambios.Where(predicate).ToList();
    }

    public void Add(Intercambio intercambio)
    {
        intercambios.Add(intercambio);
    }

    public void AddContraoferta(int intercambioID, Intercambio intercambio)
    {
        intercambios.Add(intercambio);
        var intercambioOriginal = intercambios.SingleOrDefault(i => i.ID == intercambioID);
        intercambioOriginal?.ContraOferta = intercambio;
    }
}