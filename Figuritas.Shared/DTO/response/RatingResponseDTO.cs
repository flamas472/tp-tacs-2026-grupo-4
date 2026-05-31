namespace Figuritas.Shared.DTO.response;

public class RatingResponseDTO
{
    public int Id { get; set; }
    public int ExchangeId { get; set; }
    public int EvaluatorUserId { get; set; }
    public int TargetUserId { get; set; }
    public int Stars { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}
