using Figuritas.Shared.DTO;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Responses;
using Figuritas.Client.Extensions;
using System.Net.Http.Json;

namespace Figuritas.Client.Requests
{
    public class AuthHttpClient
    {
        private readonly HttpClient _http;

        public AuthHttpClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<ApiResponse<LoginResponseDTO>> LoginAsync(LoginRequestDTO dto)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("api/auth/login", dto);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<LoginResponseDTO>();
                    return data is not null
                        ? ApiResponse<LoginResponseDTO>.Ok(data)
                        : ApiResponse<LoginResponseDTO>.Fail("No se pudo leer el token de la respuesta.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<LoginResponseDTO>.Fail(HttpExtensions.GetFriendlyErrorMessage(response.StatusCode, errorMsg));
            }
            catch (Exception)
            {
                return ApiResponse<LoginResponseDTO>.Fail(HttpExtensions.GetFriendlyConnectionError());
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
                return ApiResponse<UserResponseDTO>.Fail(HttpExtensions.GetFriendlyErrorMessage(response.StatusCode, errorMsg));
            }
            catch (Exception)
            {
                return ApiResponse<UserResponseDTO>.Fail(HttpExtensions.GetFriendlyConnectionError());
            }
        }

        public async Task<ApiResponse<bool>> LogoutAsync(string refreshToken)
        {
            try
            {
                var dto = new RefreshTokenRequestDTO { RefreshToken = refreshToken };
                var response = await _http.PostAsJsonAsync("api/auth/logout", dto);

                if (response.IsSuccessStatusCode)
                    return ApiResponse<bool>.Ok(true);

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<bool>.Fail(HttpExtensions.GetFriendlyErrorMessage(response.StatusCode, errorMsg));
            }
            catch (Exception)
            {
                return ApiResponse<bool>.Fail(HttpExtensions.GetFriendlyConnectionError());
            }
        }
    }
}
