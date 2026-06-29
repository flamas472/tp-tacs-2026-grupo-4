using System.Linq.Expressions;
using Figuritas.Shared.DTO;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.Model;

namespace Figuritas.Shared.Utils;

public static class DtoExtensions
{
    public static Func<Sticker, bool> ToPredicate(this GetStickersDTO dto) =>
        sticker =>
            (dto.Number == null || sticker.Number == dto.Number) &&
            dto.Team.AllWordsAreContainedBy(sticker.Team) &&
            dto.NationalTeam.AllWordsAreContainedBy(sticker.NationalTeam) &&
            dto.Category.AllWordsAreContainedBy(sticker.Category) &&
            dto.Description.AllWordsAreContainedBy(sticker.Description)
        ;

    public static Expression<Func<UserSticker, bool>> ToPredicate(this GetUserStickersDTO dto)
    {
        return us =>
            (dto.Number == null || us.Sticker.Number == dto.Number)
            && (string.IsNullOrEmpty(dto.Team) || us.Sticker.Team.Contains(dto.Team))
            && (string.IsNullOrEmpty(dto.NationalTeam) || us.Sticker.NationalTeam.Contains(dto.NationalTeam))
            && (string.IsNullOrEmpty(dto.Category) || us.Sticker.Category.Contains(dto.Category))
            && (string.IsNullOrEmpty(dto.Description) || us.Sticker.Description.Contains(dto.Description))
            && (dto.CanBeDirectlyExchanged == null || us.CanBeDirectlyExchanged == dto.CanBeDirectlyExchanged)
            && (dto.CanBeAuctioned == null || us.CanBeAuctioned == dto.CanBeAuctioned);
    }

    public static Expression<Func<UserSticker, bool>> ToPredicate(this GetUserStickersDTO dto, int userId)
    {
        return us =>
            us.UserId == userId
            && us.Active
            && us.Quantity > 0
            && (dto.Number == null || us.Sticker.Number == dto.Number)
            && (string.IsNullOrEmpty(dto.Team) || us.Sticker.Team.Contains(dto.Team))
            && (string.IsNullOrEmpty(dto.NationalTeam) || us.Sticker.NationalTeam.Contains(dto.NationalTeam))
            && (string.IsNullOrEmpty(dto.Category) || us.Sticker.Category.Contains(dto.Category))
            && (string.IsNullOrEmpty(dto.Description) || us.Sticker.Description.Contains(dto.Description))
            && (dto.CanBeDirectlyExchanged == null || us.CanBeDirectlyExchanged == dto.CanBeDirectlyExchanged)
            && (dto.CanBeAuctioned == null || us.CanBeAuctioned == dto.CanBeAuctioned);
    }

    public static Expression<Func<UserSticker, bool>> ToPredicate(this GetMarketStickersDTO dto, int callerUserId, List<int>? allowedOwnerIds = null)
    {
        return us =>
            us.UserId != callerUserId
            && us.Quantity > 0
            && (us.CanBeDirectlyExchanged == true || us.CanBeAuctioned == true)
            && (allowedOwnerIds == null || allowedOwnerIds.Contains(us.UserId))
            && (dto.Number == null || us.Sticker.Number == dto.Number)
            && (string.IsNullOrEmpty(dto.Team) || us.Sticker.Team.Contains(dto.Team))
            && (string.IsNullOrEmpty(dto.NationalTeam) || us.Sticker.NationalTeam.Contains(dto.NationalTeam))
            && (string.IsNullOrEmpty(dto.Category) || us.Sticker.Category.Contains(dto.Category))
            && (string.IsNullOrEmpty(dto.Description) || us.Sticker.Description.Contains(dto.Description));
    }
}
