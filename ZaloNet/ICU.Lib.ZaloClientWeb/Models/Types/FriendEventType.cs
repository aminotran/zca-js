namespace ICU.Lib.ZaloClientWeb.Models.Types;

/// <summary>
/// Types of friend events that can occur in real-time via WebSocket.
/// Each value corresponds to the "act" string that Zalo sends in control events (cmd=601, act_type="fr").
/// </summary>
public enum FriendEventType
{
    /// <summary>
    /// Unknown/unrecognized event type.
    /// <para>Value: 0 | act: (unknown)</para>
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A user was added as a friend (accepted friend request).
    /// <para>Value: 1 | act: "add"</para>
    /// </summary>
    Add = 1,

    /// <summary>
    /// A friendship was removed (unfriended).
    /// <para>Value: 2 | act: "remove"</para>
    /// </summary>
    Remove = 2,

    /// <summary>
    /// A friend request was received.
    /// <para>Note: Zalo sends both "req" and "req_v2". This library ignores "req" and only processes "req_v2".</para>
    /// <para>Value: 3 | act: "req_v2"</para>
    /// </summary>
    Request = 3,

    /// <summary>
    /// A sent friend request was undone/cancelled.
    /// <para>Value: 4 | act: "undo_req"</para>
    /// </summary>
    UndoRequest = 4,

    /// <summary>
    /// A friend request was rejected/declined.
    /// <para>Value: 5 | act: "reject"</para>
    /// </summary>
    RejectRequest = 5,

    /// <summary>
    /// The user viewed/read their pending friend requests list.
    /// <para>Value: 6 | act: "seen_fr_req"</para>
    /// </summary>
    SeenFriendRequest = 6,

    /// <summary>
    /// A friend was blocked.
    /// <para>Value: 7 | act: "block"</para>
    /// </summary>
    Block = 7,

    /// <summary>
    /// A friend was unblocked.
    /// <para>Value: 8 | act: "unblock"</para>
    /// </summary>
    Unblock = 8,

    /// <summary>
    /// Call permissions were blocked for this friend.
    /// <para>Value: 9 | act: "block_call"</para>
    /// </summary>
    BlockCall = 9,

    /// <summary>
    /// Call permissions were unblocked for this friend.
    /// <para>Value: 10 | act: "unblock_call"</para>
    /// </summary>
    UnblockCall = 10,

    /// <summary>
    /// A pinned conversation was unpinned (or vice versa) in the friend's chat.
    /// <para>Value: 11 | act: "pin_unpin"</para>
    /// </summary>
    PinUnpin = 11,

    /// <summary>
    /// A new pinned topic was created in the friend's chat (pin note/message).
    /// <para>Value: 12 | act: "pin_create"</para>
    /// </summary>
    PinCreate = 12
}