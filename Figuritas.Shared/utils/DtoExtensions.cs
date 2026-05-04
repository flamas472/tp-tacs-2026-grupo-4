using Figuritas.Shared.DTO;
using Figuritas.Shared.Model;

namespace Figuritas.Shared.Utils;

public static class DtoExtensions
{
    public static Sticker ToDomain(this StickerField dto) => new()
        {
            Number = dto.Number,
            Description = dto.Description,
            NationalTeam = dto.NationalTeam,
            Team = dto.Team,
            Category = dto.Category,
        };


    public static UserSticker ToDomain(this PostUserStickerRequestDTO dto, int userId) => new()
        {
            UserId = userId,
            Sticker = ToDomain(dto.Sticker),
            CanBeExchanged = dto.CanBeExchanged,
            Quantity = dto.Quantity
        };

    public static Func<Sticker, bool> ToPredicate(this GetStickersDto dto) =>
        sticker => 
            (dto.Number == null || sticker.Number == dto.Number) &&
            dto.Team.AllWordsAreContainedBy(sticker.Team) &&
            dto.NationalTeam.AllWordsAreContainedBy(sticker.NationalTeam) &&
            dto.Category.AllWordsAreContainedBy(sticker.Category) &&
            dto.Description.AllWordsAreContainedBy(sticker.Description)
        ;
    
    public static Func<UserSticker, bool> ToPredicate(this GetUserStickersDto dto) =>
        us => 
            (dto.Number == null || us.Sticker.Number == dto.Number) 
            && dto.Team.AllWordsAreContainedBy(us.Sticker.Team) 
            && dto.NationalTeam.AllWordsAreContainedBy(us.Sticker.NationalTeam) 
            && dto.Category.AllWordsAreContainedBy(us.Sticker.Category) 
            && dto.Description.AllWordsAreContainedBy(us.Sticker.Description) 
            && dto.CanBeExchanged == us.CanBeExchanged
        ;


    }