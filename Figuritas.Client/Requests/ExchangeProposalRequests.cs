using Figuritas.Shared.Model;
using Figuritas.Shared.DTO.request;
using Figuritas.Shared.Responses;
using Figuritas.Client.Extensions;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Figuritas.Client.Requests
{
    public class ExchangeProposalHttpClient
    {
        private readonly HttpClient _http;

        public ExchangeProposalHttpClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<ApiResponse<List<ExchangeProposal>>> GetSentProposalsAsync(string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "api/exchangeproposals/sent");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<ExchangeProposal>>();
                    return ApiResponse<List<ExchangeProposal>>.Ok(data ?? new List<ExchangeProposal>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<ExchangeProposal>>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ExchangeProposal>>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<ExchangeProposal>>> GetReceivedProposalsAsync(string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "api/exchangeproposals/received");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<ExchangeProposal>>();
                    return ApiResponse<List<ExchangeProposal>>.Ok(data ?? new List<ExchangeProposal>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<ExchangeProposal>>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ExchangeProposal>>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ExchangeProposal>> CreateProposalAsync(PostExchangeProposalDTO dto, string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "api/exchangeproposals");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                request.Content = JsonContent.Create(dto);

                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<ExchangeProposal>();
                    return data is not null
                        ? ApiResponse<ExchangeProposal>.Ok(data)
                        : ApiResponse<ExchangeProposal>.Fail("No se pudo leer la propuesta creada.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<ExchangeProposal>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<ExchangeProposal>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> AcceptProposalAsync(int id, string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"api/exchangeproposals/{id}/accept");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                var response = await _http.SendAsync(request);

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

        public async Task<ApiResponse<bool>> RejectProposalAsync(int id, string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"api/exchangeproposals/{id}/reject");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                var response = await _http.SendAsync(request);

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

        public async Task<ApiResponse<bool>> CancelProposalAsync(int id, string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"api/exchangeproposals/{id}/cancel");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                var response = await _http.SendAsync(request);

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
