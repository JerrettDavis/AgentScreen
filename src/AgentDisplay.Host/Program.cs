using System.Net;
using System.Net.Http.Json;
using AgentDisplay.Contracts;
using AgentDisplay.Core;
using AgentDisplay.Host;
using Microsoft.Extensions.Options;

var forceDemo = args.Any(x => x.Equals("--demo", StringComparison.OrdinalIgnoreCase));
var filteredArgs = args.Where(x => !x.Equals("--demo", StringComparison.OrdinalIgnoreCase)).ToArray();
var builder = WebApplication.CreateBuilder(filteredArgs);

builder.Services.Configure<AgentDisplayOptions>(builder.Configuration.GetSection("AgentDisplay"));
builder.Services.AddSingleton<PricingCatalog>();
builder.Services.AddSingleton<Redactor>();
builder.Services.AddSingleton<PolicyEngine>();
builder.Services.AddSingleton<AgentLogParser>();
builder.Services.AddSingleton<MetricsCalculator>();
builder.Services.AddSingleton<DeviceSnapshotMapper>();
builder.Services.AddSingleton<RuntimeSettingsService>();
builder.Services.AddSingleton<DirectoryLocator>();
builder.Services.AddSingleton<PairingTokenService>();
builder.Services.AddSingleton<NetworkAddressService>();
builder.Services.AddSingleton<SnapshotStore>();
builder.Services.AddSingleton<HookProcessor>();
builder.Services.AddSingleton<HookInstallerService>();
builder.Services.AddHttpClient<ClaudeUsageCollector>(client => client.Timeout = TimeSpan.FromSeconds(8));
builder.Services.AddSingleton<CodexUsageCollector>();
builder.Services.AddHttpClient("device", client => client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddSingleton<AgentScannerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AgentScannerService>());
builder.Services.AddHostedService<GateCleanupService>();

var app = builder.Build();
app.Services.GetRequiredService<AgentScannerService>().ForceDemo = forceDemo;

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") &&
        context.Connection.RemoteIpAddress is { } address &&
        !IPAddress.IsLoopback(address))
    {
        var pairing = context.Request.Headers["X-AgentDisplay-Key"].FirstOrDefault()
            ?? context.Request.Query["key"].FirstOrDefault();
        if (!app.Services.GetRequiredService<PairingTokenService>().Matches(pairing))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "A valid AgentDisplay pairing key is required." });
            return;
        }
    }
    await next();
});

var api = app.MapGroup("/api/v1");
api.MapGet("/snapshot", (SnapshotStore store) => Results.Ok(store.Snapshot()));
api.MapGet("/sessions", (SnapshotStore store) => Results.Ok(store.Snapshot().Sessions));
api.MapGet("/sessions/{id}", (string id, SnapshotStore store) => store.Session(id) is { } session ? Results.Ok(session) : Results.NotFound());
api.MapGet("/sessions/{id}/turns", (string id, SnapshotStore store) => store.Session(id) is { } session ? Results.Ok(session.Turns) : Results.NotFound());

api.MapPost("/hooks/event", (HookEvent hookEvent, HookProcessor processor) => Results.Ok(processor.Process(hookEvent)));
api.MapGet("/gates/{id}", (string id, SnapshotStore store, HookProcessor processor) =>
{
    var gate = store.Gate(id);
    if (gate is null) return Results.NotFound();
    var decision = gate.State switch
    {
        GateState.Allowed => PolicyDecision.Allow,
        GateState.Denied => PolicyDecision.Deny,
        GateState.Expired => processor.ResolveExpired(),
        _ => PolicyDecision.Ask
    };
    return Results.Ok(new { gate, decision, pending = gate.State == GateState.Pending });
});
api.MapPost("/gates/{id}/decision", (string id, GateDecisionRequest request, SnapshotStore store) =>
{
    if (request.Decision == PolicyDecision.Ask) return Results.BadRequest(new { error = "A final allow or deny decision is required." });
    return store.Decide(id, request.Decision, request.Actor) is { } gate ? Results.Ok(gate) : Results.NotFound();
});

api.MapGet("/settings", (RuntimeSettingsService runtime, DirectoryLocator directories, IOptionsMonitor<AgentDisplayOptions> configured, PolicyEngine policy) =>
{
    var current = runtime.Snapshot();
    return Results.Ok(new
    {
        roots = Enum.GetValues<AgentProvider>().ToDictionary(x => x.ToString(), x => DirectoryLocator.Display(directories.Root(x))),
        enableClaudeUsage = current.EnableClaudeUsage ?? configured.CurrentValue.EnableClaudeUsage,
        configured.CurrentValue.StrictGates,
        configured.CurrentValue.GateTimeoutSeconds,
        current.DeviceHostUrl,
        policies = policy.Describe()
    });
});
api.MapPut("/settings", (RuntimeSettingsPatch patch, RuntimeSettingsService runtime) => Results.Ok(runtime.Update(patch)));
api.MapPost("/settings/rescan", async (AgentScannerService scanner, CancellationToken cancellationToken) =>
{
    await scanner.ScanAsync(cancellationToken);
    return Results.Accepted();
});
api.MapPost("/hooks/install", async (HookInstallRequest request, HookInstallerService installer, CancellationToken cancellationToken) =>
    Results.Ok(await installer.RunAsync(request, cancellationToken)));

api.MapGet("/pairing", (HttpRequest request, PairingTokenService pairing, NetworkAddressService addresses) =>
{
    var candidates = addresses.CandidateHostUrls(request);
    return Results.Ok(new DevicePairing(
    candidates.FirstOrDefault() ?? $"{request.Scheme}://{request.Host}",
    candidates,
    pairing.Key,
    Protocol.BluetoothServiceUuid,
    Protocol.BluetoothRxUuid,
    Protocol.BluetoothTxUuid));
});
api.MapGet("/device/snapshot", (SnapshotStore store) => Results.Ok(store.DeviceSnapshot()));
api.MapPost("/device/push", async (DevicePushRequest request, SnapshotStore store, PairingTokenService pairing, NetworkAddressService addresses, IHttpClientFactory clients, CancellationToken cancellationToken) =>
{
    if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var baseUri) ||
        !(baseUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
          baseUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        return Results.BadRequest(new { error = "A valid HTTP device URL is required." });
    if (!addresses.IsLocalDeviceTarget(baseUri))
        return Results.BadRequest(new { error = "Device pushes are restricted to loopback, private-network, link-local, and .local targets." });
    var target = new Uri(baseUri, "/api/snapshot");
    using var message = new HttpRequestMessage(HttpMethod.Post, target)
    {
        Content = JsonContent.Create(store.DeviceSnapshot())
    };
    message.Headers.TryAddWithoutValidation("X-AgentDisplay-Key", pairing.Key);
    using var response = await clients.CreateClient("device").SendAsync(message, cancellationToken);
    if (!response.IsSuccessStatusCode)
        return Results.Problem($"The display returned HTTP {(int)response.StatusCode}.", statusCode: StatusCodes.Status502BadGateway);
    return Results.Ok(new { response.StatusCode, target = target.ToString() });
});

app.MapGet("/healthz", () => Results.Ok(new { status = "healthy", version = Protocol.ProductVersion }));
app.MapFallbackToFile("index.html");
app.Run();

public sealed record DevicePushRequest(string Url);
