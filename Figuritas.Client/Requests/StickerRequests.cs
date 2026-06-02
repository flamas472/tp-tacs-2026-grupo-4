using Figuritas.Shared.Model;
using Figuritas.Shared.Responses;
using Figuritas.Client.Extensions;
using System.Net.Http.Json;

namespace Figuritas.Client.Requests
{
    public class StickerHttpClient
    {
        private readonly HttpClient _http;

        public StickerHttpClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<ApiResponse<List<Sticker>>> GetStickersAsync(
            int page, int pageSize,
            string? nationalTeam = null,
            string? category = null,
            int? number = null,
            string? description = null)
        {
            string url = $"api/Stickers?Page={page}&PageSize={pageSize}";

            if (!string.IsNullOrEmpty(nationalTeam))
                url += $"&nationalTeam={Uri.EscapeDataString(nationalTeam)}";

            if (!string.IsNullOrEmpty(category))
                url += $"&category={Uri.EscapeDataString(category)}";

            if (number.HasValue)
                url += $"&number={number.Value}";

            if (!string.IsNullOrEmpty(description))
                url += $"&description={Uri.EscapeDataString(description)}";

            try
            {
                var response = await _http.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<Sticker>>();
                    return ApiResponse<List<Sticker>>.Ok(data ?? new List<Sticker>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<Sticker>>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<Sticker>>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<Sticker>> GetByIdAsync(int id)
        {
            try
            {
                var response = await _http.GetAsync($"api/Stickers/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<Sticker>();
                    return data is not null
                        ? ApiResponse<Sticker>.Ok(data)
                        : ApiResponse<Sticker>.Fail("No se pudo leer la figurita.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<Sticker>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<Sticker>.Fail($"Error de conexión: {ex.Message}");
            }
        }
    }
}