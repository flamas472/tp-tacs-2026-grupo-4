using Figuritas.Shared.Model;
using Figuritas.Shared.Responses;
using Figuritas.Client.Extensions;

namespace Figuritas.Client.Requests
{
    public class NationalTeamHttpClient
    {
        private readonly HttpClient _http;

        public NationalTeamHttpClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<ApiResponse<List<NationalTeam>>> GetNationalTeamsAsync()
        {
            try
            {
                var response = await _http.GetAsync("api/nationalteam");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<NationalTeam>>();
                    return ApiResponse<List<NationalTeam>>.Ok(data ?? new List<NationalTeam>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<NationalTeam>>.Fail(HttpExtensions.GetFriendlyErrorMessage(response.StatusCode, errorMsg));
            }
            catch (Exception)
            {
                return ApiResponse<List<NationalTeam>>.Fail(HttpExtensions.GetFriendlyConnectionError());
            }
        }
    }
}
