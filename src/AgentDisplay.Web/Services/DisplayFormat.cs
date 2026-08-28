using System.Globalization;

namespace AgentDisplay.Web.Services;

public static class DisplayFormat
{
    public static string Money(decimal value, int decimals = 2) => value.ToString($"C{decimals}", CultureInfo.CurrentCulture);
    public static string Number(long value) => value.ToString("N0", CultureInfo.CurrentCulture);
    public static string Number(double value, int decimals = 0) => value.ToString($"N{decimals}", CultureInfo.CurrentCulture);
    public static string Compact(long value) => value switch
    {
        >= 1_000_000_000 => $"{(value / 1_000_000_000d).ToString("0.0", CultureInfo.CurrentCulture)}B",
        >= 1_000_000 => $"{(value / 1_000_000d).ToString("0.0", CultureInfo.CurrentCulture)}M",
        >= 1_000 => $"{(value / 1_000d).ToString("0.#", CultureInfo.CurrentCulture)}K",
        _ => Number(value)
    };
}
