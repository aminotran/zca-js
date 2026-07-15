using ICU.Lib.ZaloClientWeb.Demo.Helpers;
using ICU.Lib.ZaloClientWeb.Demo.Scenarios;
using ICU.Lib.ZaloClientWeb.Models;

namespace ICU.Lib.ZaloClientWeb.Demo;

public class Program
{
    private static ZaloClient? _client;

    public static async Task Main(string[] args)
    {
        // Support Vietnamese UTF-8 characters in console
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        // Quick test mode for SendLink
        if (args.Length > 0 && args[0] == "testlink")
        {
            await TestSendLink.RunAsync(args);
            return;
        }

        // Quick test mode for other isolated tests
        if (args.Length > 0 && args[0] == "scenario")
        {
            await RunLegacyScenario(args);
            return;
        }

        Console.WriteLine("===========================================");
        Console.WriteLine("   ICU.Lib.ZaloClientWeb Demo Application");
        Console.WriteLine("   Unofficial Zalo API for .NET");
        Console.WriteLine("===========================================");
        Console.WriteLine();

        while (true)
        {
            ZaloApi? api = await TryAutoLoginOrShowLoginMenu();
            if (api == null) break;

            // Launch the new Zalo Web Terminal interface
            await ZaloTerminalApp.RunAsync(api);

            // Dispose client to prepare for possible re-login
            _client?.Dispose();
            _client = null;
        }

        Console.WriteLine("Goodbye!");
    }

    private static async Task RunLegacyScenario(string[] args)
    {
        ZaloApi? api = await TryAutoLoginOrShowLoginMenu();
        if (api == null) return;

        if (args.Length > 1)
        {
            switch (args[1].ToLower())
            {
                case "echo":
                    await EchoBotDemo.RunAsync(api);
                    break;
                case "chat":
                    await ChatDemo.RunAsync(api);
                    break;
                case "message":
                    await MessageDemo.RunAsync(api);
                    break;
                case "friends":
                    await FriendDemo.RunAsync(api);
                    break;
                case "groups":
                    await GroupDemo.RunAsync(api);
                    break;
                case "conversations":
                    await ConversationListDemo.RunAsync(api);
                    break;
                case "media":
                    await MediaSendDemo.RunAsync(api);
                    break;
                case "sticker":
                    await StickerDemo.RunAsync(api);
                    break;
                case "account":
                    await AccountInfoDemo.RunAsync(api);
                    break;
                default:
                    Console.WriteLine($"Unknown scenario: {args[1]}");
                    break;
            }
        }

        _client?.Dispose();
        _client = null;
    }

    private static async Task<ZaloApi?> TryAutoLoginOrShowLoginMenu()
    {
        Credentials? saved = CredentialLoader.TryLoadSession();
        if (saved != null)
        {
            Console.WriteLine("Found saved session. Attempting auto-login...");
            _client = CreateClient();
            try
            {
                ZaloApi api = await _client.LoginAsync(saved);
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
            ApiLogCallback = (msg) => Console.WriteLine(msg),
            OnCookiesUpdated = (cookies) =>
            {
                // Auto-persist updated cookies to saved session
                if (_client?.Context != null)
                {
                    Credentials session = CredentialLoader.FromContext(_client.Context);
                    session.Cookie = cookies;
                    CredentialLoader.SaveSession(session);
                }
            }
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

            string? choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    ZaloApi? api1 = await LoginWithCookieAsync(_client);
                    if (api1 != null) return api1;
                    break;
                case "2":
                    ZaloApi? api2 = await LoginWithQrAsync(_client);
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
            Credentials credentials = CredentialLoader.LoadFromFile("credentials.example.json");
            Console.WriteLine("Logging in with cookies...");
            ZaloApi api = await client.LoginAsync(credentials);
            Console.WriteLine($"Logged in as UID: {client.Context?.Uid}");

            if (client.Context != null)
            {
                Credentials session = CredentialLoader.FromContext(client.Context);
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
            ZaloApi api = await client.LoginWithQrAsync(
                qrPath: "zalo_qr.png",
                onQrCodeGenerated: (qrUrl) =>
                {
                    Console.WriteLine($"QR code saved to zalo_qr.png");
                    Console.WriteLine($"QR URL: {qrUrl}");
                    Console.WriteLine("Please scan the QR code with Zalo app.");
                }
            );
            Console.WriteLine($"Logged in as UID: {client.Context?.Uid}");

            if (client.Context != null)
            {
                Credentials session = CredentialLoader.FromContext(client.Context);
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
}