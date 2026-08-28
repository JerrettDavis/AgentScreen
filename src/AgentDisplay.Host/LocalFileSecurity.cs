namespace AgentDisplay.Host;

internal static class LocalFileSecurity
{
    public static void RestrictDirectory(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        TrySet(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    public static void RestrictFile(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        TrySet(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static void TrySet(string path, UnixFileMode mode)
    {
        if (OperatingSystem.IsWindows()) return;
        try { File.SetUnixFileMode(path, mode); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException) { }
    }
}
