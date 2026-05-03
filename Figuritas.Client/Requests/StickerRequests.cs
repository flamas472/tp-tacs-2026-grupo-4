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

        public async Task<ApiResponse<List<Sticker>>> GetStickersAsync(int? nationalTeamId = null, int? categoryId = null)
        {
            string url = "api/Stickers";
            
            if (nationalTeamId.HasValue && nationalTeamId > 0)
            {
                url += $"?nationalTeamId={nationalTeamId}";
            }
            
            if (categoryId.HasValue && categoryId > 0)
            {
                url += $"?categoryId={categoryId}";
            }

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
    }
}