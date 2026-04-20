namespace FiguritasApi.Controllers.DTO;

public class PaginatedRequestDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}