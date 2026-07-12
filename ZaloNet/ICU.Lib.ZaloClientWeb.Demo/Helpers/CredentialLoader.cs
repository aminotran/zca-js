using System.Text.Json;
using ICU.Lib.ZaloClientWeb.Models;

namespace ICU.Lib.ZaloClientWeb.Demo.Helpers;

/// <summary>
/// Loads <see cref="Credentials"/> from a JSON file exported from browser.
/// Also supports saving/loading sessions for persistent login.
/// Format example: credentials.example.json
/// </summary>
public static class CredentialLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private const string SessionFileName = "session.json";

    /// <summary>
    /// Loads credentials from a JSON file.
    /// Expected format: { "imei": "...", "userAgent": "...", "cookie": [ ... ] }
    /// </summary>
    public static Credentials LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Credentials file not found: {filePath}");
            Console.WriteLine("Creating example file...");
            CreateExampleFile(filePath);
            throw new FileNotFoundException(
                $"Please edit '{filePath}' with your Zalo credentials first.");
        }

        var json = File.ReadAllText(filePath);
        var credentials = JsonSerializer.Deserialize<Credentials>(json, JsonOptions);

        if (credentials == null || string.IsNullOrEmpty(credentials.Imei) || credentials.Cookie.Count == 0)
        {
            throw new InvalidOperationException("Invalid credentials file format.");
        }

        return credentials;
    }

    /// <summary>
    /// Tries to load a saved session from session.json.
    /// Returns null if no session exists or if loading fails.
    /// </summary>
    public static Credentials? TryLoadSession()
    {
        if (!File.Exists(SessionFileName)) return null;
        try
        {
            var json = File.ReadAllText(SessionFileName);
            return JsonSerializer.Deserialize<Credentials>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Saves credentials as a persistent session file (session.json).
    /// This allows the demo to auto-login next time.
    /// </summary>
    public static void SaveSession(Credentials credentials)
    {
        var json = JsonSerializer.Serialize(credentials, JsonOptions);
        var fullPath = Path.GetFullPath(SessionFileName);
        File.WriteAllText(SessionFileName, json);
        Console.WriteLine($"Session saved to {fullPath}");
    }

    /// <summary>
    /// Deletes the saved session file.
    /// Called on logout.
    /// </summary>
    public static void DeleteSession()
    {
        var fullPath = Path.GetFullPath(SessionFileName);
        if (File.Exists(SessionFileName))
        {
            File.Delete(SessionFileName);
            Console.WriteLine($"Session file {fullPath} deleted.");
        }
        else
        {
            Console.WriteLine($"Session file {fullPath} not found.");
        }
    }

    /// <summary>
    /// Converts a ZaloContext into Credentials for session persistence.
    /// Extracts cookies from all known Zalo subdomains in the CookieContainer.
    /// </summary>
    public static Credentials FromContext(ZaloContext context)
    {
        var cookies = new List<CookieItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Iterate through all known Zalo domains to collect cookies
        var baseDomains = new[] { "chat.zalo.me" };
        var subdomains = new[]
        {
            "chat.zalo.me", "wpa.chat.zalo.me",
            "tt-profile-wpa.chat.zalo.me", "tt-friend-wpa.chat.zalo.me",
            "tt-group-wpa.chat.zalo.me", "tt-sticker-wpa.chat.zalo.me",
            "tt-chat-wpa.chat.zalo.me", "tt-convers-wpa.chat.zalo.me",
            "tt-alias-wpa.chat.zalo.me",
        };

        foreach (var domain in subdomains)
        {
            try
            {
                var uri = new Uri($"https://{domain}");
                foreach (System.Net.Cookie cookie in context.CookieContainer.GetCookies(uri))
                {
                    var key = $"{cookie.Name}={cookie.Domain}";
                    if (seen.Add(key))
                    {
                        cookies.Add(new CookieItem
                        {
                            Domain = cookie.Domain,
                            Name = cookie.Name,
                            Value = cookie.Value,
                            Path = cookie.Path,
                            Secure = cookie.Secure,
                            HttpOnly = cookie.HttpOnly,
                            ExpirationDate = cookie.Expires.Kind != DateTimeKind.Local
                                ? new DateTimeOffset(cookie.Expires, TimeSpan.Zero).ToUnixTimeSeconds()
                                : 0
                        });
                    }
                }
            }
            catch { }
        }

        return new Credentials
        {
            Imei = context.Imei,
            UserAgent = context.UserAgent,
            Language = context.Language,
            Cookie = cookies
        };
    }

    private static void CreateExampleFile(string filePath)
    {
        var example = new
        {
            imei = "your-imei-here (generate with ZaloUtils.GenerateZaloUuid)",
            userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36...",
            cookie = new[]
            {
                new { name = "zpsid", value = "your-zpsid-value", domain = ".chat.zalo.me", path = "/" },
                new { name = "zpw_sek", value = "your-zpw_sek-value", domain = ".chat.zalo.me", path = "/" },
            },
            language = "vi"
        };

        var json = JsonSerializer.Serialize(example, JsonOptions);
        File.WriteAllText(filePath, json);
        Console.WriteLine($"Created example file: {filePath}");
    }
}