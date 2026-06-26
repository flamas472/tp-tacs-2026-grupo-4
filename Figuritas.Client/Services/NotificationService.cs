using Figuritas.Client.Models;
using Figuritas.Client.Requests;
using Figuritas.Shared.DTO.response;
using SharedNotifType = Figuritas.Shared.Model.Notificaciones.NotificationType;

namespace Figuritas.Client.Services;

/// <summary>
/// Servicio que gestiona las notificaciones del usuario conectando con el backend.
/// Se registra como Scoped para poder inyectar DashboardHttpClient.
/// </summary>
public class NotificationService
{
    private readonly DashboardHttpClient _dashboardHttp;
    private readonly NotificationHubService _hubService;
    private readonly List<AppNotification> _notifications = new();
    private bool _loaded = false;

    /// <summary>Se dispara cuando la lista de notificaciones cambia (nueva, leída, cargada, etc.).</summary>
    public event Action? OnChange;

    public IReadOnlyList<AppNotification> Notifications => _notifications.AsReadOnly();

    public int UnreadCount => _notifications.Count(n => !n.IsRead);

    public NotificationService(DashboardHttpClient dashboardHttp, NotificationHubService hubService)
    {
        _dashboardHttp = dashboardHttp;
        _hubService = hubService;
        _hubService.OnNotificationReceived += AddRealtimeNotification;
    }

    /// <summary>
    /// Carga las notificaciones desde el backend si aún no fueron cargadas
    /// e inicia la conexión SignalR para actualizaciones en tiempo real.
    /// </summary>
    public async Task LoadAsync()
    {
        // El inicio del hub es independiente de la carga HTTP: se reintenta siempre
        // que la conexión no esté activa, incluso si los datos ya fueron cargados.
        if (!_hubService.IsConnected)
            _ = _hubService.StartAsync();

        if (_loaded) return;
        await FetchFromBackendAsync();
    }

    /// <summary>Fuerza una recarga de notificaciones desde el backend.</summary>
    public async Task RefreshAsync()
    {
        _loaded = false;
        await FetchFromBackendAsync();
    }

    private async Task FetchFromBackendAsync()
    {
        var result = await _dashboardHttp.GetMyNotificationsAsync(1, 50);
        if (result.Success && result.Data is not null)
        {
            _notifications.Clear();
            foreach (var dto in result.Data)
            {
                _notifications.Add(new AppNotification
                {
                    Id          = dto.Id,
                    Type        = MapNotificationType(dto.Type),
                    Title       = dto.Title,
                    Message     = dto.Message,
                    IsRead      = dto.IsRead,
                    CreatedAt   = dto.CreatedAt,
                    NavigationUrl = GetNavigationUrl(dto.Type)
                });
            }
            _loaded = true;
            OnChange?.Invoke();
        }
        // Si la carga falla (p.ej. sin conexión) simplemente no se actualiza la lista.
    }

    /// <summary>Marca una notificación específica como leída (localmente y en el backend).</summary>
    public void MarkAsRead(int id)
    {
        var notification = _notifications.FirstOrDefault(n => n.Id == id);
        if (notification is { IsRead: false })
        {
            notification.IsRead = true;
            OnChange?.Invoke();
            // Fire-and-forget: persistir en el backend
            _ = _dashboardHttp.MarkNotificationAsReadAsync(id);
        }
    }

    /// <summary>Marca todas las notificaciones como leídas (localmente y en el backend).</summary>
    public void MarkAllAsRead()
    {
        var unread = _notifications.Where(n => !n.IsRead).ToList();
        if (!unread.Any()) return;

        foreach (var n in unread)
        {
            n.IsRead = true;
            // Fire-and-forget individual mark-as-read por cada notificación
            _ = _dashboardHttp.MarkNotificationAsReadAsync(n.Id);
        }
        OnChange?.Invoke();
    }

    /// <summary>Retorna las últimas N notificaciones ordenadas por fecha descendente.</summary>
    public IReadOnlyList<AppNotification> GetRecent(int count = 5) =>
        _notifications
            .OrderByDescending(n => n.CreatedAt)
            .Take(count)
            .ToList();

    // ─── Tiempo real ────────────────────────────────────────────────────────────

    private void AddRealtimeNotification(NotificationResponseDTO dto)
    {
        if (_notifications.Any(n => n.Id == dto.Id)) return;

        _notifications.Insert(0, new AppNotification
        {
            Id            = dto.Id,
            Type          = MapNotificationType(dto.Type),
            Title         = dto.Title,
            Message       = dto.Message,
            IsRead        = dto.IsRead,
            CreatedAt     = dto.CreatedAt,
            NavigationUrl = GetNavigationUrl(dto.Type)
        });
        OnChange?.Invoke();
    }

    // ─── Mapeos internos ────────────────────────────────────────────────────────

    private static NotificationType MapNotificationType(SharedNotifType type) => type switch
    {
        SharedNotifType.NewProposal             => NotificationType.NewProposal,
        SharedNotifType.AuctionEnding           => NotificationType.AuctionEnding,
        SharedNotifType.MissingStickerAvailable => NotificationType.StickerAvailable,
        _                                       => NotificationType.NewProposal
    };

    private static string? GetNavigationUrl(SharedNotifType type) => type switch
    {
        SharedNotifType.NewProposal             => "propuestas",
        SharedNotifType.AuctionEnding           => "subastas",
        SharedNotifType.MissingStickerAvailable => "mercado",
        _                                       => null
    };
}
