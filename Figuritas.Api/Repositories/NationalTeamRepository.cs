using Figuritas.Shared.Model;
using MongoDB.Driver;

namespace Figuritas.Api.Repositories;

public class NationalTeamRepository : INationalTeamRepository
{
    private readonly IMongoCollection<NationalTeam> _nationalTeams;
    private readonly IIdGenerator _idGenerator;

    public NationalTeamRepository(MongoDbContext context, IIdGenerator idGenerator)
    {
        _nationalTeams = context.Collection<NationalTeam>("NationalTeams");
        _idGenerator = idGenerator;
        SeedDefaultNationalTeams();
    }

    private void SeedDefaultNationalTeams()
    {
        CreateIfNonExistent(new NationalTeam { Description = "Argentina" });
        CreateIfNonExistent(new NationalTeam { Description = "Francia" });
        CreateIfNonExistent(new NationalTeam { Description = "Brasil" });
    }

    public void CreateIfNonExistent(NationalTeam nationalTeam)
    {
        if (_nationalTeams.Find(t => t.Description == nationalTeam.Description).Any())
        {
            return;
        }

        Add(nationalTeam);
    }

    public NationalTeam? GetByDescription(string description)
    {
        return _nationalTeams.Find(_ => true).ToList().FirstOrDefault(t => t.Description.Equals(description, StringComparison.OrdinalIgnoreCase));
    }

    public List<NationalTeam> GetAll()
    {
        return _nationalTeams.Find(_ => true).ToList();
    }

    public void Add(NationalTeam nationalTeam)
    {
        nationalTeam.Id = _idGenerator.GetNextId<NationalTeam>();
        _nationalTeams.InsertOne(nationalTeam);
    }

    public NationalTeam? GetById(int id) => _nationalTeams.Find(a => a.Id == id).FirstOrDefault();
}
