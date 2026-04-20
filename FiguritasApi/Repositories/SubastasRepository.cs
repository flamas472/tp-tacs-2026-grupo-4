using FiguritasApi.Model;

namespace FiguritasApi.Repositories;

public class SubastasRepository
{
    private readonly List<Subasta> subastas = [];

    public List<Subasta> GetAll(Func<Subasta, bool> predicate)
    {
        return subastas.Where(predicate).ToList();
    }

    public void Add(Subasta subasta)
    {
        subastas.Add(subasta);
    }

    public void AddOferta(int subastaID, OfertaSubasta oferta)
    {
        var subasta = subastas.SingleOrDefault(s => s.ID == subastaID);
        subasta?.Ofertas.Add(oferta);
        return;
    }
}