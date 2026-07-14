using System.Text.Json;
using ICU.Lib.ZaloClientWeb.Demo.Helpers;
using ICU.Lib.ZaloClientWeb.Demo.Models;
using ICU.Lib.ZaloClientWeb.Models;
using Spectre.Console;
using SpectreStyle = Spectre.Console.Style;

namespace ICU.Lib.ZaloClientWeb.Demo.Components;

/// <summary>
/// Renders a conversation sidebar panel (like Zalo Web's left pane).
/// Shows avatar, name, last message preview, relative time, and unread badge.
/// Uses Spectre.Console SelectionPrompt with keyboard navigation.
/// </summary>
public static class ConversationListComponent
{
    private static readonly Dictionary<string, string> _nameCache = new();

    /// <summary>
    /// Fetches and renders the conversation sidebar. Returns the selected conversation
    /// or null if the user chose to go back.
    /// </summary>
    public static async Task<ConversationItemModel?> SelectConversationAsync(ZaloApi api, long ownUid)
    {
        _nameCache.Clear();

        var items = await FetchConversationsAsync(api);
        if (items.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No conversations found.[/]");
            AnsiConsole.MarkupLine("[dim]Press any key to go back...[/]");
            Console.ReadKey(true);
            return null;
        }

        var selected = await RenderConversationSidebar(items);
        return selected;
    }

    private static async Task<List<ConversationItemModel>> FetchConversationsAsync(ZaloApi api)
    {
        var items = new List<ConversationItemModel>();

        try
        {
            var result = await api.GetConversationAsync();
            if (!result.IsSuccess) return items;

            var root = result.Data;
            JsonElement convArray;
            JsonElement dataWrapper = default;

            if (root.TryGetProperty("data", out dataWrapper) && dataWrapper.ValueKind == JsonValueKind.Object)
            {
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

            if (convArray.ValueKind != JsonValueKind.Array) return items;

            foreach (var conv in convArray.EnumerateArray())
            {
                var parsed = ParseConversationItem(conv);
                if (parsed != null) items.Add(parsed);
            }
        }
        catch
        {
            // Silently handle errors
        }

        return items;
    }

    private static ConversationItemModel? ParseConversationItem(JsonElement conv)
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

            int unread = 0;
            if (conv.TryGetProperty("unread", out var urEl) && urEl.ValueKind == JsonValueKind.Number)
                unread = urEl.GetInt32();

            return new ConversationItemModel
            {
                ThreadId = threadId,
                IsGroup = isGroup,
                Name = name ?? threadId,
                LastMessage = lastMsg ?? "",
                LastTime = lastTime,
                MemberCount = memberCount,
                UnreadCount = unread,
            };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<ConversationItemModel?> RenderConversationSidebar(List<ConversationItemModel> items)
    {
        var choices = items.Select(item => new SelectionChoice(item)).ToList();
        choices.Add(new SelectionChoice(null)); // Back option

        var selectedChoice = await AnsiConsole.PromptAsync(
            new SelectionPrompt<SelectionChoice>()
                .Title("[bold cyan]💬 Tin nhắn[/]")
                .PageSize(Math.Min(12, choices.Count))
                .MoreChoicesText("[dim](↑↓ scroll, Enter select)[/]")
                .HighlightStyle(new SpectreStyle(Color.Black, Color.Yellow, Decoration.Bold))
                .AddChoices(choices));

        return selectedChoice.Item;
    }

    /// <summary>
    /// Wrapper type for Spectre.Console selection so we get typed results.
    /// </summary>
    private class SelectionChoice : IEquatable<SelectionChoice>
    {
        public ConversationItemModel? Item { get; }

        public SelectionChoice(ConversationItemModel? item)
        {
            Item = item;
        }

        public override string ToString()
        {
            if (Item == null) return "↩ Back";

            var typeIcon = Item.IsGroup ? "👥" : "👤";
            var name = Item.Name;
            if (name.Length > 22) name = name[..19] + "…";

            var lastMsg = Item.LastMessage;
            if (lastMsg.Length > 30) lastMsg = lastMsg[..27] + "…";
            if (string.IsNullOrEmpty(lastMsg)) lastMsg = "(no messages)";

            var timeStr = FormatTime(Item.LastTime);
            var unreadBadge = Item.UnreadCount > 0 ? $" [bold white on red] {Item.UnreadCount} [/]" : "";

            var nameColor = Item.IsGroup ? "cyan" : "green";
            return $"{typeIcon} [bold {nameColor}]{name.EscapeMarkupForSpectre()}[/]{unreadBadge}\n  [dim]{lastMsg.EscapeMarkupForSpectre()} · {timeStr.EscapeMarkupForSpectre()}[/]";
        }

        public bool Equals(SelectionChoice? other)
        {
            if (other is null) return false;
            if (Item is null && other.Item is null) return true;
            if (Item is null || other.Item is null) return false;
            return Item.ThreadId == other.Item.ThreadId;
        }

        public override bool Equals(object? obj) => obj is SelectionChoice other && Equals(other);
        public override int GetHashCode() => Item?.ThreadId?.GetHashCode() ?? 0;

        private static string FormatTime(long timestampMs)
        {
            if (timestampMs <= 0) return "";
            try
            {
                var dt = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);
                var now = DateTimeOffset.Now;
                if (dt.Date == now.Date)
                    return dt.ToLocalTime().ToString("HH:mm");
                if (dt.Year == now.Year)
                    return dt.ToLocalTime().ToString("MM/dd HH:mm");
                return dt.ToLocalTime().ToString("yyyy/MM/dd");
            }
            catch { return ""; }
        }
    }

}
