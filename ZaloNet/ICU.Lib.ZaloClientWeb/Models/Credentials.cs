using System.Collections.Generic;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Authentication credentials for Zalo API login.
/// </summary>
public class Credentials
{
    public string Imei { get; set; } = string.Empty;
    public List<CookieItem> Cookie { get; set; } = new();
    public string UserAgent { get; set; } = string.Empty;
    public string? Language { get; set; }
}

/// <summary>
/// Represents a single cookie from browser export.
/// </summary>
public class CookieItem
{
    public string Domain { get; set; } = string.Empty;
    public long ExpirationDate { get; set; }
    public bool HostOnly { get; set; }
    public bool HttpOnly { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string SameSite { get; set; } = string.Empty;
    public bool Secure { get; set; }
    public bool Session { get; set; }
    public string? StoreId { get; set; }
    public string Value { get; set; } = string.Empty;
}