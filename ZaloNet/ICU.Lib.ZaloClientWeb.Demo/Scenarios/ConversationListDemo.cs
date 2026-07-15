using ICU.Lib.ZaloClientWeb.Models.ApiModels.getUserInfoModel;
using ICU.Lib.ZaloClientWeb.Utils;
using System.Text.Json;

namespace ICU.Lib.ZaloClientWeb.Demo.Scenarios;

/// <summary>
/// Displays all conversations (both user and group chats).
/// Caches user/group names to avoid repeated API calls.
/// </summary>
public static class ConversationListDemo
{
    public static async Task RunAsync(ZaloApi api)
    {
        // Use a simple name cache populated from GetConversationAsync response
        Dictionary<string, string> nameCache = new();

        Console.WriteLine("\n--- Conversation List ---");
        Console.Write("Fetching conversations... ");
        ZaloApiResponse<JsonElement> result = await api.GetConversationAsync();
        Console.WriteLine("Done.");

        if (!result.IsSuccess)
        {
            Console.WriteLine($"Failed to get conversations: {result.Error}");
            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
            return;
        }

        JsonElement root = result.Data;

        // GetConversationAsync returns: { data: { conversations: [...], profiles: {...}, groupInfo: {...} } }
        JsonElement conversations;
        JsonElement dataWrapper = default;

        if (root.TryGetProperty("data", out dataWrapper) && dataWrapper.ValueKind == JsonValueKind.Object)
        {
            conversations = dataWrapper.TryGetProperty("conversations", out JsonElement convs)
                ? convs
                : root;
        }
        else
        {
            conversations = root;
        }

        // Extract profiles/groupInfo from the response for name resolution
        if (dataWrapper.ValueKind == JsonValueKind.Object)
        {
            if (dataWrapper.TryGetProperty("profiles", out JsonElement profiles) && profiles.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty profile in profiles.EnumerateObject())
                {
                    var key = profile.Name;
                    JsonElement val = profile.Value;
                    var name = val.TryGetProperty("displayName", out JsonElement dn)
                        ? dn.GetString()
                        : val.TryGetProperty("name", out JsonElement n)
                            ? n.GetString()
                            : null;
                    if (name != null && !nameCache.ContainsKey(key))
                        nameCache[key] = name;
                }
            }

            if (dataWrapper.TryGetProperty("groupInfo", out JsonElement groupInfo) && groupInfo.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty grp in groupInfo.EnumerateObject())
                {
                    var key = grp.Name;
                    JsonElement val = grp.Value;
                    var name = val.TryGetProperty("name", out JsonElement gn)
                        ? gn.GetString()
                        : null;
                    if (name != null && !nameCache.ContainsKey(key))
                        nameCache[key] = name;
                }
            }
        }
        if (conversations.ValueKind != JsonValueKind.Array)
        {
            Console.WriteLine("Unexpected response format. Raw data:");
            Console.WriteLine(root.GetRawText().Length > 500
                ? root.GetRawText()[..500] + "..."
                : root.GetRawText());
            Console.WriteLine("\nPress Enter to continue...");
            Console.ReadLine();
            return;
        }

        List<ConversationItem> items = new();
        foreach (JsonElement conv in conversations.EnumerateArray())
        {
            ConversationItem? item = ParseConversation(conv, nameCache);
            if (item != null) items.Add(item);
        }

        if (items.Count == 0)
        {
            Console.WriteLine("No conversations found.");
            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
            return;
        }

        // Display as table
        Console.WriteLine();
        Console.WriteLine($"Found {items.Count} conversations:");
        Console.WriteLine(new string('─', 120));

        // Header
        Console.WriteLine($"{"#",-4} {"Name",-30} {"Type",-10} {"Last Message",-55} {"Time",-15}");
        Console.WriteLine(new string('─', 120));

        for (int i = 0; i < items.Count; i++)
        {
            ConversationItem item = items[i];
            var nameDisplay = item.Name;
            if (nameDisplay.Length > 28) nameDisplay = nameDisplay[..25] + "...";

            var typeIcon = item.IsGroup ? "👥 Group" : "👤 User";
            var typeDisplay = item.IsGroup
                ? $"{typeIcon}  ({item.ThreadId.Shorten()})"
                : $"{typeIcon}  ({item.ThreadId.Shorten()})";

            var lastMsg = item.LastMessage ?? "";
            if (lastMsg.Length > 52) lastMsg = lastMsg[..49] + "...";

            var timeDisplay = FormatTime(item.LastTime);

            Console.WriteLine($"{i + 1,-4} {nameDisplay,-30} {typeDisplay,-10} {lastMsg,-55} {timeDisplay,-15}");
        }
        Console.WriteLine(new string('─', 120));
        Console.WriteLine();

        // Allow selecting a conversation to view details
        Console.Write("Enter # to view details (or Enter to skip): ");
        var choice = Console.ReadLine()?.Trim();
        if (int.TryParse(choice, out int idx) && idx >= 1 && idx <= items.Count)
        {
            ConversationItem selected = items[idx - 1];
            Console.WriteLine();
            Console.WriteLine("--- Conversation Details ---");
            Console.WriteLine($"  Thread ID: {selected.ThreadId}");
            Console.WriteLine($"  Name: {selected.Name}");
            Console.WriteLine($"  Type: {(selected.IsGroup ? "Group 👥" : "User 👤")}");

            if (selected.IsGroup && selected.MemberCount > 0)
                Console.WriteLine($"  Members: {selected.MemberCount}");

            Console.WriteLine($"  Last message: {selected.LastMessage}");
            Console.WriteLine($"  Last time: {FormatTime(selected.LastTime)}");

            // Try to get user info or group info
            if (selected.IsGroup)
            {
                try
                {
                    ZaloApiResponse<JsonElement> grpResult = await api.GetGroupInfoAsync(selected.ThreadId);
                    if (grpResult.IsSuccess)
                    {
                        Console.WriteLine("\n  Group Info (from API):");
                        SafePrint(grpResult.Data, "    ");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Could not fetch group info: {ex.Message}");
                }
            }
            else
            {
                try
                {
                    ZaloApiResponse<ResponseModel?> userResult = await api.GetUserInfoAsync(long.Parse(selected.ThreadId));
                    if (userResult.IsSuccess)
                    {
                        Console.WriteLine("\n  User Info (from API):");
                        Console.WriteLine($"    Phone: {userResult.Data.ChangedProfiles[selected.ThreadId].PhoneNumber}");
                        Console.WriteLine($"    Display Name: {userResult.Data.ChangedProfiles[selected.ThreadId].DisplayName}");
                    }
                }
                catch { /* ignore */ }
            }
        }

        Console.WriteLine("\nPress Enter to continue...");
        Console.ReadLine();
    }

    private static ConversationItem? ParseConversation(JsonElement conv, Dictionary<string, string> nameCache)
    {
        try
        {
            // Extract thread ID
            var threadId = conv.TryGetProperty("id", out JsonElement idEl)
                ? idEl.GetString()
                : conv.TryGetProperty("threadId", out JsonElement tidEl)
                    ? tidEl.GetString()
                    : null;
            if (string.IsNullOrEmpty(threadId)) return null;

            // Determine type: 0 = user, 1 = group
            var isGroup = false;
            if (conv.TryGetProperty("type", out JsonElement typeEl) && typeEl.ValueKind == JsonValueKind.Number)
                isGroup = typeEl.GetInt32() == 1;
            else if (conv.TryGetProperty("isGroup", out JsonElement igEl))
                isGroup = igEl.GetBoolean();

            // Name resolution: try cache first, then from conv data
            var name = conv.TryGetProperty("name", out JsonElement nameEl) && nameEl.ValueKind == JsonValueKind.String
                ? nameEl.GetString()
                : conv.TryGetProperty("userName", out JsonElement unEl)
                    ? unEl.GetString()
                    : null;

            if (string.IsNullOrEmpty(name) && nameCache.TryGetValue(threadId, out var cachedName))
                name = cachedName;

            if (string.IsNullOrEmpty(name))
                name = threadId; // fallback to ID

            // Last message
            string? lastMsg = null;
            if (conv.TryGetProperty("lastMsg", out JsonElement lastMsgEl) && lastMsgEl.ValueKind == JsonValueKind.String)
                lastMsg = lastMsgEl.GetString();
            else if (conv.TryGetProperty("lastMessage", out JsonElement lmEl) && lmEl.ValueKind == JsonValueKind.String)
                lastMsg = lmEl.GetString();
            else if (conv.TryGetProperty("notify", out JsonElement nfEl) && nfEl.ValueKind == JsonValueKind.String)
                lastMsg = nfEl.GetString();

            // Last activity timestamp
            long lastTime = 0;
            if (conv.TryGetProperty("lastTime", out JsonElement ltEl) && ltEl.ValueKind == JsonValueKind.Number)
                lastTime = ltEl.GetInt64();
            else if (conv.TryGetProperty("timestamp", out JsonElement tsEl) && tsEl.ValueKind == JsonValueKind.Number)
                lastTime = tsEl.GetInt64();

            // Member count (for groups)
            int memberCount = 0;
            if (conv.TryGetProperty("memberCount", out JsonElement mcEl) && mcEl.ValueKind == JsonValueKind.Number)
                memberCount = mcEl.GetInt32();
            else if (conv.TryGetProperty("totalMember", out JsonElement tmEl) && tmEl.ValueKind == JsonValueKind.Number)
                memberCount = tmEl.GetInt32();

            return new ConversationItem
            {
                ThreadId = threadId,
                IsGroup = isGroup,
                Name = name ?? threadId,
                LastMessage = lastMsg ?? "",
                LastTime = lastTime,
                MemberCount = memberCount,
            };
        }
        catch
        {
            return null;
        }
    }

    private static string FormatTime(long timestampMs)
    {
        if (timestampMs <= 0) return "";
        try
        {
            DateTimeOffset dt = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);
            DateTimeOffset now = DateTimeOffset.Now;
            if (dt.Date == now.Date)
                return dt.ToLocalTime().ToString("HH:mm");
            if (dt.Year == now.Year)
                return dt.ToLocalTime().ToString("MM/dd HH:mm");
            return dt.ToLocalTime().ToString("yyyy/MM/dd");
        }
        catch
        {
            return "";
        }
    }

    private static void SafePrint(JsonElement el, string indent)
    {
        try
        {
            var text = el.GetRawText();
            if (text.Length > 500)
                text = text[..500] + "...";
            Console.WriteLine($"{indent}{text}");
        }
        catch
        {
            Console.WriteLine($"{indent}(cannot display)");
        }
    }

    private class ConversationItem
    {
        public string ThreadId { get; set; } = "";
        public bool IsGroup { get; set; }
        public string Name { get; set; } = "";
        public string LastMessage { get; set; } = "";
        public long LastTime { get; set; }
        public int MemberCount { get; set; }
    }
}

/// <summary>
/// Extension helper to shorten long thread IDs for display.
/// </summary>
internal static class StringExtensions
{
    public static string Shorten(this string str, int maxLen = 10)
    {
        if (str.Length <= maxLen) return str;
        return str[..(maxLen / 2)] + ".." + str[^(maxLen / 2)..];
    }
}