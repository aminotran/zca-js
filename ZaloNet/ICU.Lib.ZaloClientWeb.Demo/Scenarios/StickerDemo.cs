using ICU.Lib.ZaloClientWeb;

namespace ICU.Lib.ZaloClientWeb.Demo.Scenarios;

public static class StickerDemo
{
    public static async Task RunAsync(ZaloApi api)
    {
        Console.WriteLine("\n--- Stickers ---");

        Console.Write("Enter sticker keyword (e.g., 'happy', 'love'): ");
        var keyword = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(keyword)) keyword = "happy";
        var stickers = await api.GetStickersAsync(keyword);
        Console.WriteLine(stickers.IsSuccess ? $"Stickers: {stickers.Data}" : $"Error: {stickers.Error}");

        Console.WriteLine("Press Enter to continue...");
        Console.ReadLine();
    }
}