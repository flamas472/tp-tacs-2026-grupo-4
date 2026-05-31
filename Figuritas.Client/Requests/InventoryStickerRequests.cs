using Figuritas.Shared.DTO.response;
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

        public async Task<ApiResponse<List<MarketStickerResponseDTO>>> GetMarketStickersAsync(
            int page = 1,
            int pageSize = 20,
            string? nationalTeam = null,
            string? category = null)
        {
            string url = $"api/market/stickers?Page={page}&PageSize={pageSize}";

            if (!string.IsNullOrEmpty(nationalTeam))
                url += $"&NationalTeam={Uri.EscapeDataString(nationalTeam)}";

            if (!string.IsNullOrEmpty(category))
                url += $"&Category={Uri.EscapeDataString(category)}";

            try
            {
                var response = await _http.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<MarketStickerResponseDTO>>();
                    return ApiResponse<List<MarketStickerResponseDTO>>.Ok(data ?? new List<MarketStickerResponseDTO>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<MarketStickerResponseDTO>>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<MarketStickerResponseDTO>>.Fail($"Error de conexión: {ex.Message}");
            }
        }
    }
}
