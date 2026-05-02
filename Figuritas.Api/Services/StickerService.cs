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

    public List<Sticker> GetStickersByNationalTeam(NationalTeam nationalTeam)
    {
        return _stickerRepo.GetAllByNationalTeam(nationalTeam);
    }

    public List<Sticker> GetStickersByCategory(Category category)
    {
        return _stickerRepo.GetAllByCategory(category);
    }

    public List<Sticker> GetStickersByPlayer(string playerName)
    {
        return _stickerRepo.GetAllByPlayer(playerName);
    }

}