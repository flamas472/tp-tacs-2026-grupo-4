using System.Collections.Concurrent;
using Figuritas.Shared.Model;

// Repo for in-memory persistence of national teams.
public class NationalTeamRepository
{
    private readonly ConcurrentBag<NationalTeam> NationalTeams = new();
    private int nextId = 1;

    public NationalTeamRepository()
    {
        Add(new NationalTeam { Description = "Argentina" });
        Add(new NationalTeam { Description = "Francia" });
        Add(new NationalTeam { Description = "Brasil" });
    }

    public List<NationalTeam> GetAll()
    {
        return NationalTeams.ToList();
    }

    public void Add(NationalTeam nationalTeam)
    {
        nationalTeam.Id = Interlocked.Increment(ref nextId) - 1;
        NationalTeams.Add(nationalTeam);
    }

    public NationalTeam? GetById(int id) => NationalTeams.FirstOrDefault(a => a.Id == id);
}
