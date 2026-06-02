using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Responses;
using Figuritas.Client.Extensions;
using System.Net.Http.Json;

namespace Figuritas.Client.Requests
{
    public class AdminHttpClient
    {
        private readonly HttpClient _http;

        public AdminHttpClient(HttpClient http)
        {
            _http = http;
        }

        /// <summary>
        /// Returns a paginated list of all platform users.
        /// Requires backend endpoint: GET /api/admin/users?page={page}&pageSize={pageSize}
        /// Restricted to Admin and SuperAdmin.
        /// </summary>
        public async Task<ApiResponse<List<UserResponseDTO>>> GetAllUsersAsync(
            int page = 1, int pageSize = 20)
        {
            try
            {
                var response = await _http.GetAsync(
                    $"api/admin/users?page={page}&pageSize={pageSize}");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<UserResponseDTO>>();
                    return ApiResponse<List<UserResponseDTO>>.Ok(
                        data ?? new List<UserResponseDTO>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<UserResponseDTO>>.Fail(
                    $"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<UserResponseDTO>>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        /// <summary>
        /// Bans the user with the given ID.
        /// Requires backend endpoint: POST /api/admin/users/{userId}/ban
        /// Restricted to Admin and SuperAdmin.
        /// </summary>
        public async Task<ApiResponse<bool>> BanUserAsync(int userId)
        {
            try
            {
                var response = await _http.PostAsync(
                    $"api/admin/users/{userId}/ban", null);

                if (response.IsSuccessStatusCode)
                    return ApiResponse<bool>.Ok(true);

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<bool>.Fail(
                    $"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        /// <summary>
        /// Unbans the user with the given ID.
        /// Requires backend endpoint: POST /api/admin/users/{userId}/unban
        /// Restricted to Admin and SuperAdmin.
        /// </summary>
        public async Task<ApiResponse<bool>> UnbanUserAsync(int userId)
        {
            try
            {
                var response = await _http.PostAsync(
                    $"api/admin/users/{userId}/unban", null);

                if (response.IsSuccessStatusCode)
                    return ApiResponse<bool>.Ok(true);

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<bool>.Fail(
                    $"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<PlatformSummaryResponseDTO>> GetAnalyticsSummaryAsync()
        {
            try
            {
                var response = await _http.GetAsync("api/admin/analytics/summary");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<PlatformSummaryResponseDTO>();
                    return data is not null
                        ? ApiResponse<PlatformSummaryResponseDTO>.Ok(data)
                        : ApiResponse<PlatformSummaryResponseDTO>.Fail("No se pudo leer el resumen de estadísticas.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<PlatformSummaryResponseDTO>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<PlatformSummaryResponseDTO>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<AdminUserResponseDTO>> CreateAdminAsync(CreateAdminRequestDTO dto)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("api/admin/admins", dto);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<AdminUserResponseDTO>();
                    return data is not null
                        ? ApiResponse<AdminUserResponseDTO>.Ok(data)
                        : ApiResponse<AdminUserResponseDTO>.Fail("No se pudo leer el administrador creado.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<AdminUserResponseDTO>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<AdminUserResponseDTO>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<AdminUserResponseDTO>>> GetAdminsAsync(
            int page = 1, int pageSize = 20)
        {
            try
            {
                var response = await _http.GetAsync(
                    $"api/admin/admins?page={page}&pageSize={pageSize}");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<AdminUserResponseDTO>>();
                    return ApiResponse<List<AdminUserResponseDTO>>.Ok(
                        data ?? new List<AdminUserResponseDTO>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<AdminUserResponseDTO>>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<AdminUserResponseDTO>>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<AdminUserResponseDTO>> PatchAdminRoleAsync(
            int id, PatchAdminRoleRequestDTO dto)
        {
            try
            {
                var response = await _http.PatchAsJsonAsync($"api/admin/admins/{id}/role", dto);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<AdminUserResponseDTO>();
                    return data is not null
                        ? ApiResponse<AdminUserResponseDTO>.Ok(data)
                        : ApiResponse<AdminUserResponseDTO>.Fail("No se pudo leer el administrador actualizado.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<AdminUserResponseDTO>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<AdminUserResponseDTO>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        /// <summary>
        /// Revokes admin privileges from an Admin-role user, demoting them to regular User.
        /// Calls DELETE /api/admin/admins/{id}/role.
        /// Restricted to SuperAdmin.
        /// </summary>
        public async Task<ApiResponse<bool>> RevokeAdminRoleAsync(int id)
        {
            try
            {
                var response = await _http.DeleteAsync($"api/admin/admins/{id}/role");

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
