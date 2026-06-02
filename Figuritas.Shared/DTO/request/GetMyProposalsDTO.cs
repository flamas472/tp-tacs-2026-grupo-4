using Figuritas.Shared.Model.Intercambios;

namespace Figuritas.Shared.DTO.request;

public class GetMyProposalsDTO : PagedQueryDTO
{
    public ExchangeProposalState? State { get; set; }
}
