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


    }