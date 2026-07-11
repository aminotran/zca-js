using ICU.Lib.ZaloClientWeb;

namespace ICU.Lib.ZaloClientWeb.Demo.Scenarios;

public static class StickerDemo
{
    public static async Task RunAsync(ZaloApi api)
    {
        Console.WriteLine("\n--- Stickers ---");

        var stickers = await api.GetStickersAsync();
        Console.WriteLine(stickers.IsSuccess ? $"Stickers: {stickers.Data}" : $"Error: {stickers.Error}");

        Console.WriteLine("Press Enter to continue...");
        Console.ReadLine();
    }
}