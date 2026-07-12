using ICU.Lib.ZaloClientWeb;
using ICU.Lib.ZaloClientWeb.Models;
using ICU.Lib.ZaloClientWeb.Demo.Helpers;
using ICU.Lib.ZaloClientWeb.Demo.Scenarios;

namespace ICU.Lib.ZaloClientWeb.Demo;

/// <summary>
/// Interactive demo application for ICU.Lib.ZaloClientWeb.
/// Features session persistence (auto-login) and logout.
/// Run this project to explore the library's capabilities.
/// </summary>
public class Program
{
    private static ZaloClient? _client;

    public static async Task Main(string[] args)
    {
        // Support Vietnamese UTF-8 characters in console
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("===========================================");
        Console.WriteLine("   ICU.Lib.ZaloClientWeb Demo Application");
        Console.WriteLine("   Unofficial Zalo API for .NET");
        Console.WriteLine("===========================================");
        Console.WriteLine();

        while (true)
        {
            var api = await TryAutoLoginOrShowLoginMenu();
            if (api == null) break;

            await ShowMainMenu(api);

            // Dispose client to prepare for possible re-login
            _client?.Dispose();
            _client = null;
        }

        Console.WriteLine("Goodbye!");
    }

    /// <summary>
    /// Attempts to auto-login from saved session. If failed/invalid, shows login menu.
    /// Returns null when user selects Exit.
    /// </summary>
    private static async Task<ZaloApi?> TryAutoLoginOrShowLoginMenu()
    {
        // Try loading saved session
        var saved = CredentialLoader.TryLoadSession();
        if (saved != null)
        {
            Console.WriteLine("Found saved session. Attempting auto-login...");
            _client = CreateClient();
            try
            {
                var api = await _client.LoginAsync(saved);
                Console.WriteLine($"Auto-login successful! Logged in as UID: {_client.Context?.Uid}");
                return api;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Auto-login failed: {ex.Message}");
                Console.WriteLine("Session may have expired. Please login again.");
                CredentialLoader.DeleteSession();
                _client?.Dispose();
                _client = null;
            }
        }

        return await ShowLoginMenu();
    }

    private static ZaloClient CreateClient()
    {
        return new ZaloClient(new ZaloOptions
        {
            Logging = true,
            ApiType = 30,
            ApiVersion = 671,
            ApiLogCallback = (msg) => Console.WriteLine(msg)
        });
    }

    private static async Task<ZaloApi?> ShowLoginMenu()
    {
        _client = CreateClient();

        while (true)
        {
            Console.WriteLine("--- LOGIN ---");
            Console.WriteLine("1. Login with cookies (from credentials file)");
            Console.WriteLine("2. Login with QR code");
            Console.WriteLine("3. Exit");
            Console.Write("Choose: ");

            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    var api1 = await LoginWithCookieAsync(_client);
                    if (api1 != null) return api1;
                    break;
                case "2":
                    var api2 = await LoginWithQrAsync(_client);
                    if (api2 != null) return api2;
                    break;
                case "3":
                    return null;
                default:
                    Console.WriteLine("Invalid choice. Try again.");
                    break;
            }
        }
    }

    private static async Task<ZaloApi?> LoginWithCookieAsync(ZaloClient client)
    {
        try
        {
            var credentials = CredentialLoader.LoadFromFile("credentials.example.json");
            Console.WriteLine("Logging in with cookies...");
            var api = await client.LoginAsync(credentials);
            Console.WriteLine($"Logged in as UID: {client.Context?.Uid}");

            // Save session for future auto-login
            if (client.Context != null)
            {
                var session = CredentialLoader.FromContext(client.Context);
                CredentialLoader.SaveSession(session);
            }

            return api;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Login failed: {ex.Message}");
            return null;
        }
    }

    private static async Task<ZaloApi?> LoginWithQrAsync(ZaloClient client)
    {
        try
        {
            Console.WriteLine("Starting QR login...");
            var api = await client.LoginWithQrAsync(
                qrPath: "zalo_qr.png",
                onQrCodeGenerated: (qrUrl) =>
                {
                    Console.WriteLine($"QR code saved to zalo_qr.png");
                    Console.WriteLine($"QR URL: {qrUrl}");
                    Console.WriteLine("Please scan the QR code with Zalo app.");
                }
            );
            Console.WriteLine($"Logged in as UID: {client.Context?.Uid}");

            // Save session for future auto-login
            if (client.Context != null)
            {
                var session = CredentialLoader.FromContext(client.Context);
                CredentialLoader.SaveSession(session);
            }

            return api;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"QR login failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Shows main menu. Returns false when user selects Logout/Exit.
    /// </summary>
    private static async Task ShowMainMenu(ZaloApi api)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("--- MAIN MENU ---");
            Console.WriteLine("0. Logout");
            Console.WriteLine("1. Listen to all WebSocket events");
            Console.WriteLine("2. Show account info");
            Console.WriteLine("3. Send a message");
            Console.WriteLine("4. Manage friends");
            Console.WriteLine("5. Manage groups (list info)");
            Console.WriteLine("6. Get stickers");
            Console.WriteLine("7. Echo bot (auto-reply)");
            Console.WriteLine("8. Exit");
            Console.Write("Choose: ");

            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "0":
                    // Logout: delete session and return to login menu
                    CredentialLoader.DeleteSession();
                    Console.WriteLine("Logged out successfully.");
                    return;
                case "1":
                    await WebSocketEventsDemo.RunAsync(api);
                    break;
                case "2":
                    await AccountInfoDemo.RunAsync(api);
                    break;
                case "3":
                    await MessageDemo.RunAsync(api);
                    break;
                case "4":
                    await FriendDemo.RunAsync(api);
                    break;
                case "5":
                    await GroupDemo.RunAsync(api);
                    break;
                case "6":
                    await StickerDemo.RunAsync(api);
                    break;
                case "7":
                    await EchoBotDemo.RunAsync(api);
                    break;
                case "8":
                    Console.WriteLine("Exiting...");
                    return;
                default:
                    Console.WriteLine("Invalid choice. Try again.");
                    break;
            }
        }
    }
}