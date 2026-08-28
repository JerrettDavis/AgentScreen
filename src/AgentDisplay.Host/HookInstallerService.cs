using System.Diagnostics;
using System.Text.Json;
using AgentDisplay.Contracts;

namespace AgentDisplay.Host;

public sealed class HookInstallerService(ILogger<HookInstallerService> logger)
{
    public async Task<IReadOnlyList<HookInstallResult>> RunAsync(HookInstallRequest request, CancellationToken cancellationToken)
    {
        var script = LocateScript();
        if (script is null) return [new HookInstallResult(request.Provider ?? AgentProvider.Claude, string.Empty, false, request.DryRun, "Hook installer script was not found")];
        var provider = request.Provider?.ToString().ToLowerInvariant() ?? "all";
        var info = new ProcessStartInfo("node")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        info.ArgumentList.Add(script);
        info.ArgumentList.Add("--provider");
        info.ArgumentList.Add(provider);
        info.ArgumentList.Add(request.DryRun ? "--dry-run" : "--apply");
        info.ArgumentList.Add("--json");
        using var process = Process.Start(info);
        if (process is null) return [new HookInstallResult(request.Provider ?? AgentProvider.Claude, script, false, request.DryRun, "Unable to start Node.js")];
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0)
        {
            logger.LogWarning("Hook installer failed: {Error}", stderr);
            return [new HookInstallResult(request.Provider ?? AgentProvider.Claude, script, false, request.DryRun, string.IsNullOrWhiteSpace(stderr) ? "Hook installer failed" : stderr.Trim())];
        }
        return JsonSerializer.Deserialize<List<HookInstallResult>>(stdout, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? [new HookInstallResult(request.Provider ?? AgentProvider.Claude, script, false, request.DryRun, "Installer returned no result")];
    }

    private static string? LocateScript()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "integrations", "hooks", "install.mjs"),
            Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "integrations", "hooks", "install.mjs")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "integrations", "hooks", "install.mjs"))
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
