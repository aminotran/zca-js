using System.Collections.Generic;
using System.Text.Json;
using ICU.Lib.ZaloClientWeb.Models.Types;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Friend request event data.
/// Equivalent to TFriendEventRequest in zca-js.
/// </summary>
public class FriendEventRequest
{
    public string ToUid { get; set; } = string.Empty;
    public string FromUid { get; set; } = string.Empty;
    public int Src { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Friend reject/undo event data.
/// Equivalent to TFriendEventRejectUndo in zca-js.
/// </summary>
public class FriendEventRejectUndo
{
    public string ToUid { get; set; } = string.Empty;
    public string FromUid { get; set; } = string.Empty;
}

/// <summary>
/// Friend event wrapper from WebSocket real-time events.
/// Equivalent to FriendEvent discriminated union in zca-js.
/// </summary>
public class FriendEvent
{
    public FriendEventType Type { get; set; }
    public JsonElement Data { get; set; }
    public string ThreadId { get; set; } = string.Empty;
    public bool IsSelf { get; set; }

    /// <summary>
    /// Initializes a friend event from raw WebSocket data.
    /// Equivalent to initializeFriendEvent() in zca-js.
    /// </summary>
    public static FriendEvent Initialize(string uid, JsonElement data, FriendEventType type)
    {
        string threadId;
        bool isSelf;

        switch (type)
        {
            case FriendEventType.Add:
            case FriendEventType.Remove:
            case FriendEventType.Block:
            case FriendEventType.Unblock:
            case FriendEventType.BlockCall:
            case FriendEventType.UnblockCall:
                threadId = data.ValueKind == JsonValueKind.String ? data.GetString() ?? "" : "";
                isSelf = type != FriendEventType.Add && type != FriendEventType.Remove;
                break;

            case FriendEventType.RejectRequest:
            case FriendEventType.UndoRequest:
                threadId = data.TryGetProperty("toUid", out var toUid) ? toUid.GetString() ?? "" : "";
                isSelf = data.TryGetProperty("fromUid", out var fromUid) && fromUid.GetString() == uid;
                break;

            case FriendEventType.Request:
                threadId = data.TryGetProperty("toUid", out var toUid2) ? toUid2.GetString() ?? "" : "";
                isSelf = data.TryGetProperty("fromUid", out var fromUid2) && fromUid2.GetString() == uid;
                break;

            case FriendEventType.SeenFriendRequest:
                threadId = uid;
                isSelf = true;
                break;

            case FriendEventType.PinCreate:
            case FriendEventType.PinUnpin:
                threadId = data.TryGetProperty("conversationId", out var convId) ? convId.GetString() ?? "" : "";
                isSelf = data.TryGetProperty("actorId", out var actorId) && actorId.GetString() == uid;
                break;

            default:
                threadId = "";
                isSelf = false;
                break;
        }

        return new FriendEvent
        {
            Type = type,
            Data = data.Clone(),
            ThreadId = threadId,
            IsSelf = isSelf
        };
    }
}