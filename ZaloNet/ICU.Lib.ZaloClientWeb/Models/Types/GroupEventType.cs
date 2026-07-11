namespace ICU.Lib.ZaloClientWeb.Models.Types;

/// <summary>
/// Types of group events that can occur in real-time.
/// Equivalent to GroupEventType enum in zca-js.
/// </summary>
public enum GroupEventType
{
    JoinRequest = 1,
    Join = 2,
    Leave = 3,
    RemoveMember = 4,
    BlockMember = 5,
    UpdateSetting = 6,
    UpdateAvatar = 7,
    Update = 8,
    NewLink = 9,
    AddAdmin = 10,
    RemoveAdmin = 11,
    NewPinTopic = 12,
    UpdatePinTopic = 13,
    UpdateTopic = 14,
    UpdateBoard = 15,
    RemoveBoard = 16,
    ReorderPinTopic = 17,
    UnpinTopic = 18,
    RemoveTopic = 19,
    AcceptRemind = 20,
    RejectRemind = 21,
    RemindTopic = 22,
    Unknown = 0
}