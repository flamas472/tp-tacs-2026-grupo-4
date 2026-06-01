using Figuritas.Shared.Model.Intercambios;
using MongoDB.Driver;

namespace Figuritas.Api.Repositories;

public interface IExchangeRepository
{
    List<Exchange> GetAll();
    void Add(Exchange exchange);
    void Add(Exchange exchange, IClientSessionHandle session);
    Exchange? GetById(int exchangeId);
    Exchange? GetByProposalId(int proposalId);
}
