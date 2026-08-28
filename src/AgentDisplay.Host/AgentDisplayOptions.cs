namespace AgentDisplay.Host;

public sealed class AgentDisplayOptions
{
    public int ScanIntervalSeconds { get; set; } = 3;
    public int MaxFilesPerProvider { get; set; } = 120;
    public long MaxBytesPerFile { get; set; } = 2 * 1024 * 1024;
    public int GateTimeoutSeconds { get; set; } = 90;
    public bool StrictGates { get; set; }
    public bool EnableClaudeUsage { get; set; }
    public string ClaudeRoot { get; set; } = "~/.claude";
    public string CodexRoot { get; set; } = "~/.codex";
    public string CopilotRoot { get; set; } = "~/.copilot";
}
