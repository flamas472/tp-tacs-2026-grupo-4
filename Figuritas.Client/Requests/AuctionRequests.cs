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

        public async Task<ApiResponse<List<AuctionResponseDTO>>> GetAuctionsAsync(int page = 1, int pageSize = 20, string? status = null)
        {
            try
            {
                var url = $"api/auctions?page={page}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(status))
                    url += $"&status={status}";
                var response = await _http.GetAsync(url);

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

        public async Task<ApiResponse<AuctionResponseDTO>> CloseAuctionAsync(int id, CloseAuctionRequestDTO dto)
        {
            try
            {
                var response = await _http.PostAsJsonAsync($"api/auctions/{id}/close", dto);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<AuctionResponseDTO>();
                    return data is not null
                        ? ApiResponse<AuctionResponseDTO>.Ok(data)
                        : ApiResponse<AuctionResponseDTO>.Fail("No se pudo leer la subasta cerrada.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<AuctionResponseDTO>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<AuctionResponseDTO>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<AuctionWatchlistResponseDTO>>> GetMyWatchlistAsync()
        {
            try
            {
                var response = await _http.GetAsync("api/auctions/watchlist");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<AuctionWatchlistResponseDTO>>();
                    return ApiResponse<List<AuctionWatchlistResponseDTO>>.Ok(data ?? new List<AuctionWatchlistResponseDTO>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<AuctionWatchlistResponseDTO>>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<AuctionWatchlistResponseDTO>>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<AuctionWatchlistResponseDTO>> WatchAuctionAsync(int id)
        {
            try
            {
                var response = await _http.PostAsync($"api/auctions/{id}/watch", null);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<AuctionWatchlistResponseDTO>();
                    return data is not null
                        ? ApiResponse<AuctionWatchlistResponseDTO>.Ok(data)
                        : ApiResponse<AuctionWatchlistResponseDTO>.Fail("No se pudo leer la entrada de watchlist.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<AuctionWatchlistResponseDTO>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<AuctionWatchlistResponseDTO>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> UnwatchAuctionAsync(int id)
        {
            try
            {
                var response = await _http.DeleteAsync($"api/auctions/{id}/watch");

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

        /// <summary>Gets all offers for a given auction (public endpoint).</summary>
        public async Task<ApiResponse<List<AuctionOfferResponseDTO>>> GetAuctionOffersAsync(int auctionId)
        {
            try
            {
                var response = await _http.GetAsync($"api/auctions/{auctionId}/offers");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<AuctionOfferResponseDTO>>();
                    return ApiResponse<List<AuctionOfferResponseDTO>>.Ok(data ?? new());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<AuctionOfferResponseDTO>>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<AuctionOfferResponseDTO>>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        /// <summary>Cancels the authenticated user's offer on an auction.</summary>
        public async Task<ApiResponse<AuctionOfferResponseDTO>> CancelOfferAsync(int auctionId, int offerId, string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, $"api/auctions/{auctionId}/offers/{offerId}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<AuctionOfferResponseDTO>();
                    return data is not null
                        ? ApiResponse<AuctionOfferResponseDTO>.Ok(data)
                        : ApiResponse<AuctionOfferResponseDTO>.Fail("No se pudo leer la oferta cancelada.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<AuctionOfferResponseDTO>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<AuctionOfferResponseDTO>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        /// <summary>Updates (appends stickers to) the authenticated user's offer on an auction.</summary>
        public async Task<ApiResponse<AuctionOfferResponseDTO>> UpdateOfferAsync(int auctionId, int offerId, UpdateAuctionOfferRequestDTO dto, string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Patch, $"api/auctions/{auctionId}/offers/{offerId}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                request.Content = JsonContent.Create(dto);

                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<AuctionOfferResponseDTO>();
                    return data is not null
                        ? ApiResponse<AuctionOfferResponseDTO>.Ok(data)
                        : ApiResponse<AuctionOfferResponseDTO>.Fail("No se pudo leer la oferta actualizada.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<AuctionOfferResponseDTO>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<AuctionOfferResponseDTO>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        /// <summary>
        /// Allows the auctioneer to pre-select a preferred offer without closing the auction.
        /// Maps to PATCH /api/auctions/{auctionId}/selected-offer.
        /// </summary>
        public async Task<ApiResponse<AuctionResponseDTO>> SelectBestOfferAsync(int auctionId, SelectBestOfferRequestDTO dto, string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Patch, $"api/auctions/{auctionId}/selected-offer");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                request.Content = JsonContent.Create(dto);

                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<AuctionResponseDTO>();
                    return data is not null
                        ? ApiResponse<AuctionResponseDTO>.Ok(data)
                        : ApiResponse<AuctionResponseDTO>.Fail("No se pudo leer la subasta actualizada.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<AuctionResponseDTO>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<AuctionResponseDTO>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        /// <summary>
        /// Accepts a specific offer, closing the auction with that offer as the winner.
        /// Maps to POST /api/auctions/{auctionId}/offers/{offerId}/accept.
        /// </summary>
        public async Task<ApiResponse<AuctionResponseDTO>> AcceptOfferAsync(int auctionId, int offerId, string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"api/auctions/{auctionId}/offers/{offerId}/accept");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<AuctionResponseDTO>();
                    return data is not null
                        ? ApiResponse<AuctionResponseDTO>.Ok(data)
                        : ApiResponse<AuctionResponseDTO>.Fail("No se pudo leer la subasta cerrada.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<AuctionResponseDTO>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<AuctionResponseDTO>.Fail($"Error de conexión: {ex.Message}");
            }
        }

        /// <summary>
        /// Cancels the auction (no winner). Calls POST /api/auctions/{id}/close with a null WinningOfferId.
        /// NOTE: This reuses the legacy [LEGACY] /close endpoint, which is currently the only server-side
        /// path for cancellation (no WinningOfferId triggers the cancellation branch).
        /// TODO: migrate to a dedicated cancel endpoint (e.g. DELETE /api/auctions/{id}) when available.
        /// </summary>
        public async Task<ApiResponse<AuctionResponseDTO>> CancelAuctionAsync(int auctionId, string authToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"api/auctions/{auctionId}/close");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
                request.Content = JsonContent.Create(new CloseAuctionRequestDTO { WinningOfferId = null });

                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<AuctionResponseDTO>();
                    return data is not null
                        ? ApiResponse<AuctionResponseDTO>.Ok(data)
                        : ApiResponse<AuctionResponseDTO>.Fail("No se pudo leer la subasta cancelada.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<AuctionResponseDTO>.Fail($"Error del servidor: {response.StatusCode}. {errorMsg}");
            }
            catch (Exception ex)
            {
                return ApiResponse<AuctionResponseDTO>.Fail($"Error de conexión: {ex.Message}");
            }
        }
    }
}
