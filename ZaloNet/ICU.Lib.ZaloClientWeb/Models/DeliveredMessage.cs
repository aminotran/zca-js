namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Represents a "delivered" event for a user message.
/// Equivalent to UserDeliveredMessage in zca-js.
/// </summary>
public class UserDeliveredMessage
{
    public string Uid { get; set; } = string.Empty;
    public string MsgId { get; set; } = string.Empty;
    public long Ts { get; set; }
}

/// <summary>
/// Represents a "delivered" event for a group message.
/// Equivalent to GroupDeliveredMessage in zca-js.
/// </summary>
public class GroupDeliveredMessage
{
    public string Uid { get; set; } = string.Empty;
    public string MsgId { get; set; } = string.Empty;
    public long Ts { get; set; }
    public string GroupId { get; set; } = string.Empty;
    public bool IsSelf { get; }
    public string ThreadId { get; }

    public GroupDeliveredMessage(string uid, object data)
    {
        // Will be populated from JsonElement in the listener
        IsSelf = false;
        ThreadId = string.Empty;
    }
}