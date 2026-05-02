namespace Figuritas.Shared.Model;

public class ExchangeProposal
{
    public int Id { get; set; }

    public required int ProponentID { get; set; }

    public required int ProposedID { get; set; }
  
    // US5: Como usuario quiero hacer una propuesta por ¡UNA! figurita...
    public required UserSticker RequestedSticker { get; set; }  // Lo cambie a UserSticker por la corrección "hoy Figurita queda ambigua: por momentos parece representar el catálogo/base, y por momentos una figurita concreta operable dentro del sistema." 

    // ...pudiendo ofrecer ¡UNA O MÁS! figuritas de mi colección.
    public required List<UserSticker> OfferedStickers { get; set; }

    public ExchangeProposalState State { get; set; }

    public bool IsValid()
    {
        if (ProponentID == ProposedID)
            return false;

        if (OfferedStickers == null || OfferedStickers.Count() == 0 || RequestedSticker == null)
            return false;

        if (OfferedStickers.Any(s => s.Active != true || s.CanBeExchanged != true))
            return false;

        if (OfferedStickers.Any(s => s.UserId != ProponentID))
            return false;
        
        if (RequestedSticker.UserId != ProposedID)
            return false;

        return true;
    }
}
