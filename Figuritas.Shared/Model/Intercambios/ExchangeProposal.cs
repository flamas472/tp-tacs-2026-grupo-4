namespace Figuritas.Shared.Model.Intercambios;

public class ExchangeProposal
{
    public int Id { get; set; }

    public required int ProponentID { get; set; }

    public required int ProposedID { get; set; }

    public required int RequestedUserStickerId { get; set; }

    public required List<int> OfferedUserStickerIds { get; set; }

    public ExchangeProposalState State { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
