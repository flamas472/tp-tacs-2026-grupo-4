namespace Figuritas.Shared.Model;

public class MissingSticker
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required int StickerId { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}
