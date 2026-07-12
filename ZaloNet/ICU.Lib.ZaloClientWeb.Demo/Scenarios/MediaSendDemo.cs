using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ICU.Lib.ZaloClientWeb.Models;
using ICU.Lib.ZaloClientWeb.Models.Types;

namespace ICU.Lib.ZaloClientWeb.Demo.Scenarios;

/// <summary>
/// Demo for testing various media/link/card upload and send APIs.
/// </summary>
public static class MediaSendDemo
{
    public static async Task RunAsync(ZaloApi api)
    {
        while (true)
        {
            Console.WriteLine("\n╔════════════════════════════════════════════════╗");
            Console.WriteLine("║         📎 MEDIA SEND DEMO                    ║");
            Console.WriteLine("╚════════════════════════════════════════════════╝");
            Console.WriteLine("0. Back to main menu");
            Console.WriteLine("1. Send Link (URL)");
            Console.WriteLine("2. Send Video");
            Console.WriteLine("3. Send Voice");
            Console.WriteLine("4. Send Card (Contact)");
            Console.WriteLine("5. Upload & Send Image");
            Console.WriteLine("6. Upload & Send File");
            Console.Write("Choose: ");

            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "0": return;
                case "1": await TestSendLinkAsync(api); break;
                case "2": await TestSendVideoAsync(api); break;
                case "3": await TestSendVoiceAsync(api); break;
                case "4": await TestSendCardAsync(api); break;
                case "5": await TestUploadAndSendImageAsync(api); break;
                case "6": await TestUploadAndSendFileAsync(api); break;
                default:
                    Console.WriteLine("Invalid choice.");
                    break;
            }

            Console.WriteLine("\nPress Enter to continue...");
            Console.ReadLine();
        }
    }

    private static (string threadId, ThreadType type) GetThreadTarget()
    {
        Console.Write("Enter thread ID (UID or Group ID): ");
        var threadId = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(threadId)) return (null!, ThreadType.User);

        Console.Write("Type (0=User, 1=Group) [0]: ");
        var typeInput = Console.ReadLine()?.Trim();
        var threadType = typeInput == "1" ? ThreadType.Group : ThreadType.User;

        return (threadId, threadType);
    }

    // ─── 1. Send Link ─────────────────────────────────────────────────────
    private static async Task TestSendLinkAsync(ZaloApi api)
    {
        var (threadId, threadType) = GetThreadTarget();
        if (threadId == null) return;

        Console.Write("Enter URL: ");
        var url = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(url)) return;

        Console.Write("Optional message (press Enter to skip): ");
        var msg = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(msg)) msg = null;

        Console.WriteLine("Sending link...");
        var result = await api.SendLinkAsync(threadId, url, msg, threadType);

        if (result.IsSuccess)
            Console.WriteLine($"✅ Link sent! Data: {PrettyPrint(result.Data)}");
        else
            Console.WriteLine($"❌ Failed: {result.Error} (code: {result.ErrorCode})");
    }

    // ─── 2. Send Video ────────────────────────────────────────────────────
    private static async Task TestSendVideoAsync(ZaloApi api)
    {
        var (threadId, threadType) = GetThreadTarget();
        if (threadId == null) return;

        Console.Write("Enter video URL: ");
        var videoUrl = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(videoUrl)) return;

        Console.Write("Enter thumbnail URL: ");
        var thumbUrl = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(thumbUrl)) return;

        Console.Write("Optional message: ");
        var msg = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(msg)) msg = null;

        Console.Write("Duration (seconds, default=0): ");
        int.TryParse(Console.ReadLine()?.Trim(), out var duration);

        Console.Write("Width (default=1280): ");
        int.TryParse(Console.ReadLine()?.Trim(), out var width);
        if (width == 0) width = 1280;

        Console.Write("Height (default=720): ");
        int.TryParse(Console.ReadLine()?.Trim(), out var height);
        if (height == 0) height = 720;

        Console.WriteLine("Sending video...");
        var result = await api.SendVideoAsync(threadId, videoUrl, thumbUrl, msg, duration, width, height, threadType);

        if (result.IsSuccess)
            Console.WriteLine($"✅ Video sent! Data: {PrettyPrint(result.Data)}");
        else
            Console.WriteLine($"❌ Failed: {result.Error} (code: {result.ErrorCode})");
    }

    // ─── 3. Send Voice ────────────────────────────────────────────────────
    private static async Task TestSendVoiceAsync(ZaloApi api)
    {
        var (threadId, threadType) = GetThreadTarget();
        if (threadId == null) return;

        Console.Write("Enter voice URL (e.g. https://...): ");
        var voiceUrl = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(voiceUrl)) return;

        Console.Write("Duration (seconds, default=0): ");
        int.TryParse(Console.ReadLine()?.Trim(), out var duration);

        Console.Write("Optional message: ");
        var msg = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(msg)) msg = null;

        Console.WriteLine("Sending voice...");
        var result = await api.SendVoiceAsync(threadId, voiceUrl, duration, msg, threadType);

        if (result.IsSuccess)
            Console.WriteLine($"✅ Voice sent! Data: {PrettyPrint(result.Data)}");
        else
            Console.WriteLine($"❌ Failed: {result.Error} (code: {result.ErrorCode})");
    }

    // ─── 4. Send Card (Contact) ────────────────────────────────────────────
    private static async Task TestSendCardAsync(ZaloApi api)
    {
        var (threadId, threadType) = GetThreadTarget();
        if (threadId == null) return;

        Console.Write("Enter user ID to share as contact card: ");
        var userIdInput = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(userIdInput) || !long.TryParse(userIdInput, out var userId))
        {
            Console.WriteLine("Invalid user ID.");
            return;
        }

        Console.Write("Optional message: ");
        var msg = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(msg)) msg = null;

        Console.Write("Fetching QR code for contact card... ");
        var qrResult = await api.GetQrAsync(userId.ToString());
        Console.WriteLine(qrResult.IsSuccess ? "OK" : $"Failed: {qrResult.Error}");

        Console.WriteLine("Sending contact card...");
        var result = await api.SendCardAsync(threadId, userId, msg, threadType);

        if (result.IsSuccess)
            Console.WriteLine($"✅ Card sent! Data: {PrettyPrint(result.Data)}");
        else
            Console.WriteLine($"❌ Failed: {result.Error} (code: {result.ErrorCode})");
    }

    // ─── 5. Upload & Send Image ────────────────────────────────────────────
    private static async Task TestUploadAndSendImageAsync(ZaloApi api)
    {
        var (threadId, threadType) = GetThreadTarget();
        if (threadId == null) return;

        Console.Write("Enter image file path: ");
        var filePath = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Console.WriteLine("File not found.");
            return;
        }

        Console.WriteLine($"Uploading image ({filePath})...");
        try
        {
            var uploadResults = await api.UploadAttachmentAsync(
                new object[] { filePath },
                threadId,
                threadType
            );

            if (uploadResults.Count > 0)
            {
                var r = uploadResults[0];
                var normUrlPreview = r.NormalUrl?.Length > 80 ? r.NormalUrl[..80] + "..." : r.NormalUrl;
                Console.WriteLine($"✅ Upload success! Type={r.FileType}, PhotoId={r.PhotoId}, NormalUrl={normUrlPreview}");

                // After upload, send the attachment as a message
                Console.WriteLine("Sending attachment as message...");
                // Build the attachment data for the upload result
                // Note: This requires SendMessageAsync to support attachments
                // For now, just show the upload result
                Console.WriteLine("(Upload complete. Direct message sending with attachment needs SendMessageAsync update.)");
            }
            else
            {
                Console.WriteLine("❌ No upload results returned.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Upload failed: {ex.Message}");
        }
    }

    // ─── 6. Upload & Send File ─────────────────────────────────────────────
    private static async Task TestUploadAndSendFileAsync(ZaloApi api)
    {
        var (threadId, threadType) = GetThreadTarget();
        if (threadId == null) return;

        Console.Write("Enter file path: ");
        var filePath = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Console.WriteLine("File not found.");
            return;
        }

        Console.WriteLine($"Uploading file ({filePath})...");
        try
        {
            var uploadResults = await api.UploadAttachmentAsync(
                new object[] { filePath },
                threadId,
                threadType
            );

            if (uploadResults.Count > 0)
            {
                var r = uploadResults[0];
                var fileUrlPreview = r.FileUrl?.Length > 80 ? r.FileUrl[..80] + "..." : r.FileUrl;
                Console.WriteLine($"✅ Upload success! Type={r.FileType}, FileId={r.FileId}, FileUrl={fileUrlPreview}, Checksum={r.Checksum}");
            }
            else
            {
                Console.WriteLine("❌ No upload results returned.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Upload failed: {ex.Message}");
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────
    private static string PrettyPrint(JsonElement el)
    {
        try
        {
            return JsonSerializer.Serialize(el, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return el.ToString();
        }
    }
}