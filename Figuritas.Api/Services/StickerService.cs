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

    public void CreateIfNonExistent(Sticker sticker)
    {
        if (_stickerRepo.Exists(sticker))
        {
            return;
        }
        _teamRepo.CreateIfNonExistent(new Team {Description=sticker.Team});
        _nationalTeamRepo.CreateIfNonExistent(new NationalTeam {Description = sticker.NationalTeam});
        _categoryRepo.CreateIfNonExistent(new Category {Description = sticker.Team});
        _stickerRepo.Add(sticker);
        return;
    }

}