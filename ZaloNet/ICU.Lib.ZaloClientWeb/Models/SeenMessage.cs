namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Represents a "seen" event for a user message.
/// Equivalent to UserSeenMessage in zca-js.
/// </summary>
public class UserSeenMessage
{
    public string Uid { get; set; } = string.Empty;
    public string MsgId { get; set; } = string.Empty;
    public long Ts { get; set; }
}

/// <summary>
/// Represents a "seen" event for a group message.
/// Equivalent to GroupSeenMessage in zca-js.
/// </summary>
public class GroupSeenMessage
{
    public string Uid { get; set; } = string.Empty;
    public string MsgId { get; set; } = string.Empty;
    public long Ts { get; set; }
    public string GroupId { get; set; } = string.Empty;
    public bool IsSelf { get; }
    public string ThreadId { get; }

    public GroupSeenMessage(string uid, object data)
    {
        // Will be populated from JsonElement in the listener
        IsSelf = false;
        ThreadId = string.Empty;
    }
}