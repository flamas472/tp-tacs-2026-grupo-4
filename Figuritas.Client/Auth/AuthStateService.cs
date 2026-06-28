using System.Text;
using System.Text.Json;
using Figuritas.Client.Requests;
using Figuritas.Shared.DTO;
using Figuritas.Shared.DTO.request;

namespace Figuritas.Client.Auth;

public class AuthStateService
{
    private readonly AuthStateProvider _provider;
    private readonly AuthHttpClient _authHttp;
    private readonly UserHttpClient _userHttp;

    public int UserId { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public bool IsAdmin { get; private set; }
    public bool IsSuperAdmin { get; private set; }

    public AuthStateService(AuthStateProvider provider, AuthHttpClient authHttp, UserHttpClient userHttp)
    {
        _provider = provider;
        _authHttp = authHttp;
        _userHttp = userHttp;
    }

    public async Task InitializeAsync()
    {
        var token = await _provider.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
            CacheClaimsFromToken(token);
    }

    // Retorna null si OK, o el mensaje de error.
    public async Task<string?> LoginAsync(string username, string password)
    {
        var result = await _authHttp.LoginAsync(new LoginRequestDTO { Username = username, Password = password });
        if (!result.Success || result.Data?.AccessToken == null)
            return result.ErrorMessage ?? "Las credenciales son incorrectas, o puede que el usuario no exista.";

        CacheClaimsFromToken(result.Data.AccessToken);
        await _provider.SetTokenAsync(result.Data.AccessToken);
        await _provider.SetRefreshTokenAsync(result.Data.RefreshToken);
        return null;
    }

    // Registra y luego hace login automático. Retorna null si OK, o mensaje de error.
    public async Task<string?> RegisterAsync(string username, string password)
    {
        var result = await _authHttp.RegisterAsync(new PostUserDTO { Username = username, Password = password });
        if (!result.Success)
            return result.ErrorMessage ?? "No fue posible completar el registro. Intente nuevamente.";

        return await LoginAsync(username, password);
    }

    public async Task LogoutAsync()
    {
        var refreshToken = await _provider.GetRefreshTokenAsync();
        if (!string.IsNullOrEmpty(refreshToken))
            await _authHttp.LogoutAsync(refreshToken);

        UserId = 0;
        Username = string.Empty;
        IsAdmin = false;
        IsSuperAdmin = false;
        await _provider.SetTokenAsync(null);
        await _provider.SetRefreshTokenAsync(null);
    }

    public async Task<string?> GetTokenAsync() => await _provider.GetTokenAsync();

    private void CacheClaimsFromToken(string token)
    {
        var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }
        var bytes = Convert.FromBase64String(payload);
        var json = Encoding.UTF8.GetString(bytes);
        using var doc = JsonDocument.Parse(json);

        UserId = TryGetInt(doc.RootElement,
            "sub",
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

        Username = TryGetString(doc.RootElement,
            "unique_name",
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name") ?? string.Empty;

        // The backend emits ClaimTypes.Role ("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
        // with value "Admin" or "SuperAdmin". Check both the long-form and short-form "role" key.
        var roleValue = TryGetString(doc.RootElement,
            "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
            "role");
        IsSuperAdmin = roleValue == "SuperAdmin";
        IsAdmin = roleValue == "Admin" || roleValue == "SuperAdmin";
    }

    private static int TryGetInt(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
            if (root.TryGetProperty(key, out var p) && int.TryParse(p.GetString(), out var v))
                return v;
        return 0;
    }

    private static string? TryGetString(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
            if (root.TryGetProperty(key, out var p))
                return p.GetString();
        return null;
    }
}
