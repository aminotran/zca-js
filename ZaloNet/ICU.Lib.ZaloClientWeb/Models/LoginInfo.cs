using System.Collections.Generic;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Login information returned by Zalo API after successful authentication.
/// Contains session keys, service URLs, and user identity.
/// Equivalent to the parsed login response in zca-js.
/// </summary>
public class LoginInfo
{
    /// <summary>Logged-in user ID.</summary>
    public long Uid { get; set; }

    /// <summary>Encryption key (zpw_enk) used for decrypting API responses.
    /// This is the "secretKey" that enables AES-CBC decryption.</summary>
    public string? ZpwEnk { get; set; }

    /// <summary>WebSocket URL list (zpw_ws) for real-time connections.
    /// This is the full array from the login response.</summary>
    public string? ZpwWs { get; set; }

    /// <summary>Full WebSocket URL array from zpw_ws login response field.</summary>
    public string[]? ZpwWsUrls { get; set; }

    /// <summary>Service map (zpw_service_map_v3) mapping service names to endpoint URLs.
    /// Contains: chat, group, friend, sticker, reaction, file, profile, etc.</summary>
    public Dictionary<string, string[]> ZpwServiceMapV3 { get; set; } = new();

    /// <summary>send2me ID for cross-device sync.</summary>
    public string? Send2MeId { get; set; }

    /// <summary>Account language setting.</summary>
    public string? Language { get; set; }

    /// <summary>Public IP address of the user.</summary>
    public string? PublicIp { get; set; }

    /// <summary>Has PC client flag.</summary>
    public int Haspcclient { get; set; }
}