using AgentDisplay.Contracts;
using Microsoft.Extensions.Options;

namespace AgentDisplay.Host;

public sealed class DirectoryLocator(IOptionsMonitor<AgentDisplayOptions> options, RuntimeSettingsService runtime)
{
    public string Root(AgentProvider provider)
    {
        var configured = runtime.Root(provider);
        return Expand(!string.IsNullOrWhiteSpace(configured) ? configured : provider switch
        {
            AgentProvider.Claude => options.CurrentValue.ClaudeRoot,
            AgentProvider.Codex => options.CurrentValue.CodexRoot,
            _ => options.CurrentValue.CopilotRoot
        });
    }

    public IReadOnlyList<(AgentProvider Provider, string Root, string Pattern)> Sources() =>
    [
        (AgentProvider.Claude, Root(AgentProvider.Claude), "projects/**/*.jsonl"),
        (AgentProvider.Codex, Root(AgentProvider.Codex), "sessions/**/*.jsonl"),
        (AgentProvider.Codex, Root(AgentProvider.Codex), "history.jsonl"),
        (AgentProvider.Copilot, Root(AgentProvider.Copilot), "session-state/*/events.jsonl")
    ];

    public static string Expand(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        if (expanded == "~") return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (expanded.StartsWith("~/") || expanded.StartsWith("~\\"))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), expanded[2..]);
        }
        return Path.GetFullPath(expanded);
    }

    public static string Display(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd(Path.DirectorySeparatorChar);
        return path.StartsWith(home, StringComparison.OrdinalIgnoreCase) ? "~" + path[home.Length..].Replace('\\', '/') : path.Replace('\\', '/');
    }
}
