using Figuritas.Shared.Model;

namespace Figuritas.Shared.DTO;

public class PostUserStickerRequestDTO
{

    public required StickerField Sticker {get; set;}

    public required int Quantity {get;set;}

    public required bool CanBeExchanged {get;set;}

}
