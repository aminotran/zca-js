using System.Text.Json;
using ICU.Lib.ZaloClientWeb.Demo.Helpers;
using ICU.Lib.ZaloClientWeb.Demo.Models;
using ICU.Lib.ZaloClientWeb.Models;
using ICU.Lib.ZaloClientWeb.Models.Types;
using ICU.Lib.ZaloClientWeb.WebSocket;
using Spectre.Console;
using SpectreStyle = Spectre.Console.Style;

namespace ICU.Lib.ZaloClientWeb.Demo.Components;

/// <summary>
/// Renders a chat panel similar to Zalo Web's conversation area.
/// Displays message history as bubbles, supports sending messages,
/// and handles WebSocket events (typing, seen, delivered, undo).
/// </summary>
public static class ChatPanelComponent
{
    private static CancellationTokenSource? _chatCts;
    private static readonly List<DisplayMessage> _messages = new();
    private static string? _currentThreadId;

    /// <summary>
    /// Opens a real-time chat session for the given conversation.
    /// </summary>
    public static async Task RunChatSessionAsync(ZaloApi api, ConversationItemModel conversation, long ownUid)
    {
        _chatCts = new CancellationTokenSource();
        _messages.Clear();
        _currentThreadId = conversation.ThreadId;

        var threadType = conversation.IsGroup ? ThreadType.Group : ThreadType.User;
        AnsiConsole.Clear();

        // Header
        var headerPanel = new Panel(
            Align.Center(new Markup(
                $"[bold yellow]{conversation.Name.EscapeMarkupForSpectre()}[/]  " +
                $"{(conversation.IsGroup ? "👥 Group" : "👤 User")}  " +
                $"[dim]({conversation.ThreadId.Shorten()})[/]"))
        )
        {
            BorderStyle = new SpectreStyle(Color.Blue),
            Padding = new Padding(1, 0, 1, 0)
        };
        AnsiConsole.Write(headerPanel);
        AnsiConsole.WriteLine();

        // Hook WebSocket events
        var listener = api.Listener;
        listener.MessageReceived += OnMessageReceived;
        listener.TypingReceived += OnTypingReceived;
        listener.SeenReceived += OnSeenReceived;
        listener.DeliveredReceived += OnDeliveredReceived;
        listener.UndoReceived += OnUndoReceived;

        try
        {
            await listener.StartAsync();
        }
        catch (InvalidOperationException) { }

        // Main input loop
        while (!_chatCts.IsCancellationRequested)
        {
            RenderMessages();

            Console.Write(" [bold]➤[/] ");
            var input = Console.ReadLine();
            var trimmed = input?.Trim() ?? "";

            if (trimmed == "/q" || trimmed == "/quit" || trimmed == "/back")
                break;

            if (trimmed == "/help")
            {
                ShowHelp();
                continue;
            }

            if (trimmed == "/info")
            {
                ShowConversationInfo(conversation);
                continue;
            }

            if (string.IsNullOrEmpty(trimmed)) continue;

            // Detect commands
            if (trimmed.StartsWith("/"))
            {
                await HandleCommandAsync(api, trimmed, conversation, threadType);
                continue;
            }

            // Send text message
            try
            {
                var sendResult = await api.SendMessageAsync(
                    conversation.ThreadId, trimmed, threadType);

                if (sendResult.IsSuccess)
                {
                    _messages.Add(new DisplayMessage
                    {
                        SenderName = "You",
                        Content = trimmed,
                        Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                        IsSelf = true,
                    });
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]❌ Send failed: {sendResult.Error}[/]");
                    AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
                    Console.ReadKey(true);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]❌ Error: {ex.Message}[/]");
                AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
                Console.ReadKey(true);
            }
        }

        // Cleanup WebSocket handlers
        listener.MessageReceived -= OnMessageReceived;
        listener.TypingReceived -= OnTypingReceived;
        listener.SeenReceived -= OnSeenReceived;
        listener.DeliveredReceived -= OnDeliveredReceived;
        listener.UndoReceived -= OnUndoReceived;

        _chatCts.Cancel();
        _chatCts.Dispose();
    }

    private static void RenderMessages()
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine($"[bold blue]💬 Chat[/] [dim]· {_messages.Count} messages[/]");
        AnsiConsole.WriteLine();

        if (_messages.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim italic]  No messages yet. Start the conversation![/]");
            AnsiConsole.WriteLine();
            Console.WriteLine("  /help for available commands");
            AnsiConsole.WriteLine();
            return;
        }

        var displayMessages = _messages.TakeLast(15).ToList();
        foreach (var msg in displayMessages)
        {
            var timeStr = UiHelpers.FormatTimestamp(msg.Timestamp);
            var sender = msg.IsSelf
                ? "[bold green]You[/]"
                : $"[bold yellow]{msg.SenderName.EscapeMarkupForSpectre()}[/]";

            AnsiConsole.MarkupLine($"  [dim]{timeStr}[/] {sender}");
            AnsiConsole.MarkupLine($"  {(msg.IsSelf ? "[green on black]" : "[white on black]")}│ {msg.Content.EscapeMarkupForSpectre()} [/]");
            AnsiConsole.WriteLine();
        }

        Console.WriteLine("  ─────────────────────────────────");
    }

    private static void ShowHelp()
    {
        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.AddColumn("Command");
        table.AddColumn("Description");
        table.AddRow("/q, /quit, /back", "Exit chat session");
        table.AddRow("/help", "Show this help");
        table.AddRow("/img <path>", "Send an image file");
        table.AddRow("/file <path>", "Send a file");
        table.AddRow("/sticker <id>", "Send a sticker");
        table.AddRow("/info", "Show conversation info");

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
        Console.ReadKey(true);
    }

    private static void ShowConversationInfo(ConversationItemModel conversation)
    {
        var infoTable = new Table();
        infoTable.Border = TableBorder.Rounded;
        infoTable.AddColumn("Property");
        infoTable.AddColumn("Value");
        infoTable.AddRow("Name", conversation.Name);
        infoTable.AddRow("Thread ID", conversation.ThreadId);
        infoTable.AddRow("Type", conversation.IsGroup ? "Group 👥" : "User 👤");
        infoTable.AddRow("Last message", conversation.LastMessage);
        infoTable.AddRow("Last activity", UiHelpers.FormatTimeShort(conversation.LastTime));
        if (conversation.IsGroup)
            infoTable.AddRow("Members", conversation.MemberCount.ToString());
        AnsiConsole.Write(infoTable);
        AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
        Console.ReadKey(true);
    }

    private static async Task HandleCommandAsync(
        ZaloApi api, string command, ConversationItemModel conversation, ThreadType threadType)
    {
        var parts = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLower();

        switch (cmd)
        {
            case "/img":
            case "/image":
            case "/file":
                if (parts.Length < 2)
                {
                    AnsiConsole.MarkupLine("[red]Usage: /img <filepath>[/]");
                    break;
                }
                var filePath = parts[1].Trim('"');
                if (!System.IO.File.Exists(filePath))
                {
                    AnsiConsole.MarkupLine($"[red]File not found: {filePath}[/]");
                    break;
                }
                try
                {
                    var msg = new MessageContent
                    {
                        Msg = cmd == "/file" ? null : "📷 Image",
                        Attachments = new List<object> { filePath }
                    };
                    var result = await api.SendMessageAsync(msg, conversation.ThreadId, threadType);
                    if (result.IsSuccess)
                    {
                        _messages.Add(new DisplayMessage
                        {
                            SenderName = "You",
                            Content = cmd == "/file"
                                ? $"📎 {System.IO.Path.GetFileName(filePath)}"
                                : "📷 Image",
                            Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                            IsSelf = true,
                        });
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Send failed: {ex.Message}[/]");
                }
                break;

            case "/sticker":
                if (parts.Length < 2)
                {
                    AnsiConsole.MarkupLine("[red]Usage: /sticker <stickerId>[/]");
                    break;
                }
                try
                {
                    var stickerId = parts[1];
                    var stkResult = await api.SendStickerAsync(
                        conversation.ThreadId, int.Parse(stickerId), 0, threadType);
                    if (stkResult.IsSuccess)
                    {
                        _messages.Add(new DisplayMessage
                        {
                            SenderName = "You",
                            Content = $"🎨 Sticker {stickerId}",
                            Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                            IsSelf = true,
                        });
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Sticker failed: {ex.Message}[/]");
                }
                break;

            default:
                AnsiConsole.MarkupLine($"[red]Unknown command: {cmd}. Type /help for available commands.[/]");
                break;
        }
    }

    // ─── WebSocket Event Handlers ────────────────────────────────────────

    private static void OnMessageReceived(object? sender, ZaloMessageEventArgs e)
    {
        string msgThreadId = "";
        string content = "";
        string senderName = "";

        if (e.Message is UserMessageInfo userMsg)
        {
            msgThreadId = userMsg.ThreadId;
            var msgData = userMsg.Data;
            content = msgData?.Content?.GetString() ?? msgData?.Notify ?? $"[{msgData?.MsgType}]";
            senderName = msgData?.DName ?? msgData?.UidFrom ?? "";
        }
        else if (e.Message is GroupMessageInfo grpMsg)
        {
            msgThreadId = grpMsg.ThreadId;
            var msgData = grpMsg.Data;
            content = msgData?.Content?.GetString() ?? msgData?.Notify ?? $"[{msgData?.MsgType}]";
            senderName = msgData?.DName ?? msgData?.UidFrom ?? "";
        }

        if (e.IsSelf) return;

        // Add message to our list (will be shown on next render)
        _messages.Add(new DisplayMessage
        {
            SenderName = senderName,
            Content = content,
            Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
            IsSelf = false,
        });
    }

    private static void OnTypingReceived(object? sender, ZaloTypingEventArgs e)
    {
        if (e.Uid == null) return;
        Console.WriteLine($"  ⌨️  {e.Uid.Shorten()} is typing...");
    }

    private static void OnSeenReceived(object? sender, ZaloSeenDeliveredEventArgs e)
    {
        Console.WriteLine($"  👁️  Seen by {e.UserIds.Count} user(s)");
    }

    private static void OnDeliveredReceived(object? sender, ZaloSeenDeliveredEventArgs e)
    {
        Console.WriteLine($"  ✅ Delivered");
    }

    private static void OnUndoReceived(object? sender, ZaloUndoEventArgs e)
    {
        Console.WriteLine($"  ↩️  Message was unsent");
    }
}