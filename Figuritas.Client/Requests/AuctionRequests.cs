using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Responses;
using Figuritas.Client.Extensions;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Figuritas.Client.Requests
{
    public class AuctionHttpClient
    {
        private readonly HttpClient _http;

        public AuctionHttpClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<ApiResponse<List<AuctionResponseDTO>>> GetAuctionsAsync()
        {
            try
            {
                var response = await _http.GetAsync("api/auctions");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<AuctionResponseDTO>>();
                    return ApiResponse<List<AuctionResponseDTO>>.Ok(data ?? new List<AuctionResponseDTO>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<AuctionResponseDTO>>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<AuctionResponseDTO>>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<AuctionResponseDTO>> GetAuctionAsync(int id)
        {
            try
            {
                var response = await _http.GetAsync($"api/auctions/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<AuctionResponseDTO>();
                    return data is not null
                        ? ApiResponse<AuctionResponseDTO>.Ok(data)
                        : ApiResponse<AuctionResponseDTO>.Fail("No se pudo leer la subasta.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<AuctionResponseDTO>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<AuctionResponseDTO>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<AuctionResponseDTO>> CreateAuctionAsync(PostAuctionRequestDTO dto, string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "api/auctions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                request.Content = JsonContent.Create(dto);

                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<AuctionResponseDTO>();
                    return data is not null
                        ? ApiResponse<AuctionResponseDTO>.Ok(data)
                        : ApiResponse<AuctionResponseDTO>.Fail("No se pudo leer la subasta creada.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<AuctionResponseDTO>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<AuctionResponseDTO>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<AuctionOfferResponseDTO>> CreateOfferAsync(int auctionId, PostAuctionOfferRequestDTO dto, string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"api/auctions/{auctionId}/offers");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                request.Content = JsonContent.Create(dto);

                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<AuctionOfferResponseDTO>();
                    return data is not null
                        ? ApiResponse<AuctionOfferResponseDTO>.Ok(data)
                        : ApiResponse<AuctionOfferResponseDTO>.Fail("No se pudo leer la oferta creada.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<AuctionOfferResponseDTO>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<AuctionOfferResponseDTO>.Fail($"Error de conexión: {ex.Message}");
            }
        }
    }
}
