using AgentDisplay.Contracts;
using Microsoft.JSInterop;

namespace AgentDisplay.Web.Services;

public sealed class DisplaySyncService(ApiClient api, IJSRuntime js) : IAsyncDisposable
{
    private CancellationTokenSource? loopStop;
    private SyncTransport transport;
    private string? lanUrl;

    public bool AutoUpdate { get; private set; } = true;
    public int AutoUpdateIntervalSeconds { get; private set; } = 30;
    public bool BleConnected { get; private set; }
    public bool Connected => transport != SyncTransport.None;
    public string ConnectionName => transport switch { SyncTransport.Bluetooth => "Bluetooth", SyncTransport.Lan => "LAN", _ => "Not connected" };
    public DateTimeOffset? LastUpdatedAt { get; private set; }
    public string? LastError { get; private set; }
    public event Action? Changed;

    public async Task InitializeAsync()
    {
        AutoUpdate = await js.InvokeAsync<bool>("agentDisplay.autoSyncEnabled");
        AutoUpdateIntervalSeconds = await js.InvokeAsync<int>("agentDisplay.autoSyncIntervalSeconds");
        Changed?.Invoke();
    }

    public async Task SetAutoUpdateIntervalAsync(int seconds)
    {
        if (seconds is not (30 or 60 or 300 or 900))
            throw new ArgumentOutOfRangeException(nameof(seconds), "Choose a supported automatic sync interval.");
        AutoUpdateIntervalSeconds = seconds;
        await js.InvokeVoidAsync("agentDisplay.setAutoSyncIntervalSeconds", seconds);
        StopLoop();
        if (AutoUpdate && Connected) StartLoop();
        Changed?.Invoke();
    }

    public async Task SetAutoUpdateAsync(bool enabled)
    {
        AutoUpdate = enabled;
        await js.InvokeVoidAsync("agentDisplay.setAutoSyncEnabled", enabled);
        StopLoop();
        if (enabled && Connected)
        {
            await PushCoreAsync();
            StartLoop();
        }
        Changed?.Invoke();
    }

    public async Task ConnectBleAsync(DevicePairing pairing, Action<string>? progress = null)
    {
        try
        {
            progress?.Invoke("Choose your AgentDisplay…");
            await js.InvokeAsync<object>("agentDisplay.connect", pairing.BluetoothServiceUuid, pairing.BluetoothRxUuid);
            progress?.Invoke("Bluetooth connected. Preparing current stats…");
            BleConnected = true;
            transport = SyncTransport.Bluetooth;
            lanUrl = null;
            LastError = null;
            StopLoop();
            if (AutoUpdate)
            {
                progress?.Invoke("Syncing current stats to the display…");
                await PushCoreAsync();
                StartLoop();
                progress?.Invoke("Stats synced. Opening dashboard…");
            }
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            MarkBleDisconnected(FriendlyBluetoothError(ex));
            throw new InvalidOperationException(LastError, ex);
        }
    }

    public async Task ConnectLanAsync(string url)
    {
        await api.PushDeviceAsync(url);
        transport = SyncTransport.Lan;
        lanUrl = url;
        LastUpdatedAt = DateTimeOffset.UtcNow;
        LastError = null;
        StopLoop();
        if (AutoUpdate) StartLoop();
        Changed?.Invoke();
    }

    public async Task PushNowAsync()
    {
        if (!Connected) throw new InvalidOperationException("Connect to a display first.");
        await PushCoreAsync();
    }

    private async Task PushCoreAsync()
    {
        if (transport == SyncTransport.Bluetooth)
        {
            var snapshot = await js.InvokeAsync<object>("agentDisplay.compactSnapshot");
            await js.InvokeAsync<int>("agentDisplay.push", snapshot);
        }
        else if (transport == SyncTransport.Lan && lanUrl is not null)
        {
            await api.PushDeviceAsync(lanUrl);
        }
        else return;

        LastUpdatedAt = DateTimeOffset.UtcNow;
        LastError = null;
        Changed?.Invoke();
    }

    private void StartLoop()
    {
        if (!AutoUpdate || !Connected || loopStop is not null) return;
        loopStop = new CancellationTokenSource();
        _ = LoopAsync(loopStop.Token);
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(AutoUpdateIntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try { await PushCoreAsync(); }
                catch (Exception ex)
                {
                    if (transport == SyncTransport.Bluetooth)
                    {
                        MarkBleDisconnected(FriendlyBluetoothError(ex));
                        break;
                    }
                    LastError = ex.Message;
                    Changed?.Invoke();
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private void StopLoop()
    {
        loopStop?.Cancel();
        loopStop?.Dispose();
        loopStop = null;
    }

    private void MarkBleDisconnected(string message)
    {
        BleConnected = false;
        if (transport == SyncTransport.Bluetooth) transport = SyncTransport.None;
        LastError = message;
        Changed?.Invoke();
    }

    private static string FriendlyBluetoothError(Exception ex)
    {
        var message = ex.Message ?? string.Empty;
        if (message.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase) &&
            (message.Contains("retry", StringComparison.OrdinalIgnoreCase) || message.Contains("reconnect", StringComparison.OrdinalIgnoreCase)))
            return message.Split("\n", 2)[0].Trim();
        if (message.Contains("GATT", StringComparison.OrdinalIgnoreCase) || message.Contains("disconnected", StringComparison.OrdinalIgnoreCase))
            return "The Bluetooth connection was lost. Retry Connect. If it continues, reset the display, wait for startup, and reconnect.";
        return "Bluetooth sync failed. Retry Connect. If it continues, reset the display and make sure no other browser or phone is connected.";
    }

    public ValueTask DisposeAsync() { StopLoop(); return ValueTask.CompletedTask; }
    private enum SyncTransport { None, Lan, Bluetooth }
}
