using Figuritas.Shared.DTO.request;
using Figuritas.Shared.DTO.response;
using Figuritas.Shared.Model.Intercambios;
using Figuritas.Shared.Responses;
using Figuritas.Client.Extensions;
using System.Net.Http.Json;

namespace Figuritas.Client.Requests
{
    public class DashboardHttpClient
    {
        private readonly HttpClient _http;

        public DashboardHttpClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<ApiResponse<List<MyPublishedStickerResponseDTO>>> GetMyStickersAsync(
            int page = 1, int pageSize = 20)
        {
            try
            {
                var response = await _http.GetAsync(
                    $"api/dashboard/stickers?Page={page}&PageSize={pageSize}");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<MyPublishedStickerResponseDTO>>();
                    return ApiResponse<List<MyPublishedStickerResponseDTO>>.Ok(
                        data ?? new List<MyPublishedStickerResponseDTO>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<MyPublishedStickerResponseDTO>>.Fail(
                    HttpExtensions.GetFriendlyErrorMessage(response.StatusCode, errorMsg));
            }
            catch (Exception)
            {
                return ApiResponse<List<MyPublishedStickerResponseDTO>>.Fail(
                    HttpExtensions.GetFriendlyConnectionError());
            }
        }

        public async Task<ApiResponse<List<ExchangeProposalResponseDTO>>> GetMySentProposalsAsync(
            int page = 1, int pageSize = 20, ExchangeProposalState? state = null)
        {
            try
            {
                var url = $"api/dashboard/proposals/sent?Page={page}&PageSize={pageSize}";
                if (state.HasValue)
                    url += $"&State={state.Value}";

                var response = await _http.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<ExchangeProposalResponseDTO>>();
                    return ApiResponse<List<ExchangeProposalResponseDTO>>.Ok(
                        data ?? new List<ExchangeProposalResponseDTO>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<ExchangeProposalResponseDTO>>.Fail(
                    HttpExtensions.GetFriendlyErrorMessage(response.StatusCode, errorMsg));
            }
            catch (Exception)
            {
                return ApiResponse<List<ExchangeProposalResponseDTO>>.Fail(
                    HttpExtensions.GetFriendlyConnectionError());
            }
        }

        public async Task<ApiResponse<List<ExchangeProposalResponseDTO>>> GetMyReceivedProposalsAsync(
            int page = 1, int pageSize = 20, ExchangeProposalState? state = null)
        {
            try
            {
                var url = $"api/dashboard/proposals/received?Page={page}&PageSize={pageSize}";
                if (state.HasValue)
                    url += $"&State={state.Value}";

                var response = await _http.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<ExchangeProposalResponseDTO>>();
                    return ApiResponse<List<ExchangeProposalResponseDTO>>.Ok(
                        data ?? new List<ExchangeProposalResponseDTO>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<ExchangeProposalResponseDTO>>.Fail(
                    HttpExtensions.GetFriendlyErrorMessage(response.StatusCode, errorMsg));
            }
            catch (Exception)
            {
                return ApiResponse<List<ExchangeProposalResponseDTO>>.Fail(
                    HttpExtensions.GetFriendlyConnectionError());
            }
        }

        public async Task<ApiResponse<List<AuctionResponseDTO>>> GetMyAuctionsAsync(
            int page = 1, int pageSize = 20, string? status = null)
        {
            try
            {
                var url = $"api/dashboard/auctions?Page={page}&PageSize={pageSize}";
                if (!string.IsNullOrEmpty(status))
                    url += $"&Status={status}";
                var response = await _http.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<AuctionResponseDTO>>();
                    return ApiResponse<List<AuctionResponseDTO>>.Ok(
                        data ?? new List<AuctionResponseDTO>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<AuctionResponseDTO>>.Fail(
                    HttpExtensions.GetFriendlyErrorMessage(response.StatusCode, errorMsg));
            }
            catch (Exception)
            {
                return ApiResponse<List<AuctionResponseDTO>>.Fail(HttpExtensions.GetFriendlyConnectionError());
            }
        }

        public async Task<ApiResponse<List<NotificationResponseDTO>>> GetMyNotificationsAsync(
            int page = 1, int pageSize = 20)
        {
            try
            {
                var response = await _http.GetAsync(
                    $"api/dashboard/notifications?Page={page}&PageSize={pageSize}");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<NotificationResponseDTO>>();
                    return ApiResponse<List<NotificationResponseDTO>>.Ok(
                        data ?? new List<NotificationResponseDTO>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<NotificationResponseDTO>>.Fail(
                    HttpExtensions.GetFriendlyErrorMessage(response.StatusCode, errorMsg));
            }
            catch (Exception)
            {
                return ApiResponse<List<NotificationResponseDTO>>.Fail(HttpExtensions.GetFriendlyConnectionError());
            }
        }

        public async Task<ApiResponse<bool>> MarkNotificationAsReadAsync(int id)
        {
            try
            {
                var response = await _http.PatchAsync(
                    $"api/dashboard/notifications/{id}/read", null);

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

        /// <summary>
        /// Returns a paged list of bids placed by the authenticated user on third-party auctions.
        /// Maps to GET /api/dashboard/bids.
        /// </summary>
        public async Task<ApiResponse<List<MyBidResponseDTO>>> GetMyBidsAsync(int page = 1, int pageSize = 20)
        {
            try
            {
                var response = await _http.GetAsync(
                    $"api/dashboard/bids?page={page}&pageSize={pageSize}");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.ProcesarRespuesta<List<MyBidResponseDTO>>();
                    return ApiResponse<List<MyBidResponseDTO>>.Ok(data ?? new List<MyBidResponseDTO>());
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return ApiResponse<List<MyBidResponseDTO>>.Fail(
                    HttpExtensions.GetFriendlyErrorMessage(response.StatusCode, errorMsg));
            }
            catch (Exception)
            {
                return ApiResponse<List<MyBidResponseDTO>>.Fail(HttpExtensions.GetFriendlyConnectionError());
            }
        }

        public async Task<ApiResponse<bool>> UpdatePreferencesAsync(UpdatePreferencesDTO dto)
        {
            try
            {
                var response = await _http.PutAsJsonAsync("api/dashboard/preferences", dto);

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
    }
}
