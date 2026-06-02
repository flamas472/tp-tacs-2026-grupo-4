namespace Figuritas.Shared.Model.Intercambios;

public class Exchange
{
    public int Id { get; set; }

    public DateTime DateCompleted { get; set; }

    public required List<int> ProponentUserStickerIds { get; set; }

    public required int ProposedUserStickerId { get; set; }

    public required int ExchangeProposalID { get; set; }

    public required int ProponentID { get; set; }

    public required int ProposedID { get; set; }
}
