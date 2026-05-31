using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Figuritas.Client;
using Figuritas.Client.Auth;
using Figuritas.Client.Requests;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Obtener la dirección base según el entorno
string apiBaseAddress;

if (builder.HostEnvironment.IsDevelopment())
{
    // Cuando se corre la aplicacon con dotnet watch run
    apiBaseAddress = "http://localhost:8080"; 
}
else
{
    // Cuando está en producción (Docker)
    apiBaseAddress = builder.HostEnvironment.BaseAddress;
}

builder.Services.AddScoped<AuthStateProvider>();
builder.Services.AddScoped(sp =>
{
    var authProvider = sp.GetRequiredService<AuthStateProvider>();
    var handler = new BearerTokenHandler(authProvider) { InnerHandler = new HttpClientHandler() };
    return new HttpClient(handler) { BaseAddress = new Uri(apiBaseAddress) };
});
builder.Services.AddScoped<StickerHttpClient>();
builder.Services.AddScoped<AuthHttpClient>();
builder.Services.AddScoped<UserHttpClient>();
builder.Services.AddScoped<InventoryStickerHttpClient>();
builder.Services.AddScoped<TeamHttpClient>();
builder.Services.AddScoped<CategoryHttpClient>();
builder.Services.AddScoped<NationalTeamHttpClient>();
builder.Services.AddScoped<ExchangeHttpClient>();
builder.Services.AddScoped<ExchangeProposalHttpClient>();
builder.Services.AddScoped<AuctionHttpClient>();
builder.Services.AddScoped<DashboardHttpClient>();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider>(p => p.GetRequiredService<AuthStateProvider>());
builder.Services.AddScoped<AuthStateService>();
builder.Services.AddMudServices();

await builder.Build().RunAsync();
