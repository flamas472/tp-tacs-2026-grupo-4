using Figuritas.Shared.Model;
using Figuritas.Shared.Responses;
using Figuritas.Client.Extensions;

namespace Figuritas.Client.Requests
{
    public class CategoryHttpClient
    {
        private readonly HttpClient _http;

        public CategoryHttpClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<ApiResponse<List<Category>>> GetCategoriesAsync()
        {
            try
            {
                var response = await _http.GetAsync("api/category");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<Category>>();
                    return ApiResponse<List<Category>>.Ok(data ?? new List<Category>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<Category>>.Fail(HttpExtensions.GetFriendlyErrorMessage(response.StatusCode, errorMsg));
            }
            catch (Exception)
            {
                return ApiResponse<List<Category>>.Fail(HttpExtensions.GetFriendlyConnectionError());
            }
        }
    }
}
