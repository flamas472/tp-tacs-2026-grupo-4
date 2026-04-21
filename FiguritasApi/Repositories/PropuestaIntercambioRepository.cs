using FiguritasApi.Model;

// Repo para persistencia en memoria.
public class PropuestaIntercambioRepository
{
    private readonly List<PropuestaIntercambio> propuestas = new();

    public List<PropuestaIntercambio> GetAll()
    {
        return propuestas;
    }

    public void Add(PropuestaIntercambio propuesta)
    {
        propuestas.Add(propuesta);
    }

    public PropuestaIntercambio? GetByID(int propuestaID)
    {
        return propuestas.FirstOrDefault(p => p.id == propuestaID);
    }
}
