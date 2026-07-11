using System.Text.Json;
using ICU.Lib.ZaloClientWeb.Models.Types;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Data for friend events where the data is just a user ID string.
/// Used by FriendEventType: Add, Remove, Block, Unblock, BlockCall, UnblockCall.
/// </summary>
public class FriendEventUserData
{
    /// <summary>The user ID involved in the event.</summary>
    public string Uid { get; set; } = string.Empty;

    public static FriendEventUserData FromJson(JsonElement data)
    {
        return new FriendEventUserData { Uid = data.ValueKind == JsonValueKind.String ? data.GetString() ?? "" : "" };
    }
}

/// <summary>
/// Data for a friend request event.
/// Used by FriendEventType: Request.
/// Equivalent to TFriendEventRequest in zca-js.
/// </summary>
public class FriendEventRequestData
{
    /// <summary>Recipient (target) user ID.</summary>
    public string ToUid { get; set; } = string.Empty;
    /// <summary>Sender user ID.</summary>
    public string FromUid { get; set; } = string.Empty;
    /// <summary>Source of the request (e.g. 0=unknown, 1=phone, 2=QR, 3=group).</summary>
    public int Src { get; set; }
    /// <summary>Optional friend request message.</summary>
    public string Message { get; set; } = string.Empty;

    public static FriendEventRequestData FromJson(JsonElement data)
    {
        return new FriendEventRequestData
        {
            ToUid = data.TryGetProperty("toUid", out var to) ? to.GetString() ?? "" : "",
            FromUid = data.TryGetProperty("fromUid", out var from) ? from.GetString() ?? "" : "",
            Src = data.TryGetProperty("src", out var src) ? src.GetInt32() : 0,
            Message = data.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : ""
        };
    }
}

/// <summary>
/// Data for friend request reject or undo events.
/// Used by FriendEventType: RejectRequest, UndoRequest.
/// Equivalent to TFriendEventRejectUndo in zca-js.
/// </summary>
public class FriendEventRejectUndoData
{
    /// <summary>Target user ID who was rejected/undone.</summary>
    public string ToUid { get; set; } = string.Empty;
    /// <summary>User ID who performed the reject/undo.</summary>
    public string FromUid { get; set; } = string.Empty;

    public static FriendEventRejectUndoData FromJson(JsonElement data)
    {
        return new FriendEventRejectUndoData
        {
            ToUid = data.TryGetProperty("toUid", out var to) ? to.GetString() ?? "" : "",
            FromUid = data.TryGetProperty("fromUid", out var from) ? from.GetString() ?? "" : ""
        };
    }
}

/// <summary>
/// Data for a seen friend request event (user viewed their pending requests).
/// Used by FriendEventType: SeenFriendRequest.
/// </summary>
public class FriendEventSeenData
{
    /// <summary>List of user IDs whose requests were seen.</summary>
    public string[]? Uids { get; set; }

    public static FriendEventSeenData FromJson(JsonElement data)
    {
        if (data.ValueKind == JsonValueKind.Array)
        {
            var uids = new System.Collections.Generic.List<string>();
            foreach (var item in data.EnumerateArray())
                uids.Add(item.GetString() ?? "");
            return new FriendEventSeenData { Uids = uids.ToArray() };
        }
        return new FriendEventSeenData();
    }
}

/// <summary>
/// Data for friend pin events (create or unpin a topic in 1-to-1 chat).
/// Used by FriendEventType: PinCreate, PinUnpin.
/// </summary>
public class FriendEventPinData
{
    /// <summary>Conversation ID where the pin occurred.</summary>
    public string ConversationId { get; set; } = string.Empty;
    /// <summary>User ID who performed the action.</summary>
    public string ActorId { get; set; } = string.Empty;

    public static FriendEventPinData FromJson(JsonElement data)
    {
        return new FriendEventPinData
        {
            ConversationId = data.TryGetProperty("conversationId", out var conv) ? conv.GetString() ?? "" : "",
            ActorId = data.TryGetProperty("actorId", out var actor) ? actor.GetString() ?? "" : ""
        };
    }
}

/// <summary>
/// Friend event wrapper from WebSocket real-time events.
/// <c>Data</c> is strongly-typed based on <c>Type</c>:
/// <list type="bullet">
///   <item><see cref="FriendEventType.Add"/>, <see cref="FriendEventType.Remove"/> → <see cref="FriendEventUserData"/></item>
///   <item><see cref="FriendEventType.Request"/> → <see cref="FriendEventRequestData"/></item>
///   <item><see cref="FriendEventType.RejectRequest"/>, <see cref="FriendEventType.UndoRequest"/> → <see cref="FriendEventRejectUndoData"/></item>
///   <item><see cref="FriendEventType.SeenFriendRequest"/> → <see cref="FriendEventSeenData"/></item>
///   <item><see cref="FriendEventType.PinCreate"/>, <see cref="FriendEventType.PinUnpin"/> → <see cref="FriendEventPinData"/></item>
/// </list>
/// </summary>
public class FriendEvent
{
    /// <summary>Type of friend event.</summary>
    public FriendEventType Type { get; set; }

    /// <summary>Strongly-typed data object. Cast to the appropriate type based on <see cref="Type"/>.</summary>
    public object? Data { get; set; }

    /// <summary>Thread/conversation ID related to this event.</summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>True if the event was triggered by the current logged-in user.</summary>
    public bool IsSelf { get; set; }

    /// <summary>
    /// Initializes a friend event from raw WebSocket data.
    /// Returns the event with strongly-typed Data based on the event type.
    /// </summary>
    public static FriendEvent Initialize(string uid, JsonElement rawData, FriendEventType type)
    {
        string threadId;
        bool isSelf;
        object? data;

        switch (type)
        {
            case FriendEventType.Add:
            case FriendEventType.Remove:
            case FriendEventType.Block:
            case FriendEventType.Unblock:
            case FriendEventType.BlockCall:
            case FriendEventType.UnblockCall:
                var userData = FriendEventUserData.FromJson(rawData);
                threadId = userData.Uid;
                isSelf = type != FriendEventType.Add && type != FriendEventType.Remove;
                data = userData;
                break;

            case FriendEventType.RejectRequest:
            case FriendEventType.UndoRequest:
                var rejectData = FriendEventRejectUndoData.FromJson(rawData);
                threadId = rejectData.ToUid;
                isSelf = rejectData.FromUid == uid;
                data = rejectData;
                break;

            case FriendEventType.Request:
                var reqData = FriendEventRequestData.FromJson(rawData);
                threadId = reqData.ToUid;
                isSelf = reqData.FromUid == uid;
                data = reqData;
                break;

            case FriendEventType.SeenFriendRequest:
                threadId = uid;
                isSelf = true;
                data = FriendEventSeenData.FromJson(rawData);
                break;

            case FriendEventType.PinCreate:
            case FriendEventType.PinUnpin:
                var pinData = FriendEventPinData.FromJson(rawData);
                threadId = pinData.ConversationId;
                isSelf = pinData.ActorId == uid;
                data = pinData;
                break;

            default:
                threadId = "";
                isSelf = false;
                data = rawData.Clone();
                break;
        }

        return new FriendEvent
        {
            Type = type,
            Data = data,
            ThreadId = threadId,
            IsSelf = isSelf
        };
    }
}