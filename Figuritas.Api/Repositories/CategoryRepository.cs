using System.Collections.Concurrent;
using Figuritas.Shared.Model;

// Repo for in-memory persistence of categories.
public class CategoryRepository
{
    private readonly ConcurrentBag<Category> Categories = new();
    private int nextId = 1;

    public CategoryRepository()
    {
        Add(new Category { Description = "Jugador" });
        Add(new Category { Description = "Escudo" });
        Add(new Category { Description = "Estadio" });
    }

    public List<Category> GetAll()
    {
        return Categories.ToList();
    }

    public void Add(Category category)
    {
        category.Id = Interlocked.Increment(ref nextId) - 1;
        Categories.Add(category);
    }

    public Category? GetById(int id) => Categories.FirstOrDefault(a => a.Id == id);
}
