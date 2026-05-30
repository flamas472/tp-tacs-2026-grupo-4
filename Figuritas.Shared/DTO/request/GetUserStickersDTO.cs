namespace Figuritas.Shared.DTO;
using Figuritas.Shared.Model;

public class GetUserStickersDto//: PaginatedRequestDto
{
    public int? Number {get; set; }
    public string? Team {get; set; }
    public string? NationalTeam {get; set; }
    public string? Category {get; set; }
    public string? Description {get; set; }
    public PublicationType PublicationMode{get; set;}
    public int Page { get; set; } = 0;
    public int PageSize { get; set; } = 0;

}