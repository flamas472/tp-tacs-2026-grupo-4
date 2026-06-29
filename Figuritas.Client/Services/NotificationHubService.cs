using Figuritas.Client.Auth;
using Figuritas.Shared.DTO.response;
using Microsoft.AspNetCore.SignalR.Client;

namespace Figuritas.Client.Services;

public class NotificationHubService : IAsyncDisposable
{
    private readonly AuthStateProvider _authProvider;
    private readonly HttpClient _httpClient;
    private HubConnection? _connection;

    public event Action<NotificationResponseDTO>? OnNotificationReceived;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public NotificationHubService(AuthStateProvider authProvider, HttpClient httpClient)
    {
        _authProvider = authProvider;
        _httpClient = httpClient;
    }

    public async Task StartAsync()
    {
        if (_connection?.State is HubConnectionState.Connected
                               or HubConnectionState.Connecting
                               or HubConnectionState.Reconnecting)
            return;

        var hubUrl = new Uri(_httpClient.BaseAddress!, "api/notification-hub");

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl.ToString(), options =>
            {
                options.AccessTokenProvider = _authProvider.GetTokenAsync;
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<NotificationResponseDTO>("ReceiveNotification", dto =>
        {
            OnNotificationReceived?.Invoke(dto);
        });

        try
        {
            await _connection.StartAsync();
        }
        catch
        {
            // Real-time delivery is best-effort; HTTP fallback remains active.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
