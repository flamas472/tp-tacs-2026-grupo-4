using Figuritas.Shared.Model;
using Figuritas.Shared.DTO;
using Figuritas.Shared.Responses;
using Figuritas.Client.Extensions;
using System.Net.Http.Json;

namespace Figuritas.Client.Requests
{
    public class UserHttpClient
    {
        private readonly HttpClient _http;

        public UserHttpClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<ApiResponse<List<User>>> GetUsersAsync()
        {
            try
            {
                var response = await _http.GetAsync("api/users");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<User>>();
                    return ApiResponse<List<User>>.Ok(data ?? new List<User>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<User>>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<User>>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<User>> CreateUserAsync(PostUserDTO dto)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("api/users", dto);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<User>();
                    return data is not null
                        ? ApiResponse<User>.Ok(data)
                        : ApiResponse<User>.Fail("No se pudo leer el usuario creado.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<User>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<User>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<User>> UpdateUserAsync(int id, PatchUserDTO dto)
        {
            try
            {
                var response = await _http.PatchAsJsonAsync($"api/users/{id}", dto);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<User>();
                    return data is not null
                        ? ApiResponse<User>.Ok(data)
                        : ApiResponse<User>.Fail("No se pudo leer el usuario actualizado.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<User>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<User>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<UserSticker>>> GetUserStickersAsync(int userId)
        {
            try
            {
                var response = await _http.GetAsync($"api/users/{userId}/stickers");

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

        public async Task<ApiResponse<UserSticker>> AddUserStickerAsync(int userId, PostUserStickerRequestDTO dto)
        {
            try
            {
                var response = await _http.PostAsJsonAsync($"api/users/{userId}/stickers", dto);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<UserSticker>();
                    return data is not null
                        ? ApiResponse<UserSticker>.Ok(data)
                        : ApiResponse<UserSticker>.Fail("No se pudo leer la figurita agregada.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<UserSticker>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<UserSticker>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<UserSticker>> UpdateUserStickerAsync(int userId, int stickerId, PatchUserStickerDto dto)
        {
            try
            {
                var response = await _http.PatchAsJsonAsync($"api/users/{userId}/stickers/{stickerId}", dto);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<UserSticker>();
                    return data is not null
                        ? ApiResponse<UserSticker>.Ok(data)
                        : ApiResponse<UserSticker>.Fail("No se pudo leer la figurita actualizada.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<UserSticker>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<UserSticker>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> DeleteUserStickerAsync(int userId, int stickerId)
        {
            try
            {
                var response = await _http.DeleteAsync($"api/users/{userId}/stickers/{stickerId}");

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

        public async Task<ApiResponse<List<Sticker>>> AddMissingStickerAsync(int userId, PostMissingStickerRequestDTO dto)
        {
            try
            {
                var response = await _http.PostAsJsonAsync($"api/users/{userId}/missing-stickers", dto);

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

        public async Task<ApiResponse<List<Sticker>>> GetMissingStickersAsync(
            int userId, int page, int pageSize, string? nationalTeam = null, string? category = null)
        {
            try
            {
                var url = $"api/users/{userId}/missing-stickers?page={page}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(nationalTeam)) url += $"&nationalTeam={Uri.EscapeDataString(nationalTeam)}";
                if (!string.IsNullOrEmpty(category)) url += $"&category={Uri.EscapeDataString(category)}";

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

        public async Task<ApiResponse<bool>> DeleteMissingStickerAsync(int userId, int stickerId)
        {
            try
            {
                var response = await _http.DeleteAsync($"api/users/{userId}/missing-stickers/{stickerId}");
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

        public async Task<ApiResponse<List<Rate>>> GetUserRatingsAsync(int userId)
        {
            try
            {
                var response = await _http.GetAsync($"api/users/{userId}/ratings");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<Rate>>();
                    return ApiResponse<List<Rate>>.Ok(data ?? new List<Rate>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<Rate>>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<Rate>>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<double>> GetUserReputationAsync(int userId)
        {
            try
            {
                var response = await _http.GetAsync($"api/users/{userId}/reputation");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<double>();
                    return ApiResponse<double>.Ok(data);
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<double>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<double>.Fail($"Error de conexión: {ex.Message}");
            }
        }
    }
}
