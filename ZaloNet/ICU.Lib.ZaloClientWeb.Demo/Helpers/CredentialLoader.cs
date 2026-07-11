using System.Text.Json;
using ICU.Lib.ZaloClientWeb.Models;

namespace ICU.Lib.ZaloClientWeb.Demo.Helpers;

/// <summary>
/// Loads <see cref="Credentials"/> from a JSON file exported from browser.
/// Format example: credentials.example.json
/// </summary>
public static class CredentialLoader
{
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
        var credentials = JsonSerializer.Deserialize<Credentials>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (credentials == null || string.IsNullOrEmpty(credentials.Imei) || credentials.Cookie.Count == 0)
        {
            throw new InvalidOperationException("Invalid credentials file format.");
        }

        return credentials;
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

        var json = JsonSerializer.Serialize(example, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        File.WriteAllText(filePath, json);
        Console.WriteLine($"Created example file: {filePath}");
    }
}