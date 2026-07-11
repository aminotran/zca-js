using System.Text.Json;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Represents a "seen" event for a user (1-to-1) message.
/// Received via WebSocket when the other user has read the message.
/// Equivalent to UserSeenMessage in zca-js.
/// </summary>
public class UserSeenMessage
{
    /// <summary>User ID of the person who saw the message.</summary>
    public string Uid { get; set; } = string.Empty;
    /// <summary>Message ID that was seen.</summary>
    public string MsgId { get; set; } = string.Empty;
    /// <summary>Timestamp of the seen event (milliseconds).</summary>
    public long Ts { get; set; }
}

/// <summary>
/// Represents a "seen" event for a group message.
/// Received via WebSocket when group members have read the message.
/// Equivalent to GroupSeenMessage in zca-js.
/// </summary>
public class GroupSeenMessage
{
    /// <summary>User ID of the person who saw the message.</summary>
    public string Uid { get; set; } = string.Empty;
    /// <summary>Message ID that was seen.</summary>
    public string MsgId { get; set; } = string.Empty;
    /// <summary>Timestamp of the seen event (milliseconds).</summary>
    public long Ts { get; set; }
    /// <summary>Group ID where the message was seen.</summary>
    public string GroupId { get; set; } = string.Empty;
}