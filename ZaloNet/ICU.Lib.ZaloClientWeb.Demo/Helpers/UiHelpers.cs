namespace ICU.Lib.ZaloClientWeb.Demo.Helpers;

/// <summary>
/// Shared helper methods for the demo UI.
/// </summary>
public static class UiHelpers
{
    public static string Shorten(this string str, int maxLen = 12)
    {
        if (string.IsNullOrEmpty(str)) return "";
        if (str.Length <= maxLen) return str;
        return str[..(maxLen / 2)] + "…" + str[^(maxLen / 2)..];
    }

    public static string EscapeMarkupForSpectre(this string str)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Replace("[", "[[").Replace("]", "]]");
    }

    public static string FormatTimestamp(long timestampMs)
    {
        if (timestampMs <= 0) return "";
        try
        {
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);
            return dt.ToLocalTime().ToString("HH:mm:ss");
        }
        catch { return ""; }
    }

    public static string FormatTimeShort(long timestampMs)
    {
        if (timestampMs <= 0) return "";
        try
        {
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);
            var now = DateTimeOffset.Now;
            if (dt.Date == now.Date)
                return dt.ToLocalTime().ToString("HH:mm");
            if (dt.Year == now.Year)
                return dt.ToLocalTime().ToString("MM/dd HH:mm");
            return dt.ToLocalTime().ToString("yyyy/MM/dd");
        }
        catch { return ""; }
    }
}