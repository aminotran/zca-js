using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ICU.Lib.ZaloClientWeb;
using ICU.Lib.ZaloClientWeb.Models;
using ICU.Lib.ZaloClientWeb.Utils;
using ICU.Lib.ZaloClientWeb.Demo.Helpers;
using ICU.Lib.ZaloClientWeb.Models.Types;

namespace ICU.Lib.ZaloClientWeb.Demo.Scenarios;

public static class TestSendLink
{
    private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true };

    public static async Task RunAsync(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("=== Test SendLink ===");

        var saved = CredentialLoader.TryLoadSession();
        if (saved == null)
        {
            Console.WriteLine("No saved session found. Login first via Demo app.");
            return;
        }

        var client = new ZaloClient(new ZaloOptions
        {
            Logging = false,
            ApiType = 30,
            ApiVersion = 671,
        });

        try
        {
            var api = await client.LoginAsync(saved);
            var ctx = client.Context;
            Console.WriteLine($"Logged in as: {ctx?.Uid}");

            var threadId = "6253999119161452967";
            var url = "https://www.google.com/maps/@21.0599936,105.8111488,13z?entry=ttu&g_ep=EgoyMDI2MDcwOC4wIKXMDSoASAFQAw%3D%3D";
            var msg = "Đây là cái gì";

            // Test the current SendLinkAsync implementation first
            Console.WriteLine("\n--- Test 1: api.SendLinkAsync() (current implementation) ---");
            var result1 = await api.SendLinkAsync(threadId, url, msg, ThreadType.User);
            PrintResult("SendLinkAsync", result1);

            // If still failing, parse link data manually and print it
            if (!result1.IsSuccess)
            {
                Console.WriteLine("\n--- Debug: Parse raw parseLink result ---");
                var parseResult = await api.ParseLinkAsync(url);
                if (parseResult.IsSuccess)
                {
                    Console.WriteLine($"ParseLink data: {JsonSerializer.Serialize(parseResult.Data, PrettyJson)}");
                }
                else
                {
                    Console.WriteLine($"ParseLink failed: {parseResult.Error}");
                }

                // Try without message (msg=null)
                Console.WriteLine("\n--- Test 2: No optional message ---");
                var result2 = await api.SendLinkAsync(threadId, url, null, ThreadType.User);
                PrintResult("No message", result2);

                // Try with just URL as message
                Console.WriteLine("\n--- Test 3: Simple URL (example.com) ---");
                var result3 = await api.SendLinkAsync(threadId, "https://example.com", "test link", ThreadType.User);
                PrintResult("Simple URL", result3);

                // Try with another URL
                Console.WriteLine("\n--- Test 4: youtube URL ---");
                var result4 = await api.SendLinkAsync(threadId, "https://www.youtube.com/watch?v=dQw4w9WgXcQ", "video", ThreadType.User);
                PrintResult("YouTube", result4);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nException: {ex.Message}");
        }
        finally
        {
            client.Dispose();
        }

        Console.WriteLine("\nPress Enter to exit...");
        Console.ReadLine();
    }

    private static void PrintResult(string label, ZaloApiResponse<JsonElement> result)
    {
        Console.WriteLine($"  [{label}] Success: {result.IsSuccess}");
        Console.WriteLine($"  [{label}] Error: {result.Error ?? "(none)"}");
        Console.WriteLine($"  [{label}] ErrorCode: {result.ErrorCode}");
        if (result.IsSuccess)
        {
            var preview = result.Data.ToString();
            if (preview.Length > 120) preview = preview[..120] + "...";
            Console.WriteLine($"  [{label}] Data: {preview}");
        }
    }
}