using Figuritas.Shared.Model.Intercambios;

namespace Figuritas.Api.Repositories;

public interface IExchangeRepository
{
    List<Exchange> GetAll();
    void Add(Exchange exchange);
    Exchange? GetById(int exchangeId);
}
