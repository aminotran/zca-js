using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json.Serialization;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Zalo API session context containing authentication state and configuration.
/// Equivalent to the ContextBase/ContextSession types in zca-js.
/// This is created after successful login and holds all necessary data for API calls.
/// </summary>
public class ZaloContext
{
    /// <summary>Logged-in user ID (numeric string, e.g. "123456789").</summary>
    public long Uid { get; set; }
    /// <summary>Device IMEI/hardware ID used for authentication.</summary>
    public string Imei { get; set; } = string.Empty;
    /// <summary>Cookie container for storing Zalo session cookies (zpsid, zpw_sek, etc.).</summary>
    public CookieContainer CookieContainer { get; set; } = new();
    /// <summary>User agent string sent in all API requests.</summary>
    public string UserAgent { get; set; } = string.Empty;
    /// <summary>Language code (e.g. "vi", "en").</summary>
    public string Language { get; set; } = "vi";
    /// <summary>AES encryption key (zpw_enk) used for request/response encryption.
    /// Setting this value indicates the context is fully authenticated.</summary>
    public string? SecretKey { get; set; }
    /// <summary>WebSocket endpoint URL for real-time connections.</summary>
    public string? ZpwWs { get; set; }
    /// <summary>Array of WebSocket fallback URLs for redundancy/rotation.</summary>
    public string[]? ZpwWsUrls { get; set; }
    /// <summary>Service endpoint map (zpw_service_map_v3) — maps service names to API URL arrays.
    /// Keys include: chat, group, friend, sticker, reaction, profile, file, etc.</summary>
    public Dictionary<string, string[]> ZpwServiceMapV3 { get; set; } = new();
    /// <summary>Zalo server settings including socket config, share file limits, keepalive settings.</summary>
    public Dictionary<string, object> Settings { get; set; } = new();
    /// <summary>Extra version info for phonebook, contacts, stickers, etc.</summary>
    public string? ExtraVer { get; set; }
    /// <summary>Full login response data.</summary>
    public LoginInfo? LoginInfo { get; set; }
    /// <summary>Client configuration options.</summary>
    public ZaloOptions Options { get; set; }

    /// <summary>The API version number used for "zpw_ver" parameter in request URLs.
    /// Default: 671. Higher values may enable newer features.</summary>
    public int ApiVersion => Options.ApiVersion;
    /// <summary>The API type number used for "zpw_type" parameter in request URLs.
    /// Default: 30. This identifies the client platform type.</summary>
    public int ApiType => Options.ApiType;

    public ZaloContext(ZaloOptions options)
    {
        Options = options;
    }

    /// <summary>
    /// Checks if the context is fully authenticated (has a secret key).
    /// Equivalent to isContextSession() in zca-js.
    /// </summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(SecretKey);
}