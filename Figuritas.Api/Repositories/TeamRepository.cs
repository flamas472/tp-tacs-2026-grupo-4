using System.Collections.Concurrent;
using Figuritas.Shared.Model;

// Repo for in-memory persistence of teams.
public class TeamRepository
{
    private readonly ConcurrentBag<Team> Teams = new();
    private int nextId = 1;

    public TeamRepository()
    {
        Teams.Add(new Team { Id = 0, Description = string.Empty });
        Add(new Team { Description = "Boca" });
        Add(new Team { Description = "River" });
        Add(new Team { Description = "Morón" });
    }

    public Team? GetByDescription(string description) => Teams.FirstOrDefault(t => t.Description.Equals(description, StringComparison.OrdinalIgnoreCase));
    

    public List<Team> GetAll()
    {
        return Teams.ToList();
    }

    public void Add(Team team)
    {
        team.Id = Interlocked.Increment(ref nextId) - 1;
        Teams.Add(team);
    }

    public void CreateIfNonExistent(Team team)
    {
        if (!Teams.Any(t => t.Equals(team)))
        {
            return;
        }
        Add(team);
    }

    public Team? GetById(int id) => Teams.FirstOrDefault(a => a.Id == id);
}
