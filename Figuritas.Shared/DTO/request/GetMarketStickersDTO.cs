using System.ComponentModel.DataAnnotations;

namespace Figuritas.Shared.DTO.request;

public class GetMarketStickersDTO
{
    [Range(1, int.MaxValue, ErrorMessage = "Number must be greater than 0.")]
    public int? Number { get; set; }

    public string? Team { get; set; }

    public string? NationalTeam { get; set; }

    public string? Category { get; set; }

    public string? Description { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0.")]
    public int Page { get; set; } = 1;

    [Range(1, int.MaxValue, ErrorMessage = "PageSize must be greater than 0.")]
    public int PageSize { get; set; } = 20;
}
