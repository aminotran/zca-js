using System;
using System.Collections.Generic;
using System.Net;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Zalo API session context containing authentication state and configuration.
/// Equivalent to the ContextBase/ContextSession types in zca-js.
/// </summary>
public class ZaloContext
{
    public long Uid { get; set; }
    public string Imei { get; set; } = string.Empty;
    public CookieContainer CookieContainer { get; set; } = new();
    public string UserAgent { get; set; } = string.Empty;
    public string Language { get; set; } = "vi";
    public string? SecretKey { get; set; }
    public string? ZpwWs { get; set; }
    public string[]? ZpwWsUrls { get; set; }
    public Dictionary<string, string[]> ZpwServiceMapV3 { get; set; } = new();
    public Dictionary<string, object> Settings { get; set; } = new();
    public string? ExtraVer { get; set; }
    public LoginInfo? LoginInfo { get; set; }
    public ZaloOptions Options { get; set; }

    /// <summary>
    /// The API version number used for request parameters.
    /// </summary>
    public int ApiVersion => Options.ApiVersion;

    /// <summary>
    /// The API type number used for request parameters.
    /// </summary>
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