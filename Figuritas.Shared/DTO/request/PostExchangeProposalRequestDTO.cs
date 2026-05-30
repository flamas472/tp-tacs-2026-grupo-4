namespace Figuritas.Shared.DTO.request;

public class PostExchangeProposalRequestDTO
{
    public required List<int> OfferedUserStickerIds { get; set; }
    public required int RequestedUserStickerId { get; set; }
    public required int ProposedUserId { get; set; }
}
