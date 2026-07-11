using ICU.Lib.ZaloClientWeb;
using ICU.Lib.ZaloClientWeb.Models;

namespace ICU.Lib.ZaloClientWeb.Demo.Scenarios;

public static class WebSocketEventsDemo
{
    public static async Task RunAsync(ZaloApi api)
    {
        Console.WriteLine("\n--- WebSocket Events (listening) ---");
        Console.WriteLine("Connecting to WebSocket...");

        api.Listener.MessageReceived += (_, args) =>
        {
            if (args.Message is UserMessageInfo userMsg)
            {
                Console.WriteLine($"[USER MSG] From {userMsg.Data.DName}: {userMsg.Data.Content}");
            }
            else if (args.Message is GroupMessageInfo grpMsg)
            {
                Console.WriteLine($"[GROUP MSG] {grpMsg.ThreadId} - {grpMsg.Data.DName}: {grpMsg.Data.Content}");
            }
        };

        api.Listener.TypingReceived += (_, args) =>
        {
            Console.WriteLine($"[TYPING] {(args.IsGroup ? "Group" : "User")} {args.ThreadId}");
        };

        api.Listener.ReactionReceived += (_, args) =>
        {
            foreach (var r in args.Reactions)
                Console.WriteLine($"[REACTION] {r.ThreadId}: {r.Data.Content?.RIcon}");
        };

        api.Listener.GroupEventReceived += (_, evt) =>
        {
            if (evt.Data is GroupEventBaseData baseData)
                Console.WriteLine($"[GROUP] {evt.Act} in {baseData.GroupName}");
            else
                Console.WriteLine($"[GROUP] {evt.Act} type={evt.Type}");
        };

        api.Listener.FriendEventReceived += (_, evt) =>
        {
            if (evt.Data is FriendEventRequestData req)
                Console.WriteLine($"[FRIEND] Request from {req.FromUid}: {req.Message}");
            else
                Console.WriteLine($"[FRIEND] Type={evt.Type}");
        };

        api.Listener.Connected += (_, _) => Console.WriteLine("[WS] Connected!");
        api.Listener.Disconnected += (_, args) => Console.WriteLine($"[WS] Disconnected: {args.CloseReason}");

        await api.Listener.StartAsync();

        Console.WriteLine("Listening for events. Press Enter to stop...");
        Console.ReadLine();

        await api.Listener.StopAsync();
        Console.WriteLine("Stopped listening.");
    }
}