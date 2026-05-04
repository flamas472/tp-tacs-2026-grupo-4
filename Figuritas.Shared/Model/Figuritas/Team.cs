namespace Figuritas.Shared.Model;

public class Team
{
    public int Id { get; set; }

    public required string Description { get; set; }

    public bool Equals(Team team)
    {
        return Description == team.Description;
    }
}
