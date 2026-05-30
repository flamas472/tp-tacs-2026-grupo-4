using Figuritas.Shared.DTO;
using Figuritas.Shared.Model;
using Figuritas.Shared.Utils;
namespace Figuritas.Api.Services;

public class StickerService(
    StickerRepository stickerRepo,
    CategoryRepository categoryRepo,
    TeamRepository teamRepo,
    NationalTeamRepository nationalTeamRepo
    )
{
    private readonly StickerRepository _stickerRepo = stickerRepo;
    private readonly CategoryRepository _categoryRepo = categoryRepo;
    private readonly TeamRepository _teamRepo = teamRepo;
    private readonly NationalTeamRepository _nationalTeamRepo = nationalTeamRepo;

    public List<Sticker> GetAllStickers()
    {
        return _stickerRepo.GetAll();
    }

    public List<Sticker> Get(GetStickersDto filters)
    {
        return _stickerRepo.Get(filters.ToPredicate(), filters.Page, filters.PageSize).ToList();
    }

    public Sticker? GetStickerById(int id)
    {
        return _stickerRepo.GetById(id);
    }

}