using Figuritas.Shared.Model;

namespace Figuritas.Api.Services;

public class CategoryService
{
    private readonly CategoryRepository _categoryRepository;

    public CategoryService(CategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public List<Category> GetAllCategories()
    {
        return _categoryRepository.GetAll();
    }
}
