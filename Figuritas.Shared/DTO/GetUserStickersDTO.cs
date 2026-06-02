namespace Figuritas.Shared.DTO;

public class GetUserStickersDTO
{
    public int? Number { get; set; }
    public string? Team { get; set; }
    public string? NationalTeam { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public bool? CanBeDirectlyExchanged { get; set; }
    public bool? CanBeAuctioned { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}