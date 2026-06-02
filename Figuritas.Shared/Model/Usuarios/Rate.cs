namespace Figuritas.Shared.Model;

using System.ComponentModel.DataAnnotations;

public class Rate
{
    public int Id { get; set; }

    [Range(1, 5, ErrorMessage = "Stars must be between 1 and 5")]
    public int Stars { get; set; }

    [MaxLength(500)]
    public string? Comment { get; set; }

    public int ExchangeId { get; set; }

    public int EvaluatorUserId { get; set; }

    public int TargetUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
