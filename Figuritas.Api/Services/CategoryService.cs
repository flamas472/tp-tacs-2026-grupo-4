using Figuritas.Api.Repositories;
using Figuritas.Shared.Model;

namespace Figuritas.Api.Services;

public class CategoryService
{
    private readonly ICategoryRepository _categoryRepository;

    public CategoryService(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public List<Category> GetAllCategories()
    {
        return _categoryRepository.GetAll();
    }
}
