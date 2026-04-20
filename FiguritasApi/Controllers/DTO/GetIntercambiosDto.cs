namespace FiguritasApi.Controllers.DTO;

using FiguritasApi.Model;

public class GetIntercambiosDto
{
    public int? UsuarioProponenteID {get; set; }
    public int? UsuarioPropuestoID {get; set; }
    public int? AnyUsuarioID {get; set; }

    public Func<Intercambio, bool> ToPredicate()
    {
        return Intercambio => 
            (UsuarioProponenteID != null || Intercambio.Proponente.ID == UsuarioProponenteID) &&
            (UsuarioPropuestoID != null || Intercambio.Propuesto.ID == UsuarioPropuestoID) &&
            (AnyUsuarioID != null || Intercambio.Proponente.ID == AnyUsuarioID || Intercambio.Propuesto.ID == AnyUsuarioID);
    }

}