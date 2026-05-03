using Figuritas.Shared.Model;

namespace Figuritas.Api.Services;

public class StickerService
{
    private readonly StickerRepository _stickerRepo;
    
    public StickerService(StickerRepository stickerRepo)
    {
        _stickerRepo = stickerRepo;
    }

    public List<Sticker> GetAllStickers()
    {
        return _stickerRepo.GetAll();
    }

    public Sticker? GetStickersByNumber(int number)
    {
        return _stickerRepo.GetByNumber(number);
    }

    public List<Sticker> GetStickersByTeam(Team team)
    {
        return _stickerRepo.GetAllByTeam(team);
    }

    public List<Sticker> GetStickersByNationalTeamId(int nationalTeamId)
    {
        return _stickerRepo.GetAllByNationalTeamId(nationalTeamId);
    }

    public List<Sticker> GetStickersByCategoryId(int categoryId)
    {
        return _stickerRepo.GetAllByCategoryId(categoryId);
    }

    public List<Sticker> GetStickersByDescription(string description)
    {
        return _stickerRepo.GetAllByDescription(description);
    }

}