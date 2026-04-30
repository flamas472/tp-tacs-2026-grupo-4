namespace FiguritasApi.Model;

public class ExchangeProposal
{
    public int Id { get; set; }

    public required User Proponent { get; set; }

    public required User Proposed { get; set; }

    public required List<UserSticker> OfferedStickers { get; set; }

    public required List<UserSticker> RequestedStickers { get; set; }  // Lo cambie a UserSticker por la corrección "hoy Figurita queda ambigua: por momentos parece representar el catálogo/base, y por momentos una figurita concreta operable dentro del sistema." 

    public ExchangeProposalState State { get; set; }
}
