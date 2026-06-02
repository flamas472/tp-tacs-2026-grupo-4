using Figuritas.Api.Repositories;
using Figuritas.Shared.Model;

namespace Figuritas.Api.Services;

public class TeamService
{
    private readonly ITeamRepository _teamRepository;

    public TeamService(ITeamRepository teamRepository)
    {
        _teamRepository = teamRepository;
    }

    public List<Team> GetAllTeams()
    {
        return _teamRepository.GetAll();
    }
}
