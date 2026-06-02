using Figuritas.Api.Repositories;
using Figuritas.Shared.Model;

namespace Figuritas.Api.Services;

public class NationalTeamService
{
    private readonly INationalTeamRepository _nationalTeamRepository;

    public NationalTeamService(INationalTeamRepository nationalTeamRepository)
    {
        _nationalTeamRepository = nationalTeamRepository;
    }

    public List<NationalTeam> GetAllNationalTeams()
    {
        return _nationalTeamRepository.GetAll();
    }
}
