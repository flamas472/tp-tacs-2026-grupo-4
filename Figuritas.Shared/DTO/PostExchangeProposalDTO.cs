namespace Figuritas.Shared.DTO.request;

public class PostExchangeProposalDTO
{
    public required List<int> OfferedStickersID { get; set; }
    public required int RequestedStickerID { get; set; }
    public required int ProposedUserID { get; set; }
}
