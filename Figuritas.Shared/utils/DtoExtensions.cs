using System.Linq.Expressions;
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
    
    public static Expression<Func<UserSticker, bool>> ToPredicate(this GetUserStickersDto dto)
    {
        // Construimos la expresión de forma dinámica o estática
        return us => 
            (dto.Number == null || us.Sticker.Number == dto.Number) 
            && (string.IsNullOrEmpty(dto.Team) || us.Sticker.Team.Contains(dto.Team)) 
            && (string.IsNullOrEmpty(dto.NationalTeam) || us.Sticker.NationalTeam.Contains(dto.NationalTeam)) 
            && (string.IsNullOrEmpty(dto.Category) || us.Sticker.Category.Contains(dto.Category))
            && (string.IsNullOrEmpty(dto.Description) || us.Sticker.Description.Contains(dto.Description))
            && (dto.CanBeExchanged == null || us.CanBeExchanged == dto.CanBeExchanged);
    }   
}