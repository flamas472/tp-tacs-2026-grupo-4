namespace FiguritasApi.Controllers.DTO;

using FiguritasApi.Model;

public class GetSubastasDto: PaginatedRequestDto
{
    public DateOnly? FechaFin {get; set; }
    public DateOnly? FechaInicio {get; set; }
    public int? HaceXMilisegundos {get; set; }
    public int? FinalizaEnMenosDeXMilisegundos {get; set; }
    public int? SubastadorID {get; set; }
    public List<int>? FiguritasSubastadasID {get; set; }

    public Func<Subasta, bool> ToPredicate()
    {
        return subasta => 
            (!FechaFin.HasValue || DateOnly.FromDateTime(subasta.FechaFin) == FechaFin) &&
            (!FechaInicio.HasValue || DateOnly.FromDateTime(subasta.FechaInicio) == FechaInicio) &&
            (!HaceXMilisegundos.HasValue || subasta.FechaInicio > DateTime.Now.AddMilliseconds(-HaceXMilisegundos.Value)) &&
            (!FinalizaEnMenosDeXMilisegundos.HasValue || subasta.FechaFin < DateTime.Now.AddMilliseconds(-FinalizaEnMenosDeXMilisegundos.Value)) &&
            (!SubastadorID.HasValue || subasta.Subastador.ID == SubastadorID) &&
            (FiguritasSubastadasID?.Count != 0 || FiguritasSubastadasID.All(id => subasta.FiguritasSubastadas.Any(f => f.id == id)));
    }
}