using Figuritas.Shared.Model;

namespace Figuritas.Api.Services;

public class NationalTeamService
{
    private readonly NationalTeamRepository _nationalTeamRepository;

    public NationalTeamService(NationalTeamRepository nationalTeamRepository)
    {
        _nationalTeamRepository = nationalTeamRepository;
    }

    public List<NationalTeam> GetAllNationalTeams()
    {
        return _nationalTeamRepository.GetAll();
    }
}
