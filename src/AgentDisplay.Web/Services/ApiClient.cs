using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentDisplay.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AgentDisplay.Web.Services;

public sealed class ApiClient(HttpClient http, IJSRuntime js, NavigationManager navigation)
{
    public async Task<DashboardSnapshot?> SnapshotAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/v1/snapshot", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DashboardSnapshot>(cancellationToken: cancellationToken);
    }

    public async Task<AgentSession?> SessionAsync(string id, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/v1/sessions/{Uri.EscapeDataString(id)}", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentSession>(cancellationToken: cancellationToken);
    }

    public async Task DecideAsync(string id, PolicyDecision decision, string actor = "pwa", CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, $"api/v1/gates/{Uri.EscapeDataString(id)}/decision", new GateDecisionRequest(decision, actor), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<DevicePairing?> PairingAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/v1/pairing", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DevicePairing>(cancellationToken: cancellationToken);
    }

    public async Task PushDeviceAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/v1/device/push", new { url }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<JsonElement> SettingsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/v1/settings", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
    }

    public async Task UpdateSettingsAsync(Dictionary<string, string?> roots, bool enableClaudeUsage, string? deviceHostUrl, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Put, "api/v1/settings", new { roots, enableClaudeUsage, deviceHostUrl }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<HookInstallResult>> InstallHooksAsync(AgentProvider? provider, bool dryRun, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/v1/hooks/install", new HookInstallRequest(provider, dryRun), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<HookInstallResult>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task RescanAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/v1/settings/rescan", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);
        if (body is not null) request.Content = JsonContent.Create(body);

        var key = await js.InvokeAsync<string?>("agentDisplay.accessKey", cancellationToken);
        if (!string.IsNullOrWhiteSpace(key)) request.Headers.TryAddWithoutValidation("X-AgentDisplay-Key", key);

        var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized && !new Uri(navigation.Uri).AbsolutePath.Equals("/pair", StringComparison.OrdinalIgnoreCase))
        {
            navigation.NavigateTo("/pair");
        }
        return response;
    }
}
