using System.Text.Json;
using AgentDisplay.Contracts;

namespace AgentDisplay.Host;

public sealed record RuntimeSettings(
    IReadOnlyDictionary<string, string?> Roots,
    bool? EnableClaudeUsage = null,
    string? DeviceHostUrl = null);

public sealed record RuntimeSettingsPatch(
    IReadOnlyDictionary<string, string?>? Roots,
    bool? EnableClaudeUsage,
    string? DeviceHostUrl);

public sealed class RuntimeSettingsService
{
    private readonly object _gate = new();
    private readonly string _path;
    private RuntimeSettings _settings;

    public RuntimeSettingsService(ILogger<RuntimeSettingsService> logger)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentdisplay");
        Directory.CreateDirectory(directory);
        LocalFileSecurity.RestrictDirectory(directory);
        _path = Path.Combine(directory, "settings.json");
        _settings = Load(logger);
        if (File.Exists(_path)) LocalFileSecurity.RestrictFile(_path);
    }

    public RuntimeSettings Snapshot()
    {
        lock (_gate) return _settings with { Roots = new Dictionary<string, string?>(_settings.Roots, StringComparer.OrdinalIgnoreCase) };
    }

    public string? Root(AgentProvider provider)
    {
        lock (_gate) return _settings.Roots.GetValueOrDefault(provider.ToString());
    }

    public RuntimeSettings Update(RuntimeSettingsPatch patch)
    {
        lock (_gate)
        {
            var roots = new Dictionary<string, string?>(_settings.Roots, StringComparer.OrdinalIgnoreCase);
            if (patch.Roots is not null)
            {
                foreach (var pair in patch.Roots)
                {
                    if (Enum.TryParse<AgentProvider>(pair.Key, true, out var provider)) roots[provider.ToString()] = pair.Value?.Trim();
                }
            }
            _settings = new RuntimeSettings(roots, patch.EnableClaudeUsage ?? _settings.EnableClaudeUsage, patch.DeviceHostUrl ?? _settings.DeviceHostUrl);
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
            var temp = _path + ".tmp";
            File.WriteAllText(temp, json + Environment.NewLine);
            LocalFileSecurity.RestrictFile(temp);
            File.Move(temp, _path, true);
            LocalFileSecurity.RestrictFile(_path);
            return Snapshot();
        }
    }

    private RuntimeSettings Load(ILogger logger)
    {
        try
        {
            if (!File.Exists(_path)) return new RuntimeSettings(new Dictionary<string, string?>());
            return JsonSerializer.Deserialize<RuntimeSettings>(File.ReadAllText(_path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? new RuntimeSettings(new Dictionary<string, string?>());
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Unable to read runtime settings from {Path}", _path);
            return new RuntimeSettings(new Dictionary<string, string?>());
        }
    }
}
