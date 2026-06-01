using Figuritas.Shared.DTO;
using Figuritas.Shared.DTO.response;
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

        public async Task<ApiResponse<UserResponseDTO>> RegisterAsync(PostUserDTO dto)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("api/auth/register", dto);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<UserResponseDTO>();
                    return data is not null
                        ? ApiResponse<UserResponseDTO>.Ok(data)
                        : ApiResponse<UserResponseDTO>.Fail("No se pudo leer el usuario registrado.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<UserResponseDTO>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<UserResponseDTO>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> LogoutAsync()
        {
            try
            {
                var response = await _http.PostAsync("api/auth/logout", null);

                if (response.IsSuccessStatusCode)
                    return ApiResponse<bool>.Ok(true);

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<bool>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail($"Error de conexión: {ex.Message}");
            }
        }
    }
}
