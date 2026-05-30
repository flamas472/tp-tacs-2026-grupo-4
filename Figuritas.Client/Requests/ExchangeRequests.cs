using Figuritas.Shared.Model;
using Figuritas.Shared.DTO;
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

        public async Task<ApiResponse<Rate>> RateExchangeAsync(int exchangeId, PostRateDTO dto, string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"api/exchange/{exchangeId}/rate");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                request.Content = JsonContent.Create(dto);

                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<Rate>();
                    return data is not null
                        ? ApiResponse<Rate>.Ok(data)
                        : ApiResponse<Rate>.Fail("No se pudo leer la calificación creada.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<Rate>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<Rate>.Fail($"Error de conexión: {ex.Message}");
            }
        }
    }
}
