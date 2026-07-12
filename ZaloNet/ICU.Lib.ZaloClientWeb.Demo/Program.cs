using ICU.Lib.ZaloClientWeb;
using ICU.Lib.ZaloClientWeb.Models;
using ICU.Lib.ZaloClientWeb.Demo.Scenarios;

namespace ICU.Lib.ZaloClientWeb.Demo;

/// <summary>
/// Interactive demo application for ICU.Lib.ZaloClientWeb.
/// Run this project to explore the library's capabilities.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("===========================================");
        Console.WriteLine("   ICU.Lib.ZaloClientWeb Demo Application");
        Console.WriteLine("   Unofficial Zalo API for .NET");
        Console.WriteLine("===========================================");
        Console.WriteLine();

        // Create client with default options
        var client = new ZaloClient(new ZaloOptions
        {
            Logging = true,
            ApiType = 30,
            ApiVersion = 671,
            ApiLogCallback = (msg) => Console.WriteLine(msg)
        });

        var api = await ShowLoginMenu(client);
        if (api == null) return;

        await ShowMainMenu(api);
    }

    private static async Task<ZaloApi?> ShowLoginMenu(ZaloClient client)
    {
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
                    return await LoginWithCookieAsync(client);
                case "2":
                    return await LoginWithQrAsync(client);
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
            var credentials = Helpers.CredentialLoader.LoadFromFile("credentials.example.json");
            Console.WriteLine("Logging in with cookies...");
            var api = await client.LoginAsync(credentials);
            Console.WriteLine($"Logged in as UID: {client.Context?.Uid}");
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
            return api;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"QR login failed: {ex.Message}");
            return null;
        }
    }

    private static async Task ShowMainMenu(ZaloApi api)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("--- MAIN MENU ---");
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
                    Console.WriteLine("Goodbye!");
                    return;
                default:
                    Console.WriteLine("Invalid choice. Try again.");
                    break;
            }
        }
    }
}