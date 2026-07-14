using ICU.Lib.ZaloClientWeb.Models.Types;

namespace ICU.Lib.ZaloClientWeb.Demo.Models;

/// <summary>
/// Represents a single chat message formatted for display in the chat panel.
/// </summary>
public class DisplayMessage
{
    public string MessageId { get; set; } = "";
    public string SenderId { get; set; } = "";
    public string SenderName { get; set; } = "";
    public string Content { get; set; } = "";
    public string Notify { get; set; } = "";
    public long Timestamp { get; set; }
    public bool IsSelf { get; set; }
    public string ThreadId { get; set; } = "";
    public ThreadType ThreadType { get; set; }
}