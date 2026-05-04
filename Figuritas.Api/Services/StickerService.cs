using Figuritas.Shared.Model;
using FiguritasApi.Controllers.DTO;

namespace Figuritas.Api.Services;

public class StickerService(StickerRepository stickerRepo)
{
    private readonly StickerRepository _stickerRepo = stickerRepo;

    public List<Sticker> GetAllStickers()
    {
        return _stickerRepo.GetAll();
    }

    public List<Sticker> Get(GetStickersDto filters)
    {
        return _stickerRepo.Get(ToPredicate(filters), filters.Page, filters.PageSize).ToList();
    }
    public Func<Sticker, bool> ToPredicate(GetStickersDto dto)
    {
        return sticker => 
            (dto.Number == null || sticker.Number == dto.Number) &&
            (string.IsNullOrEmpty(dto.TeamDescription) || dto.TeamDescription.Split(" ").All(words => sticker.Team.Description.Contains(words, StringComparison.OrdinalIgnoreCase))) &&
            (dto.NationalTeamId == null || sticker.NationalTeam.Id == dto.NationalTeamId) &&
            (dto.CategoryId == null || sticker.Category.Id == dto.CategoryId) &&
            (string.IsNullOrEmpty(dto.Description) || dto.Description.Split(" ").All(words => sticker.Description.Contains(words, StringComparison.OrdinalIgnoreCase)));
    }
    

}