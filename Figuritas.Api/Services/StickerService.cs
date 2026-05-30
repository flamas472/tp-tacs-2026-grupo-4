using Figuritas.Api.Repositories;
using Figuritas.Shared.DTO;
using Figuritas.Shared.Model;
using Figuritas.Shared.Utils;

namespace Figuritas.Api.Services;

public class StickerService(
    IStickerRepository stickerRepo,
    ICategoryRepository categoryRepo,
    ITeamRepository teamRepo,
    INationalTeamRepository nationalTeamRepo
    )
{
    private readonly IStickerRepository _stickerRepo = stickerRepo;
    private readonly ICategoryRepository _categoryRepo = categoryRepo;
    private readonly ITeamRepository _teamRepo = teamRepo;
    private readonly INationalTeamRepository _nationalTeamRepo = nationalTeamRepo;

    public List<Sticker> GetAllStickers()
    {
        return _stickerRepo.GetAll();
    }

    public List<Sticker> Get(GetStickersDTO filters)
    {
        return _stickerRepo.Get(filters.ToPredicate(), filters.Page, filters.PageSize).ToList();
    }

    public void CreateIfNonExistent(Sticker sticker)
    {
        if (_stickerRepo.Exists(sticker))
        {
            return;
        }
        _teamRepo.CreateIfNonExistent(new Team { Description = sticker.Team });
        _nationalTeamRepo.CreateIfNonExistent(new NationalTeam { Description = sticker.NationalTeam });
        _categoryRepo.CreateIfNonExistent(new Category { Description = sticker.Category });
        _stickerRepo.Add(sticker);
    }
}
