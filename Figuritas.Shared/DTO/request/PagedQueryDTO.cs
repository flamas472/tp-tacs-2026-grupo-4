using System.ComponentModel.DataAnnotations;

namespace Figuritas.Shared.DTO.request;

public abstract class PagedQueryDTO
{
    [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100")]
    public int PageSize { get; set; } = 20;
}
