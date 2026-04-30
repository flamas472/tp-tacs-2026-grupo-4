using Microsoft.AspNetCore.Mvc;
using FiguritasApi.Model;

namespace FiguritasApi.Controllers;

[ApiController]
[Route("api/search")]
public class SearchController : ControllerBase
{
    private readonly InventoryFiguritaRepository _inventoryRepo;

    public SearchController(InventoryFiguritaRepository inventoryRepo)
    {
        _inventoryRepo = inventoryRepo;
    }

    [HttpGet("inventory-figuritas")]
    public ActionResult<List<InventoryFigurita>> SearchInventoryFiguritas(
        [FromQuery] int? number,
        [FromQuery] Seleccion? selection,
        [FromQuery] Equipo? team,
        [FromQuery] Categoria? category,
        [FromQuery] bool? canBeExchanged,
        [FromQuery] bool? active)
    {
        var results = _inventoryRepo.GetAll(fig =>
            (!number.HasValue || fig.Figurita.Number == number.Value) &&
            (!selection.HasValue || fig.Figurita.Selection == selection.Value) &&
            (!team.HasValue || fig.Figurita.Team == team.Value) &&
            (!category.HasValue || fig.Figurita.Category == category.Value) &&
            (!canBeExchanged.HasValue || fig.CanBeExchanged == canBeExchanged.Value) &&
            (!active.HasValue || fig.Active == active.Value)
        );
        return Ok(results);
    }
}