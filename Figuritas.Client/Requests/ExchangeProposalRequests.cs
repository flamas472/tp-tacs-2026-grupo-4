using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
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

        public async Task<ApiResponse<List<ExchangeProposalResponseDTO>>> GetSentProposalsAsync(string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "api/dashboard/proposals/sent");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<ExchangeProposalResponseDTO>>();
                    return ApiResponse<List<ExchangeProposalResponseDTO>>.Ok(data ?? new List<ExchangeProposalResponseDTO>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<ExchangeProposalResponseDTO>>.Fail(HttpExtensions.GetFriendlyErrorMessage(response.StatusCode, errorMsg));
            }
            catch (Exception)
            {
                return ApiResponse<List<ExchangeProposalResponseDTO>>.Fail(HttpExtensions.GetFriendlyConnectionError());
            }
        }

        public async Task<ApiResponse<List<ExchangeProposalResponseDTO>>> GetReceivedProposalsAsync(string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "api/dashboard/proposals/received");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<ExchangeProposalResponseDTO>>();
                    return ApiResponse<List<ExchangeProposalResponseDTO>>.Ok(data ?? new List<ExchangeProposalResponseDTO>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<ExchangeProposalResponseDTO>>.Fail(HttpExtensions.GetFriendlyErrorMessage(response.StatusCode, errorMsg));
            }
            catch (Exception)
            {
                return ApiResponse<List<ExchangeProposalResponseDTO>>.Fail(HttpExtensions.GetFriendlyConnectionError());
            }
        }

        public async Task<ApiResponse<ExchangeProposalResponseDTO>> CreateProposalAsync(PostExchangeProposalRequestDTO dto, string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "api/exchange-proposals");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                request.Content = JsonContent.Create(dto);

                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<ExchangeProposalResponseDTO>();
                    return data is not null
                        ? ApiResponse<ExchangeProposalResponseDTO>.Ok(data)
                        : ApiResponse<ExchangeProposalResponseDTO>.Fail("No se pudo leer la propuesta creada.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<ExchangeProposalResponseDTO>.Fail(HttpExtensions.GetFriendlyErrorMessage(response.StatusCode, errorMsg));
            }
            catch (Exception)
            {
                return ApiResponse<ExchangeProposalResponseDTO>.Fail(HttpExtensions.GetFriendlyConnectionError());
            }
        }

        public async Task<ApiResponse<bool>> AcceptProposalAsync(int id, string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"api/exchange-proposals/{id}/accept");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                var response = await _http.SendAsync(request);

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

        public async Task<ApiResponse<bool>> RejectProposalAsync(int id, string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"api/exchange-proposals/{id}/reject");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                var response = await _http.SendAsync(request);

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

        public async Task<ApiResponse<bool>> CancelProposalAsync(int id, string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"api/exchange-proposals/{id}/cancel");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                var response = await _http.SendAsync(request);

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

        public async Task<ApiResponse<ExchangeProposalResponseDTO>> GetExchangeProposalByIdAsync(int id)
        {
            try
            {
                var response = await _http.GetAsync($"api/exchange-proposals/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<ExchangeProposalResponseDTO>();
                    return data is not null
                        ? ApiResponse<ExchangeProposalResponseDTO>.Ok(data)
                        : ApiResponse<ExchangeProposalResponseDTO>.Fail("No se pudo leer la propuesta.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<ExchangeProposalResponseDTO>.Fail(HttpExtensions.GetFriendlyErrorMessage(response.StatusCode, errorMsg));
            }
            catch (Exception)
            {
                return ApiResponse<ExchangeProposalResponseDTO>.Fail(HttpExtensions.GetFriendlyConnectionError());
            }
        }

        public async Task<ApiResponse<ExchangeSummaryDTO>> GetExchangeByProposalIdAsync(int id)
        {
            try
            {
                var response = await _http.GetAsync($"api/exchange-proposals/{id}/exchange");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<ExchangeSummaryDTO>();
                    return data is not null
                        ? ApiResponse<ExchangeSummaryDTO>.Ok(data)
                        : ApiResponse<ExchangeSummaryDTO>.Fail("No se pudo leer el intercambio.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<ExchangeSummaryDTO>.Fail(HttpExtensions.GetFriendlyErrorMessage(response.StatusCode, errorMsg));
            }
            catch (Exception)
            {
                return ApiResponse<ExchangeSummaryDTO>.Fail(HttpExtensions.GetFriendlyConnectionError());
            }
        }
    }

    public record ExchangeSummaryDTO(int Id, int ExchangeProposalID, int ProponentID, int ProposedID);
}
