using AgentDisplay.Web;
using AgentDisplay.Web.Services;
using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<DisplaySyncService>();
var host = builder.Build();
var cultureName = await host.Services.GetRequiredService<IJSRuntime>().InvokeAsync<string>("agentDisplay.browserCulture");
try
{
    var culture = CultureInfo.GetCultureInfo(cultureName);
    CultureInfo.DefaultThreadCurrentCulture = culture;
    CultureInfo.DefaultThreadCurrentUICulture = culture;
}
catch (CultureNotFoundException) { }
await host.RunAsync();
