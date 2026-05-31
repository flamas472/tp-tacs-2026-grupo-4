using Figuritas.Shared.Model;

namespace Figuritas.Api.Repositories;

public interface ITeamRepository
{
    Team? GetByDescription(string description);
    List<Team> GetAll();
    void Add(Team team);
    void CreateIfNonExistent(Team team);
    Team? GetById(int id);
}
