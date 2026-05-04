using Figuritas.Shared.Model;

namespace Figuritas.Shared.DTO;

public class PostUserStickerRequestDTO
{

    public required StickerField Sticker {get; set;}

    public required int Quantity {get;set;}

    public required bool CanBeExchanged {get;set;}

}

public class StickerField
{
    public required int Number {get;set;}
    public required string NationalTeamDescription {get;set;}
    public required string TeamDescription {get;set;}
    public required string Description {get;set;}
    public required string CategoryDescription {get;set;}

}