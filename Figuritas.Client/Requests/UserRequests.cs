using Figuritas.Shared.Model;
using Figuritas.Shared.DTO;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Responses;
using Figuritas.Client.Extensions;
using System.Net.Http.Json;
using System.Text;

namespace Figuritas.Client.Requests
{
    public class UserHttpClient
    {
        private readonly HttpClient _http;

        public UserHttpClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<ApiResponse<List<UserResponseDTO>>> GetUsersAsync()
        {
            try
            {
                var response = await _http.GetAsync("api/users");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<UserResponseDTO>>();
                    return ApiResponse<List<UserResponseDTO>>.Ok(data ?? new List<UserResponseDTO>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<UserResponseDTO>>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<UserResponseDTO>>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<UserResponseDTO>> CreateUserAsync(PostUserDTO dto)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("api/users", dto);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<UserResponseDTO>();
                    return data is not null
                        ? ApiResponse<UserResponseDTO>.Ok(data)
                        : ApiResponse<UserResponseDTO>.Fail("No se pudo leer el usuario creado.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<UserResponseDTO>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<UserResponseDTO>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<UserResponseDTO>> UpdateUserAsync(int id, PatchUserDTO dto)
        {
            try
            {
                var response = await _http.PatchAsJsonAsync($"api/users/{id}", dto);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<UserResponseDTO>();
                    return data is not null
                        ? ApiResponse<UserResponseDTO>.Ok(data)
                        : ApiResponse<UserResponseDTO>.Fail("No se pudo leer el usuario actualizado.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<UserResponseDTO>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<UserResponseDTO>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<MarketStickerResponseDTO>>> GetUserStickersAsync(
            int userId,
            int page = 1,
            int pageSize = 20,
            string? nationalTeam = null,
            string? category = null,
            int? number = null,
            string? description = null,
            bool? canBeDirectlyExchanged = null,
            bool? canBeAuctioned = null)
        {
            var url = new StringBuilder($"api/users/{userId}/stickers?Page={page}&PageSize={pageSize}");
            if (!string.IsNullOrEmpty(nationalTeam)) url.Append($"&NationalTeam={Uri.EscapeDataString(nationalTeam)}");
            if (!string.IsNullOrEmpty(category)) url.Append($"&Category={Uri.EscapeDataString(category)}");
            if (number.HasValue) url.Append($"&Number={number.Value}");
            if (!string.IsNullOrEmpty(description)) url.Append($"&Description={Uri.EscapeDataString(description)}");
            if (canBeDirectlyExchanged.HasValue) url.Append($"&CanBeDirectlyExchanged={canBeDirectlyExchanged.Value}");
            if (canBeAuctioned.HasValue) url.Append($"&CanBeAuctioned={canBeAuctioned.Value}");

            try
            {
                var response = await _http.GetAsync(url.ToString());

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

        public async Task<ApiResponse<UserStickerResponseDTO>> AddUserStickerAsync(int userId, PostUserStickerRequestDTO dto)
        {
            try
            {
                var response = await _http.PostAsJsonAsync($"api/users/{userId}/stickers", dto);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<UserStickerResponseDTO>();
                    return ApiResponse<UserStickerResponseDTO>.Ok(data ?? new UserStickerResponseDTO());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<UserStickerResponseDTO>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<UserStickerResponseDTO>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<UserStickerResponseDTO>> UpdateUserStickerAsync(int userId, int stickerId, PatchUserStickerDTO dto)
        {
            try
            {
                var response = await _http.PatchAsJsonAsync($"api/users/{userId}/stickers/{stickerId}", dto);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<UserStickerResponseDTO>();
                    return ApiResponse<UserStickerResponseDTO>.Ok(data ?? new UserStickerResponseDTO());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<UserStickerResponseDTO>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<UserStickerResponseDTO>.Fail($"Error de conexión: {ex.Message}");
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

        public async Task<ApiResponse<MissingSticker>> AddMissingStickerAsync(int userId, PostMissingStickerRequestDTO dto)
        {
            try
            {
                var response = await _http.PostAsJsonAsync($"api/users/{userId}/missing-stickers", dto);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<MissingSticker>();
                    return data is not null
                        ? ApiResponse<MissingSticker>.Ok(data)
                        : ApiResponse<MissingSticker>.Fail("No se pudo leer la figurita faltante agregada.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<MissingSticker>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<MissingSticker>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<MissingSticker>>> GetMissingStickersAsync(int userId)
        {
            try
            {
                var response = await _http.GetAsync($"api/users/{userId}/missing-stickers");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<MissingSticker>>();
                    return ApiResponse<List<MissingSticker>>.Ok(data ?? new List<MissingSticker>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<MissingSticker>>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<MissingSticker>>.Fail($"Error de conexión: {ex.Message}");
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

        public async Task<ApiResponse<List<RatingResponseDTO>>> GetUserRatingsAsync(int userId)
        {
            try
            {
                var response = await _http.GetAsync($"api/users/{userId}/ratings");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<RatingResponseDTO>>();
                    return ApiResponse<List<RatingResponseDTO>>.Ok(data ?? new List<RatingResponseDTO>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<RatingResponseDTO>>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<RatingResponseDTO>>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<UserResponseDTO>> GetUserByIdAsync(int id)
        {
            try
            {
                var response = await _http.GetAsync($"api/users/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<UserResponseDTO>();
                    return data is not null
                        ? ApiResponse<UserResponseDTO>.Ok(data)
                        : ApiResponse<UserResponseDTO>.Fail("No se pudo leer el usuario.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<UserResponseDTO>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<UserResponseDTO>.Fail($"Error de conexión: {ex.Message}");
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
