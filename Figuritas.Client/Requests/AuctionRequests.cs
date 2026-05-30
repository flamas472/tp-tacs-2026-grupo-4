using Figuritas.Shared.Model;
using Figuritas.Shared.DTO.request;
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

        public async Task<ApiResponse<List<Auction>>> GetAuctionsAsync()
        {
            try
            {
                var response = await _http.GetAsync("api/auctions");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<Auction>>();
                    return ApiResponse<List<Auction>>.Ok(data ?? new List<Auction>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<Auction>>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<Auction>>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<Auction>> GetAuctionAsync(int id)
        {
            try
            {
                var response = await _http.GetAsync($"api/auctions/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<Auction>();
                    return data is not null
                        ? ApiResponse<Auction>.Ok(data)
                        : ApiResponse<Auction>.Fail("No se pudo leer la subasta.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<Auction>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<Auction>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<Auction>> CreateAuctionAsync(PostAuctionDTO dto, string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "api/auctions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                request.Content = JsonContent.Create(dto);

                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<Auction>();
                    return data is not null
                        ? ApiResponse<Auction>.Ok(data)
                        : ApiResponse<Auction>.Fail("No se pudo leer la subasta creada.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<Auction>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<Auction>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<AuctionOffer>> CreateOfferAsync(int auctionId, PostAuctionOfferDTO dto, string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"api/auctions/{auctionId}/offers");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                request.Content = JsonContent.Create(dto);

                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<AuctionOffer>();
                    return data is not null
                        ? ApiResponse<AuctionOffer>.Ok(data)
                        : ApiResponse<AuctionOffer>.Fail("No se pudo leer la oferta creada.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<AuctionOffer>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<AuctionOffer>.Fail($"Error de conexión: {ex.Message}");
            }
        }
    }
}
