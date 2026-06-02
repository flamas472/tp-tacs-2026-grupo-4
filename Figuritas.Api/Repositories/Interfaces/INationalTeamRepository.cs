using Figuritas.Shared.Model;

namespace Figuritas.Api.Repositories;

public interface INationalTeamRepository
{
    void CreateIfNonExistent(NationalTeam nationalTeam);
    NationalTeam? GetByDescription(string description);
    List<NationalTeam> GetAll();
    void Add(NationalTeam nationalTeam);
    NationalTeam? GetById(int id);
}
