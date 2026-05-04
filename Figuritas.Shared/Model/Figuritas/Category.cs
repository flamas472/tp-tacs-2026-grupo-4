namespace Figuritas.Shared.Model;

public class Category
{
    public int Id { get; set; }

    public required string Description { get; set; }

    public bool Equals(Category category)
    {
        return Description == category.Description;
    }
}
