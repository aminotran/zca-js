using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ICU.Lib.ZaloClientWeb.Models;
using ICU.Lib.ZaloClientWeb.Models.Types;
using ICU.Lib.ZaloClientWeb.WebSocket;

namespace ICU.Lib.ZaloClientWeb.Demo.Scenarios;

/// <summary>
/// Interactive chat demo that connects to WebSocket for real-time messaging.
/// Features:
/// - Fetches conversation list (both user and group)
/// - Opens a real-time chat session with any conversation
/// - Displays incoming messages live
/// - Lets you type and send messages
/// - Shows typing indicators
/// </summary>
public static class ChatDemo
{
    /// <summary>
    /// Cache for display names (userId → displayName)
    /// </summary>
    private static readonly Dictionary<string, string> _nameCache = new();
    private static ZaloListener? _listener;
    private static CancellationTokenSource? _chatCts;
    private static string? _currentThreadId;
    private static ThreadType _currentThreadType;
    private static string? _currentThreadName;
    private static long _ownUid;

    public static async Task RunAsync(ZaloApi api)
    {
        _ownUid = api.GetOwnId();
        _nameCache.Clear();

        while (true)
        {
            // Step 1: Show conversation list
            var selected = await SelectConversationAsync(api);
            if (selected == null) break; // user chose to exit

            _currentThreadId = selected.Value.ThreadId;
            _currentThreadType = selected.Value.IsGroup ? ThreadType.Group : ThreadType.User;
            _currentThreadName = selected.Value.Name;

            // Step 2: Enter chat session for this conversation
            await RunChatSessionAsync(api);
        }
    }

    private static async Task<(string ThreadId, bool IsGroup, string Name)?> SelectConversationAsync(ZaloApi api)
    {
        Console.WriteLine("\n╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              💬 REAL-TIME CHAT SELECTION                ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        Console.Write("Fetching conversations... ");
        var result = await api.GetConversationAsync();
        Console.WriteLine("Done.");

        if (!result.IsSuccess)
        {
            Console.WriteLine($"Failed: {result.Error}");
            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
            return null;
        }

        var root = result.Data;
        JsonElement convArray;

        if (root.TryGetProperty("data", out var dataWrapper) && dataWrapper.ValueKind == JsonValueKind.Object)
        {
            // Extract profiles/groupInfo from the response for name resolution
            if (dataWrapper.TryGetProperty("profiles", out var profiles) && profiles.ValueKind == JsonValueKind.Object)
            {
                foreach (var profile in profiles.EnumerateObject())
                {
                    var key = profile.Name;
                    var val = profile.Value;
                    var name = val.TryGetProperty("displayName", out var dn)
                        ? dn.GetString()
                        : val.TryGetProperty("name", out var n)
                            ? n.GetString()
                            : null;
                    if (name != null && !_nameCache.ContainsKey(key))
                        _nameCache[key] = name;
                }
            }

            if (dataWrapper.TryGetProperty("groupInfo", out var groupInfo) && groupInfo.ValueKind == JsonValueKind.Object)
            {
                foreach (var grp in groupInfo.EnumerateObject())
                {
                    var key = grp.Name;
                    var val = grp.Value;
                    var name = val.TryGetProperty("name", out var gn) ? gn.GetString() : null;
                    if (name != null && !_nameCache.ContainsKey(key))
                        _nameCache[key] = name;
                }
            }

            convArray = dataWrapper.TryGetProperty("conversations", out var convs)
                ? convs
                : root;
        }
        else
        {
            convArray = root;
        }

        if (convArray.ValueKind != JsonValueKind.Array)
        {
            Console.WriteLine("Unexpected response format.");
            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
            return null;
        }

        var items = new List<(string ThreadId, bool IsGroup, string Name, string LastMsg, long LastTime, int MemberCount)>();

        foreach (var conv in convArray.EnumerateArray())
        {
            var parsed = ParseConversationItem(conv);
            if (parsed != null) items.Add(parsed.Value);
        }

        if (items.Count == 0)
        {
            Console.WriteLine("No conversations found.");
            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
            return null;
        }

        // Display conversation list
        Console.WriteLine();
        Console.WriteLine($"  Found {items.Count} conversations. Select one to chat:");
        Console.WriteLine(new string('─', 100));

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var name = item.Name;
            if (name.Length > 25) name = name[..22] + "...";

            var typeStr = item.IsGroup ? "👥" : "👤";
            var lastMsg = item.LastMsg;
            if (lastMsg.Length > 45) lastMsg = lastMsg[..42] + "...";
            var timeStr = FormatTimestamp(item.LastTime);

            var unread = item.MemberCount > 0 && item.IsGroup ? $" [{item.MemberCount} members]" : "";

            Console.WriteLine($"  {i + 1,2}. {typeStr} {name,-25} {unread}");
            Console.WriteLine($"      └─ {lastMsg,-55} {timeStr,10}");
        }
        Console.WriteLine(new string('─', 100));
        Console.WriteLine("  0. Back to main menu");
        Console.WriteLine();

        Console.Write("Choose conversation #: ");
        var choice = Console.ReadLine()?.Trim();
        if (choice == "0" || string.IsNullOrEmpty(choice)) return null;

        if (int.TryParse(choice, out int idx) && idx >= 1 && idx <= items.Count)
        {
            var selected = items[idx - 1];
            return (selected.ThreadId, selected.IsGroup, selected.Name);
        }

        Console.WriteLine("Invalid choice.");
        return null;
    }

    private static (string ThreadId, bool IsGroup, string Name, string LastMsg, long LastTime, int MemberCount)? ParseConversationItem(JsonElement conv)
    {
        try
        {
            var threadId = conv.TryGetProperty("id", out var idEl)
                ? idEl.GetString()
                : conv.TryGetProperty("threadId", out var tidEl)
                    ? tidEl.GetString()
                    : null;
            if (string.IsNullOrEmpty(threadId)) return null;

            var isGroup = false;
            if (conv.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.Number)
                isGroup = typeEl.GetInt32() == 1;

            var name = conv.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
                ? nameEl.GetString()
                : null;
            if (string.IsNullOrEmpty(name) && _nameCache.TryGetValue(threadId, out var cached))
                name = cached;
            if (string.IsNullOrEmpty(name)) name = threadId;

            string? lastMsg = null;
            if (conv.TryGetProperty("lastMsg", out var lmEl) && lmEl.ValueKind == JsonValueKind.String)
                lastMsg = lmEl.GetString();
            else if (conv.TryGetProperty("notify", out var nfEl) && nfEl.ValueKind == JsonValueKind.String)
                lastMsg = nfEl.GetString();
            else if (conv.TryGetProperty("lastMessage", out var lmsEl) && lmsEl.ValueKind == JsonValueKind.String)
                lastMsg = lmsEl.GetString();

            long lastTime = 0;
            if (conv.TryGetProperty("lastTime", out var ltEl) && ltEl.ValueKind == JsonValueKind.Number)
                lastTime = ltEl.GetInt64();
            else if (conv.TryGetProperty("timestamp", out var tsEl) && tsEl.ValueKind == JsonValueKind.Number)
                lastTime = tsEl.GetInt64();

            int memberCount = 0;
            if (conv.TryGetProperty("memberCount", out var mcEl) && mcEl.ValueKind == JsonValueKind.Number)
                memberCount = mcEl.GetInt32();
            else if (conv.TryGetProperty("totalMember", out var tmEl) && tmEl.ValueKind == JsonValueKind.Number)
                memberCount = tmEl.GetInt32();

            return (threadId, isGroup, name ?? threadId, lastMsg ?? "", lastTime, memberCount);
        }
        catch
        {
            return null;
        }
    }

    private static async Task RunChatSessionAsync(ZaloApi api)
    {
        _chatCts = new CancellationTokenSource();
        var typeLabel = _currentThreadType == ThreadType.Group ? "GROUP 👥" : "USER 👤";

        Console.Clear();
        Console.WriteLine($"╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║  💬 CHATTING WITH: {_currentThreadName,-40} ║");
        Console.WriteLine($"║  📋 Type: {typeLabel,-16} ID: {_currentThreadId,-10}║");
        Console.WriteLine($"║  📝 Type message and press Enter to send                ║");
        Console.WriteLine($"║  📖 Commands: /q=quit  /info=details  /refresh=reload   ║");
        Console.WriteLine($"╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Fetch recent messages from the conversation
        Console.Write("Loading conversation history... ");
        await ShowConversationHistoryAsync(api);
        Console.WriteLine();
        Console.WriteLine(new string('─', 60));
        Console.WriteLine("  [Listening for new messages... Type your message below]");
        Console.WriteLine();

        // Start WebSocket listener
        _listener = api.Listener;
        _listener.MessageReceived += OnMessageReceived;
        _listener.TypingReceived += OnTypingReceived;
        _listener.SeenReceived += OnSeenReceived;
        _listener.DeliveredReceived += OnDeliveredReceived;
        _listener.UndoReceived += OnUndoReceived;

        // Connect if not already connected
        try
        {
            await _listener.StartAsync();
        }
        catch (InvalidOperationException)
        {
            // Already started, that's fine
        }

        // Main input loop
        while (!_chatCts.IsCancellationRequested)
        {
            Console.Write("> ");
            var input = Console.ReadLine();
            if (input == null) break;

            var trimmed = input.Trim();

            // Commands
            if (trimmed == "/q" || trimmed == "/quit")
                break;

            if (trimmed == "/info")
            {
                ShowCurrentConversationInfo(api);
                continue;
            }

            if (trimmed == "/refresh")
            {
                Console.Write("Reloading... ");
                await ShowConversationHistoryAsync(api);
                continue;
            }

            if (string.IsNullOrEmpty(trimmed)) continue;

            // Send message
            try
            {
                var sendResult = await api.SendMessageAsync(_currentThreadId!, trimmed, _currentThreadType);
                if (sendResult.IsSuccess)
                {
                    var timestamp = DateTime.Now.ToString("HH:mm:ss");
                    Console.WriteLine($"  [{timestamp}] You: {trimmed}");
                }
                else
                {
                    Console.WriteLine($"  ❌ Send failed: {sendResult.Error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Send error: {ex.Message}");
            }
        }

        // Cleanup
        if (_listener != null)
        {
            _listener.MessageReceived -= OnMessageReceived;
            _listener.TypingReceived -= OnTypingReceived;
            _listener.SeenReceived -= OnSeenReceived;
            _listener.DeliveredReceived -= OnDeliveredReceived;
            _listener.UndoReceived -= OnUndoReceived;
        }

        Console.WriteLine("\n  Chat session ended.");
    }

    private static async Task ShowConversationHistoryAsync(ZaloApi api)
    {
        try
        {
            if (_currentThreadType == ThreadType.Group)
            {
                var history = await api.GetGroupChatHistoryAsync(_currentThreadId!);
                if (history.IsSuccess && history.Data.ValueKind == JsonValueKind.Object)
                {
                    var data = history.Data;
                    // Try to find messages array - could be nested
                    JsonElement msgs = default;
                    if (data.TryGetProperty("data", out var inner) && inner.ValueKind == JsonValueKind.Object)
                    {
                        if (inner.TryGetProperty("messages", out var msgsEl) || inner.TryGetProperty("msgs", out msgsEl))
                            msgs = msgsEl;
                    }
                    else if (data.TryGetProperty("messages", out var msgsEl2))
                        msgs = msgsEl2;
                    else if (data.TryGetProperty("msgs", out var msgsEl3))
                        msgs = msgsEl3;

                    if (msgs.ValueKind == JsonValueKind.Array)
                    {
                        var count = 0;
                        foreach (var msg in msgs.EnumerateArray())
                        {
                            count++;
                            if (count <= 20) // show last 20 messages
                                PrintMessage(msg);
                        }
                        Console.WriteLine($"  (Showing last {Math.Min(count, 20)} of {count} messages)");
                    }
                    else
                    {
                        Console.WriteLine("  (No message history available)");
                    }
                }
                else
                {
                    Console.WriteLine("  (Could not load history)");
                }
            }
            else
            {
                // User chat: also try getGroupChatHistory or we use getContext which has recent messages
                // For user, the conversation list already has some context
                Console.WriteLine("  (User conversations loaded from conversation list)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  (Error loading history: {ex.Message})");
        }
    }

    private static void PrintMessage(JsonElement msgEl)
    {
        try
        {
            var uidFrom = msgEl.TryGetProperty("uidFrom", out var ufEl) ? ufEl.GetString() : "?";
            var dName = msgEl.TryGetProperty("dName", out var dnEl) ? dnEl.GetString() : null;
            var content = msgEl.TryGetProperty("content", out var cEl) && cEl.ValueKind == JsonValueKind.String
                ? cEl.GetString()
                : null;
            var notify = msgEl.TryGetProperty("notify", out var nfEl) ? nfEl.GetString() : null;
            var msgType = msgEl.TryGetProperty("msgType", out var mtEl) ? mtEl.GetString() : "";
            var ts = msgEl.TryGetProperty("ts", out var tsEl) ? tsEl.GetString() : "0";
            var timeStr = FormatTimestamp(long.TryParse(ts, out var tsLong) ? tsLong : 0);

            var displayText = content ?? notify ?? $"[{msgType}]";
            var senderName = dName;
            if (string.IsNullOrEmpty(senderName) && uidFrom != null && uidFrom != "0" && _nameCache.TryGetValue(uidFrom, out var cached))
                senderName = cached;
            if (string.IsNullOrEmpty(senderName))
                senderName = uidFrom == "0" ? "You" : uidFrom;

            var isSelf = uidFrom == "0";
            var prefix = isSelf ? "  You" : $"  {senderName}";

            Console.WriteLine($"  [{timeStr}] {prefix}: {displayText}");
        }
        catch { /* skip malformed messages */ }
    }

    // ─── WebSocket Event Handlers ────────────────────────────────────────

    private static void OnMessageReceived(object? sender, ZaloMessageEventArgs e)
    {
        // Only show messages for the current active chat
        string msgThreadId = "";
        string content = "";
        string senderName = "";
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        bool isSelf = e.IsSelf;

        if (e.Message is UserMessageInfo userMsg)
        {
            msgThreadId = userMsg.ThreadId;
            var msgData = userMsg.Data;
            content = msgData?.Content?.GetString() ?? msgData?.Notify ?? $"[{msgData?.MsgType}]";
            senderName = msgData?.DName ?? "";

            if (string.IsNullOrEmpty(senderName) && msgData?.UidFrom != null && msgData.UidFrom != _ownUid.ToString())
            {
                senderName = msgData.UidFrom;
                if (_nameCache.TryGetValue(msgData.UidFrom, out var cachedName))
                    senderName = cachedName;
            }
        }
        else if (e.Message is GroupMessageInfo grpMsg)
        {
            msgThreadId = grpMsg.ThreadId;
            var msgData = grpMsg.Data;
            content = msgData?.Content?.GetString() ?? msgData?.Notify ?? $"[{msgData?.MsgType}]";
            senderName = msgData?.DName ?? "";

            if (string.IsNullOrEmpty(senderName) && msgData?.UidFrom != null && msgData.UidFrom != _ownUid.ToString())
            {
                senderName = msgData.UidFrom;
                if (_nameCache.TryGetValue(msgData.UidFrom, out var cachedName))
                    senderName = cachedName;
            }
        }

        // Only show if it's for our current chat
        if (msgThreadId != _currentThreadId) return;

        if (isSelf) return; // We already echo our own sent messages

        if (content.Length > 100) content = content[..97] + "...";
        Console.WriteLine();
        Console.WriteLine($"  [{timestamp}] 💬 {senderName}: {content}");
        Console.Write("> ");
    }

    private static void OnTypingReceived(object? sender, ZaloTypingEventArgs e)
    {
        if (e.ThreadId != _currentThreadId) return;
        if (e.Uid == _ownUid.ToString()) return;

        var displayName = _nameCache.TryGetValue(e.Uid, out var name) ? name : e.Uid.Shorten();

        // We write on a new line to not disturb the input
        Console.WriteLine($"  ⌨️  {displayName} is typing...");
        Console.Write("> ");
    }

    private static void OnSeenReceived(object? sender, ZaloSeenDeliveredEventArgs e)
    {
        if (e.ThreadId != _currentThreadId) return;
        Console.WriteLine($"  👁️  Seen by {e.UserIds.Count} user(s)");
        Console.Write("> ");
    }

    private static void OnDeliveredReceived(object? sender, ZaloSeenDeliveredEventArgs e)
    {
        if (e.ThreadId != _currentThreadId) return;
        Console.WriteLine($"  ✅ Delivered");
        Console.Write("> ");
    }

    private static void OnUndoReceived(object? sender, ZaloUndoEventArgs e)
    {
        if (e.Undo?.ThreadId != _currentThreadId) return;
        Console.WriteLine($"  ↩️  Message was unsent/recalled");
        Console.Write("> ");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static void ShowCurrentConversationInfo(ZaloApi api)
    {
        Console.WriteLine();
        Console.WriteLine("  ┌─ Conversation Info ──────────────────────────────┐");
        Console.WriteLine($"  │ Name: {_currentThreadName,-42}│");
        Console.WriteLine($"  │ ID:   {_currentThreadId,-42}│");
        Console.WriteLine($"  │ Type: {(_currentThreadType == ThreadType.Group ? "Group 👥" : "User 👤"),-42}│");
        Console.WriteLine("  └──────────────────────────────────────────────────┘");
    }

    private static string FormatTimestamp(long timestampMs)
    {
        if (timestampMs <= 0) return "";
        try
        {
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);
            return dt.ToLocalTime().ToString("HH:mm:ss");
        }
        catch { return ""; }
    }
}