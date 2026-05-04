using System.ComponentModel.DataAnnotations;

namespace FiguritasApi.Controllers.DTO;

public class GetStickersDto//: PaginatedRequestDto
{
    public int? Number {get; set; }
    public string? TeamDescription {get; set; }
    public int? NationalTeamId {get; set; }
    public int? CategoryId {get; set; }
    public string? Description {get; set; }
    public int Page { get; set; } = 0;
    public int PageSize { get; set; } = 0;

}