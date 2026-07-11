using ICU.Lib.ZaloClientWeb;
using ICU.Lib.ZaloClientWeb.Models;

namespace ICU.Lib.ZaloClientWeb.Demo.Scenarios;

public static class EchoBotDemo
{
    public static async Task RunAsync(ZaloApi api)
    {
        Console.WriteLine("\n--- Echo Bot ---");
        Console.WriteLine("Bot will auto-reply to incoming messages. Press Enter to stop.");

        api.Listener.MessageReceived += async (_, args) =>
        {
            if (args.Message is UserMessageInfo userMsg && !userMsg.IsSelf)
            {
                var reply = $"Echo: {userMsg.Data.Content}";
                Console.WriteLine($"Replying to {userMsg.Data.DName}: {reply}");
                await api.SendMessageAsync(userMsg.ThreadId, reply);
            }
        };

        await api.Listener.StartAsync();
        Console.ReadLine();
        await api.Listener.StopAsync();
    }
}