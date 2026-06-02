using System.Net.Http.Headers;

namespace Figuritas.Client.Auth;

public class BearerTokenHandler : DelegatingHandler
{
    private readonly AuthStateProvider _provider;

    public BearerTokenHandler(AuthStateProvider provider) => _provider = provider;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Solo agrega el header si el caller no lo fijó manualmente
        if (!request.Headers.Contains("Authorization"))
        {
            var token = await _provider.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
