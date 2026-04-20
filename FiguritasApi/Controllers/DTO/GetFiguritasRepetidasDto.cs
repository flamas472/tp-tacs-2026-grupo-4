namespace FiguritasApi.Controllers.DTO;

using FiguritasApi.Model;

public class GetFiguritasRepetidasDto: PaginatedRequestDto
{
    public int? numero {get; set; }
    public int? seleccionId {get; set; }
    public int? equipoId {get; set; }
    public int? categoriaId {get; set; }

    public Func<FiguritaRepetida, bool> toPredicate()
    {
        return figurita => 
            (!numero.HasValue || figurita.figurita.numero == numero) &&
            (!seleccionId.HasValue || figurita.figurita.seleccion == (Seleccion)seleccionId) &&
            (!equipoId.HasValue || figurita.figurita.equipo == (Equipo)equipoId) &&
            (!categoriaId.HasValue || figurita.figurita.categoria == (Categoria)categoriaId);
    }
}