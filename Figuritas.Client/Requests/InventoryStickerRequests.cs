using Figuritas.Shared.Model;
using Figuritas.Shared.Responses;
using Figuritas.Client.Extensions;

namespace Figuritas.Client.Requests
{
    public class InventoryStickerHttpClient
    {
        private readonly HttpClient _http;

        public InventoryStickerHttpClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<ApiResponse<List<UserSticker>>> GetInventoryStickersAsync(
            int page, int pageSize,
            string? nationalTeam = null,
            string? category = null,
            bool? canBeExchanged = null)
        {
            string url = $"api/inventorystickers?Page={page}&PageSize={pageSize}";

            if (!string.IsNullOrEmpty(nationalTeam))
                url += $"&NationalTeam={nationalTeam}";

            if (!string.IsNullOrEmpty(category))
                url += $"&Category={category}";

            if (canBeExchanged.HasValue)
                url += $"&CanBeExchanged={canBeExchanged.Value}";

            try
            {
                var response = await _http.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<UserSticker>>();
                    return ApiResponse<List<UserSticker>>.Ok(data ?? new List<UserSticker>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<UserSticker>>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<UserSticker>>.Fail($"Error de conexión: {ex.Message}");
            }
        }
    }
}
