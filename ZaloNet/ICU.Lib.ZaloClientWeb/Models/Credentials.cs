using System.Collections.Generic;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Authentication credentials for Zalo API login.
/// Obtained by exporting cookies from a browser after logging into https://chat.zalo.me.
/// </summary>
public class Credentials
{
    /// <summary>Device IMEI/hardware identifier. Generate with <c>ZaloUtils.GenerateZaloUuid(userAgent)</c>.</summary>
    public string Imei { get; set; } = string.Empty;

    /// <summary>List of cookies exported from browser after logging into Zalo Web.
    /// Key cookies: "zpsid", "zpw_sek", "app.event.zalo.me", "zma" etc.</summary>
    public List<CookieItem> Cookie { get; set; } = new();

    /// <summary>User-Agent string matching the browser used to export cookies.
    /// Example: "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:133.0) Gecko/20100101 Firefox/133.0"</summary>
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>Language code for API requests. Default: "vi".</summary>
    public string? Language { get; set; }
}

/// <summary>
/// Represents a single cookie exported from browser.
/// Compatible with the format used by cookie export extensions (JSON format).
/// </summary>
public class CookieItem
{
    /// <summary>Cookie domain (e.g. ".chat.zalo.me").</summary>
    public string Domain { get; set; } = string.Empty;
    /// <summary>Cookie expiration timestamp (Unix epoch seconds).</summary>
    public long ExpirationDate { get; set; }
    /// <summary>Is host-only cookie? (not shared with subdomains).</summary>
    public bool HostOnly { get; set; }
    /// <summary>Is HTTP-only cookie? (inaccessible to JavaScript).</summary>
    public bool HttpOnly { get; set; }
    /// <summary>Cookie name (e.g. "zpsid", "zpw_sek", "zma").</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Cookie path (e.g. "/").</summary>
    public string Path { get; set; } = string.Empty;
    /// <summary>SameSite policy: "unspecified", "lax", "strict", "none".</summary>
    public string SameSite { get; set; } = string.Empty;
    /// <summary>Is secure cookie? (only sent over HTTPS).</summary>
    public bool Secure { get; set; }
    /// <summary>Is session cookie? (expires on browser close).</summary>
    public bool Session { get; set; }
    /// <summary>Store ID for browser storage.</summary>
    public string? StoreId { get; set; }
    /// <summary>Cookie value (the actual token/secret).</summary>
    public string Value { get; set; } = string.Empty;
}