using Figuritas.Shared.Model;
using MongoDB.Driver;

namespace Figuritas.Api.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly IMongoCollection<Category> _categories;
    private readonly IIdGenerator _idGenerator;

    public CategoryRepository(MongoDbContext context, IIdGenerator idGenerator)
    {
        _categories = context.Collection<Category>("Categories");
        _idGenerator = idGenerator;
        SeedDefaultCategories();
    }

    private void SeedDefaultCategories()
    {
        CreateIfNonExistent(new Category { Description = "Jugador" });
        CreateIfNonExistent(new Category { Description = "Escudo" });
        CreateIfNonExistent(new Category { Description = "Estadio" });
    }

    public List<Category> GetAll()
    {
        return _categories.Find(_ => true).ToList();
    }

    public void Add(Category category)
    {
        category.Id = _idGenerator.GetNextId<Category>();
        _categories.InsertOne(category);
    }

    public void CreateIfNonExistent(Category category)
    {
        if (_categories.Find(c => c.Description == category.Description).Any())
        {
            return;
        }

        Add(category);
    }

    public Category? GetByDescription(string description) => _categories.Find(c => c.Description == description).FirstOrDefault();

    public Category? GetById(int id) => _categories.Find(c => c.Id == id).FirstOrDefault();
}
