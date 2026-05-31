using System.ComponentModel.DataAnnotations;

namespace Figuritas.Shared.DTO.request;

public class GetSuggestionsDTO
{
    [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0.")]
    public int Page { get; set; } = 1;

    [Range(1, int.MaxValue, ErrorMessage = "PageSize must be greater than 0.")]
    public int PageSize { get; set; } = 20;
}
