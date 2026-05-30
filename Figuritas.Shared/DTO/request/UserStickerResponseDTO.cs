using Figuritas.Shared.Model;

namespace Figuritas.Shared.DTO;

public class UserStickerResponseDTO
{
    public int Id { get; set; }
    public required Sticker Sticker { get; set; }
    public int UserId { get; set; }
    public PublicationType PublicationMode { get; set; }
    public bool Active { get; set; }
    public int Quantity { get; set; }
}