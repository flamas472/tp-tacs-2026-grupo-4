using System.Linq; // Aseguramos que LINQ esté disponible para el .Any()

namespace Figuritas.Shared.Model;

public class ExchangeProposal
{
    public int Id { get; set; }

    public required int ProponentID { get; set; }

    public required int ProposedID { get; set; }
  
    // US5: Como usuario quiero hacer una propuesta por ¡UNA! figurita...
    public required UserSticker RequestedSticker { get; set; }  

    // ...pudiendo ofrecer ¡UNA O MÁS! figuritas de mi colección.
    public required List<UserSticker> OfferedStickers { get; set; }

    public ExchangeProposalState State { get; set; }

    public bool IsValid()
    {
        if (ProponentID == ProposedID)
            return false;

        // Se usa la propiedad .Count en lugar del método .Count() para mayor eficiencia en List<T>
        if (OfferedStickers == null || OfferedStickers.Count == 0 || RequestedSticker == null)
            return false;

        // CORREGIDO: Adaptación al nuevo Enum PublicationType para las figuritas ofrecidas
        // Falla si alguna no está activa, o si su modo NO permite intercambio directo
        if (OfferedStickers.Any(s => !s.Active || (s.PublicationMode != PublicationType.DirectExchange && s.PublicationMode != PublicationType.Both)))
            return false;

        // MEJORA: También validamos que la figurita solicitada al otro usuario admita intercambios
        if (!RequestedSticker.Active || (RequestedSticker.PublicationMode != PublicationType.DirectExchange && RequestedSticker.PublicationMode != PublicationType.Both))
            return false;

        if (OfferedStickers.Any(s => s.UserId != ProponentID))
            return false;
        
        if (RequestedSticker.UserId != ProposedID)
            return false;

        return true;
    }
}