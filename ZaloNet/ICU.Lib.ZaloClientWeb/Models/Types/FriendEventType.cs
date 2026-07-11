namespace ICU.Lib.ZaloClientWeb.Models.Types;

/// <summary>
/// Types of friend events that can occur in real-time.
/// Equivalent to FriendEventType enum in zca-js.
/// </summary>
public enum FriendEventType
{
    Add = 1,
    Remove = 2,
    Request = 3,
    UndoRequest = 4,
    RejectRequest = 5,
    SeenFriendRequest = 6,
    Block = 7,
    Unblock = 8,
    BlockCall = 9,
    UnblockCall = 10,
    PinUnpin = 11,
    PinCreate = 12,
    Unknown = 0
}