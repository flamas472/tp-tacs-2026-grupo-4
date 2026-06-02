using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Responses;
using Figuritas.Client.Extensions;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Figuritas.Client.Requests
{
    public class ExchangeHttpClient
    {
        private readonly HttpClient _http;

        public ExchangeHttpClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<ApiResponse<RatingResponseDTO>> CreateRatingAsync(PostRatingRequestDTO dto, string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "api/ratings");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                request.Content = JsonContent.Create(dto);

                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<RatingResponseDTO>();
                    return data is not null
                        ? ApiResponse<RatingResponseDTO>.Ok(data)
                        : ApiResponse<RatingResponseDTO>.Fail("No se pudo leer la calificación creada.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<RatingResponseDTO>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<RatingResponseDTO>.Fail($"Error de conexión: {ex.Message}");
            }
        }
    }
}
