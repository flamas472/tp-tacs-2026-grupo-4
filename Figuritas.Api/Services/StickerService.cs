using Figuritas.Shared.DTO;
using Figuritas.Shared.Model;
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
        return _stickerRepo.Get(ToPredicate(filters), filters.Page, filters.PageSize).ToList();
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

    public Func<Sticker, bool> ToPredicate(GetStickersDto dto)
    {
        return sticker => 
            (dto.Number == null || sticker.Number == dto.Number) &&
            AllWordsAreContainedBy(dto.Team, sticker.Team) &&
            AllWordsAreContainedBy(dto.NationalTeam, sticker.NationalTeam) &&
            AllWordsAreContainedBy(dto.Category, sticker.Category) &&
            AllWordsAreContainedBy(dto.Description, sticker.Description);
    }

    //TODO MOVER A UTILS
     /* 
     Checks if origin contains all the words contained in filter in any order.
     eg.: filter = "Hola Mundo"; origin = "Mundo Hola Pan"; is true
     eg.: filer = "Hola Mundo"; origin = "Hola Pan"; is false
      */
    private static bool AllWordsAreContainedBy(string? filter, string origin)
    {
        return string.IsNullOrEmpty(filter) || filter.Split(" ").All(words => origin.Contains(words, StringComparison.OrdinalIgnoreCase));
    }
    

}