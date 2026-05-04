using Figuritas.Shared.Model;

namespace Figuritas.Shared.DTO;

public class PostUserStickerResponseDTO
{
    public int UserId {get; set;}

    public required StickerField Sticker {get; set;}

    public required int Quantity {get;set;}

    public required bool CanBeExchanged {get;set;}


}