using System.Text.Json;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Represents a "delivered" event for a user (1-to-1) message.
/// Received via WebSocket when the message has been delivered to the recipient's device.
/// Equivalent to UserDeliveredMessage in zca-js.
/// </summary>
public class UserDeliveredMessage
{
    /// <summary>User ID of the recipient who received the message.</summary>
    public string Uid { get; set; } = string.Empty;
    /// <summary>Message ID that was delivered.</summary>
    public string MsgId { get; set; } = string.Empty;
    /// <summary>Timestamp of the delivery event (milliseconds).</summary>
    public long Ts { get; set; }
}

/// <summary>
/// Represents a "delivered" event for a group message.
/// Received via WebSocket when the message has been delivered to group members.
/// Equivalent to GroupDeliveredMessage in zca-js.
/// </summary>
public class GroupDeliveredMessage
{
    /// <summary>User ID of the recipient who received the message.</summary>
    public string Uid { get; set; } = string.Empty;
    /// <summary>Message ID that was delivered.</summary>
    public string MsgId { get; set; } = string.Empty;
    /// <summary>Timestamp of the delivery event (milliseconds).</summary>
    public long Ts { get; set; }
    /// <summary>Group ID where the message was delivered.</summary>
    public string GroupId { get; set; } = string.Empty;
}