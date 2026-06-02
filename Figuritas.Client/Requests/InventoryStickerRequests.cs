using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Responses;
using Figuritas.Client.Extensions;
using System.Net.Http.Json;

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

        public async Task<ApiResponse<List<ExchangeSuggestionResponseDTO>>> GetSuggestionsAsync(
            int page = 1, int pageSize = 20)
        {
            try
            {
                var response = await _http.GetAsync(
                    $"api/market/suggestions?Page={page}&PageSize={pageSize}");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<ExchangeSuggestionResponseDTO>>();
                    return ApiResponse<List<ExchangeSuggestionResponseDTO>>.Ok(
                        data ?? new List<ExchangeSuggestionResponseDTO>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<ExchangeSuggestionResponseDTO>>.Fail(
                    $"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ExchangeSuggestionResponseDTO>>.Fail(
                    $"Error de conexión: {ex.Message}");
            }
        }
    }
}
