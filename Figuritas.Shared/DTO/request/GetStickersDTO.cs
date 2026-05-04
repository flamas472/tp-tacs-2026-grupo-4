namespace Figuritas.Shared.DTO;

public class GetStickersDto//: PaginatedRequestDto
{
    public int? Number {get; set; }
    public string? Team {get; set; }
    public string? NationalTeam {get; set; }
    public string? Category {get; set; }
    public string? Description {get; set; }
    public int Page { get; set; } = 0;
    public int PageSize { get; set; } = 0;

}