using System.Net;
using System.Net.Http.Headers;

namespace Figuritas.Client.Auth;

public class BearerTokenHandler : DelegatingHandler
{
    private readonly AuthStateProvider _provider;

    public BearerTokenHandler(AuthStateProvider provider) => _provider = provider;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Track whether we attached a Bearer token so we can decide what to do on 401.
        bool attachedToken = false;

        // Solo agrega el header si el caller no lo fijó manualmente
        if (!request.Headers.Contains("Authorization"))
        {
            var token = await _provider.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                attachedToken = true;
            }
        }

        var response = await base.SendAsync(request, cancellationToken);

        // If the server rejects our token (banned user, token revoked, or race with expiry),
        // silently clear the session so the user sees the login screen on the next navigation.
        if (response.StatusCode == HttpStatusCode.Unauthorized && attachedToken)
        {
            await _provider.SetTokenAsync(null);
            await _provider.SetRefreshTokenAsync(null);
        }

        return response;
    }
}
