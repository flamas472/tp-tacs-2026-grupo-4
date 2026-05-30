using Figuritas.Shared.DTO;
using Figuritas.Shared.Responses;
using Figuritas.Client.Extensions;
using System.Net.Http.Json;

namespace Figuritas.Client.Requests
{
    public record LoginResponse(string Token);

    public class AuthHttpClient
    {
        private readonly HttpClient _http;

        public AuthHttpClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<ApiResponse<LoginResponse>> LoginAsync(PostUserDTO dto)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("api/auth/login", dto);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<LoginResponse>();
                    return data is not null
                        ? ApiResponse<LoginResponse>.Ok(data)
                        : ApiResponse<LoginResponse>.Fail("No se pudo leer el token de la respuesta.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<LoginResponse>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<LoginResponse>.Fail($"Error de conexión: {ex.Message}");
            }
        }
    }
}
