namespace FiguritasApi.Shared.Model;

public class ExchangeProposal
{
    public int Id { get; set; }

    public required User Proponent { get; set; }

    public required User Proposed { get; set; }

    public required List<InventoryFigurita> OfferedFiguritas { get; set; }

    public required List<Figurita> ReceivedFiguritas { get; set; }

    public ExchangeProposalState State { get; set; }
}
