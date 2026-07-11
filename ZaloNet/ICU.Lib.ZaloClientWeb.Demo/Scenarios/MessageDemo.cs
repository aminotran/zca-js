using ICU.Lib.ZaloClientWeb;
using ICU.Lib.ZaloClientWeb.Models.Types;

namespace ICU.Lib.ZaloClientWeb.Demo.Scenarios;

public static class MessageDemo
{
    public static async Task RunAsync(ZaloApi api)
    {
        Console.WriteLine("\n--- Send Message ---");
        Console.Write("Enter thread ID (UID or Group ID): ");
        var threadId = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(threadId)) return;

        Console.Write("Enter message text: ");
        var message = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(message)) return;

        Console.Write("Type (0=User, 1=Group) [0]: ");
        var typeInput = Console.ReadLine()?.Trim();
        var threadType = typeInput == "1" ? ThreadType.Group : ThreadType.User;

        var result = await api.SendMessageAsync(threadId, message, threadType);
        Console.WriteLine(result.IsSuccess
            ? $"Message sent successfully! Data: {result.Data}"
            : $"Failed: {result.Error}");

        Console.WriteLine("Press Enter to continue...");
        Console.ReadLine();
    }
}