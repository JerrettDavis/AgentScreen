using System.Security.Cryptography;

namespace AgentDisplay.Host;

public sealed class PairingTokenService
{
    private readonly string _path;
    private readonly Lazy<string> _key;

    public PairingTokenService()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentdisplay");
        Directory.CreateDirectory(directory);
        LocalFileSecurity.RestrictDirectory(directory);
        _path = Path.Combine(directory, "pairing-key");
        _key = new Lazy<string>(LoadOrCreate, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string Key => _key.Value;

    public bool Matches(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        var left = System.Text.Encoding.UTF8.GetBytes(Key);
        var right = System.Text.Encoding.UTF8.GetBytes(candidate);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    private string LoadOrCreate()
    {
        if (File.Exists(_path))
        {
            var existing = File.ReadAllText(_path).Trim();
            if (existing.Length >= 24)
            {
                LocalFileSecurity.RestrictFile(_path);
                return existing;
            }
        }

        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var temp = _path + $".tmp-{Environment.ProcessId}";
        File.WriteAllText(temp, key + Environment.NewLine);
        LocalFileSecurity.RestrictFile(temp);
        File.Move(temp, _path, true);
        LocalFileSecurity.RestrictFile(_path);
        return key;
    }
}
