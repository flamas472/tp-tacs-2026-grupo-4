using Figuritas.Shared.Model;
using Figuritas.Shared.Responses;
using Figuritas.Client.Extensions;

namespace Figuritas.Client.Requests
{
    public class TeamHttpClient
    {
        private readonly HttpClient _http;

        public TeamHttpClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<ApiResponse<List<Team>>> GetTeamsAsync()
        {
            try
            {
                var response = await _http.GetAsync("api/team");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<Team>>();
                    return ApiResponse<List<Team>>.Ok(data ?? new List<Team>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<Team>>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<Team>>.Fail($"Error de conexión: {ex.Message}");
            }
        }
    }
}
