namespace FiguritasApi.Model;

public class Exchange
{
    public int Id { get; set; }

    public DateTime DateCompleted { get; set; }

    public required List<UserSticker> ProponentStickers { get; set; } // Stickers ofrecidos por quien inicio la propuesta de intercambio.

    public required UserSticker ProposedSticker { get; set; } // El sticker dado por quien aceptó la oferta de intercambio.

    public required int ExchangeProposalID { get; set; }

    public required int ProponentID { get; set; }

    public required int ProposedID { get; set; }
}
