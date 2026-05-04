namespace Figuritas.Shared.Model;

public class NationalTeam
{
    public int Id { get; set; }

    public required string Description { get; set; }

    public bool Equals(NationalTeam nationalTeam)
    {
        return Description == nationalTeam.Description;
    }
}
