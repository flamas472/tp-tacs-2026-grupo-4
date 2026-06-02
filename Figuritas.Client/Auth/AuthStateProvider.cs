using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace Figuritas.Client.Auth;

public class AuthStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _js;
    private const string TokenKey = "auth_token";

    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public AuthStateProvider(IJSRuntime js) => _js = js;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
            return Anonymous;

        var claims = ParseClaimsFromJwt(token);

        var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
        if (expClaim != null && long.TryParse(expClaim.Value, out var exp) &&
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp)
        {
            await SetTokenAsync(null);
            return Anonymous;
        }

        var identity = new ClaimsIdentity(claims, "jwt", "unique_name", ClaimTypes.Role);
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public async Task SetTokenAsync(string? token)
    {
        if (token is null)
            await _js.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        else
            await _js.InvokeVoidAsync("localStorage.setItem", TokenKey, token);

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task<string?> GetTokenAsync() =>
        await _js.InvokeAsync<string?>("localStorage.getItem", TokenKey);

    private static List<Claim> ParseClaimsFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }
        var bytes = Convert.FromBase64String(payload);
        var json = Encoding.UTF8.GetString(bytes);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .EnumerateObject()
            .Select(p => new Claim(p.Name, p.Value.ToString()))
            .ToList();
    }
}
