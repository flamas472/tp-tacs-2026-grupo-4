namespace FiguritasApi.Shared.Model;

using System.ComponentModel.DataAnnotations;

public class Rate
{
    public int Id { get; set; }

    [Range(1, 10)]
    public required double Score {get; set; }

    public required string Comment { get; set; }
    
    public int exchangeID {get; set; }
}