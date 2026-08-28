using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentDisplay.Core;

public sealed partial class Redactor
{
    private static readonly string[] SensitiveNames =
    [
        "authorization", "api_key", "apikey", "access_token", "refresh_token",
        "token", "password", "secret", "cookie", "credential", "private_key"
    ];

    public string Text(string? value, int maxLength = 180)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var result = HomePathRegex().Replace(value, "~$3");
        result = WindowsUserPathRegex().Replace(result, "~$3");
        result = BearerRegex().Replace(result, "$1[redacted]");
        result = ApiTokenRegex().Replace(result, "[redacted-token]");
        result = result.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return result.Length <= maxLength
            ? result
            : result[..Math.Max(0, maxLength - 1)] + "…";
    }

    public JsonElement Json(JsonElement value)
    {
        var node = Rewrite(value, null);
        return JsonSerializer.SerializeToElement(node);
    }

    private object? Rewrite(JsonElement element, string? name)
    {
        if (name is not null && SensitiveNames.Any(x => name.Contains(x, StringComparison.OrdinalIgnoreCase)))
        {
            return "[redacted]";
        }

        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(property => property.Name, property => Rewrite(property.Value, property.Name)),
            JsonValueKind.Array => element.EnumerateArray().Select(item => Rewrite(item, name)).ToArray(),
            JsonValueKind.String => Text(element.GetString(), 300),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number when element.TryGetDecimal(out var number) => number,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    [GeneratedRegex(@"(?i)(Bearer\s+)[A-Za-z0-9._~+/=-]{12,}")]
    private static partial Regex BearerRegex();

    [GeneratedRegex(@"(?i)\b(sk-ant-|sk-proj-|ghp_|github_pat_|eyJ)[A-Za-z0-9._-]{12,}\b")]
    private static partial Regex ApiTokenRegex();

    [GeneratedRegex(@"(?i)(/home/|/Users/)([^/]+)(/[^\s]*)?")]
    private static partial Regex HomePathRegex();

    [GeneratedRegex(@"(?i)([A-Z]:\\Users\\)([^\\]+)(\\[^\s]*)?")]
    private static partial Regex WindowsUserPathRegex();
}
