using Figuritas.Shared.Model;

namespace Figuritas.Api.Services;

public class TeamService
{
    private readonly TeamRepository _teamRepository;

    public TeamService(TeamRepository teamRepository)
    {
        _teamRepository = teamRepository;
    }

    public List<Team> GetAllTeams()
    {
        return _teamRepository.GetAll();
    }
}
