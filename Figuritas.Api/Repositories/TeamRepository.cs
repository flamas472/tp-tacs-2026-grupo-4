using Figuritas.Shared.Model;
using MongoDB.Driver;

public class TeamRepository
{
    private readonly IMongoCollection<Team> _teams;
    private readonly IIdGenerator _idGenerator;

    public TeamRepository(MongoDbContext context, IIdGenerator idGenerator)
    {
        _teams = context.Collection<Team>("Teams");
        _idGenerator = idGenerator;
        SeedDefaultTeams();
    }

    private void SeedDefaultTeams()
    {
        CreateIfNonExistent(new Team { Description = string.Empty });
        CreateIfNonExistent(new Team { Description = "Boca" });
        CreateIfNonExistent(new Team { Description = "River" });
        CreateIfNonExistent(new Team { Description = "Morón" });
    }

    public Team? GetByDescription(string description)
    {
        return _teams.Find(_ => true).ToList().FirstOrDefault(t => t.Description.Equals(description, StringComparison.OrdinalIgnoreCase));
    }
    
    public List<Team> GetAll()
    {
        return _teams.Find(_ => true).ToList();
    }

    public void Add(Team team)
    {
        team.Id = _idGenerator.GetNextId<Team>();
        _teams.InsertOne(team);
    }

    public void CreateIfNonExistent(Team team)
    {
        if (_teams.Find(t => t.Description == team.Description).Any())
        {
            return;
        }

        Add(team);
    }

    public Team? GetById(int id) => _teams.Find(a => a.Id == id).FirstOrDefault();
}
