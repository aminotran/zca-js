namespace ICU.Lib.ZaloClientWeb.Models.Types;

/// <summary>
/// Types of group events that can occur in real-time via WebSocket.
/// Each value corresponds to the "act" string that Zalo sends in control events (cmd=601).
/// </summary>
public enum GroupEventType
{
    /// <summary>
    /// Unknown/unrecognized event type.
    /// <para>Value: 0 | act: (unknown)</para>
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Someone requested to join the group (requires approval).
    /// <para>Value: 1 | act: "join_request"</para>
    /// </summary>
    JoinRequest = 1,

    /// <summary>
    /// A user joined the group.
    /// <para>Value: 2 | act: "join"</para>
    /// </summary>
    Join = 2,

    /// <summary>
    /// A user left the group.
    /// <para>Value: 3 | act: "leave"</para>
    /// </summary>
    Leave = 3,

    /// <summary>
    /// A member was removed from the group.
    /// <para>Value: 4 | act: "remove_member"</para>
    /// </summary>
    RemoveMember = 4,

    /// <summary>
    /// A member was blocked in the group.
    /// <para>Value: 5 | act: "block_member"</para>
    /// </summary>
    BlockMember = 5,

    /// <summary>
    /// Group settings were updated (e.g., permissions changed).
    /// <para>Value: 6 | act: "update_setting"</para>
    /// </summary>
    UpdateSetting = 6,

    /// <summary>
    /// Group avatar was changed.
    /// <para>Value: 7 | act: "update_avatar"</para>
    /// </summary>
    UpdateAvatar = 7,

    /// <summary>
    /// General group info was updated (name, description, etc.).
    /// <para>Value: 8 | act: "update"</para>
    /// </summary>
    Update = 8,

    /// <summary>
    /// A new group join link was created.
    /// <para>Value: 9 | act: "new_link"</para>
    /// </summary>
    NewLink = 9,

    /// <summary>
    /// A member was promoted to admin/deputy.
    /// <para>Value: 10 | act: "add_admin"</para>
    /// </summary>
    AddAdmin = 10,

    /// <summary>
    /// An admin/deputy was demoted to regular member.
    /// <para>Value: 11 | act: "remove_admin"</para>
    /// </summary>
    RemoveAdmin = 11,

    /// <summary>
    /// A new pinned topic was created in the group board.
    /// <para>Value: 12 | act: "new_pin_topic"</para>
    /// </summary>
    NewPinTopic = 12,

    /// <summary>
    /// A pinned topic was updated.
    /// <para>Value: 13 | act: "update_pin_topic"</para>
    /// </summary>
    UpdatePinTopic = 13,

    /// <summary>
    /// A topic was updated (pinned note/message/poll).
    /// <para>Value: 14 | act: "update_topic"</para>
    /// </summary>
    UpdateTopic = 14,

    /// <summary>
    /// A board (table of contents) was updated in the group.
    /// <para>Value: 15 | act: "update_board"</para>
    /// </summary>
    UpdateBoard = 15,

    /// <summary>
    /// A board was removed from the group.
    /// <para>Value: 16 | act: "remove_board"</para>
    /// </summary>
    RemoveBoard = 16,

    /// <summary>
    /// Pinned topics were reordered in the group board.
    /// <para>Value: 17 | act: "reorder_pin_topic"</para>
    /// </summary>
    ReorderPinTopic = 17,

    /// <summary>
    /// A topic was unpinned from the group board.
    /// <para>Value: 18 | act: "unpin_topic"</para>
    /// </summary>
    UnpinTopic = 18,

    /// <summary>
    /// A topic was removed from the group board.
    /// <para>Value: 19 | act: "remove_topic"</para>
    /// </summary>
    RemoveTopic = 19,

    /// <summary>
    /// A user accepted a reminder in the group.
    /// <para>Value: 20 | act: "accept_remind"</para>
    /// </summary>
    AcceptRemind = 20,

    /// <summary>
    /// A user rejected a reminder in the group.
    /// <para>Value: 21 | act: "reject_remind"</para>
    /// </summary>
    RejectRemind = 21,

    /// <summary>
    /// A reminder was sent/triggered in the group topic.
    /// <para>Value: 22 | act: "remind_topic"</para>
    /// </summary>
    RemindTopic = 22
}