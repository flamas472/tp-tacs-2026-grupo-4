using Figuritas.Shared.Model;

namespace Figuritas.Api.Repositories;

public interface ICategoryRepository
{
    List<Category> GetAll();
    void Add(Category category);
    void CreateIfNonExistent(Category category);
    Category? GetByDescription(string description);
    Category? GetById(int id);
}
