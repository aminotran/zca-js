namespace ICU.Lib.ZaloClientWeb.Demo.Models;

/// <summary>
/// Represents a single conversation item in the sidebar list.
/// </summary>
public class ConversationItemModel
{
    public string ThreadId { get; set; } = "";
    public bool IsGroup { get; set; }
    public string Name { get; set; } = "";
    public string LastMessage { get; set; } = "";
    public long LastTime { get; set; }
    public int MemberCount { get; set; }
    public int UnreadCount { get; set; }
    public bool IsPinned { get; set; }
    public bool IsOnline { get; set; }
}