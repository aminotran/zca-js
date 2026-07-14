using System.Collections.Generic;
using System.Text.Json;
using ICU.Lib.ZaloClientWeb.Models.Types;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Group settings/permissions that control member actions.
/// Zalo uses 0/1 integer flags for most settings.
/// Equivalent to GroupSetting in zca-js.
/// </summary>
public class GroupSetting
{
    /// <summary>Block changing member nickname? 0 = allow, 1 = block.</summary>
    public int BlockName { get; set; }
    /// <summary>Sign messages from admins? 0 = no, 1 = yes (show "admin" badge).</summary>
    public int SignAdminMsg { get; set; }
    /// <summary>Only admins can add members? 0 = anyone, 1 = admins only.</summary>
    public int AddMemberOnly { get; set; }
    /// <summary>Only admins can set topics? 0 = anyone, 1 = admins only.</summary>
    public int SetTopicOnly { get; set; }
    /// <summary>Enable message history for new members? 0 = no, 1 = yes.</summary>
    public int EnableMsgHistory { get; set; }
    /// <summary>Require admin approval for new join requests? 0 = no, 1 = yes.</summary>
    public int JoinAppr { get; set; }
    /// <summary>Lock creating posts? 0 = allow, 1 = lock.</summary>
    public int LockCreatePost { get; set; }
    /// <summary>Lock creating polls? 0 = allow, 1 = lock.</summary>
    public int LockCreatePoll { get; set; }
    /// <summary>Lock sending messages? 0 = allow, 1 = lock (mute all).</summary>
    public int LockSendMsg { get; set; }
    /// <summary>Lock viewing member list? 0 = allow, 1 = lock.</summary>
    public int LockViewMember { get; set; }
    /// <summary>Banned feature flags (bitmask).</summary>
    public int BannFeature { get; set; }
    /// <summary>Media dirty flag.</summary>
    public int DirtyMedia { get; set; }
    /// <summary>Ban duration in minutes (for temporary bans).</summary>
    public int BanDuration { get; set; }
}

/// <summary>
/// Types of group topics (pinned items in the group board).
/// </summary>
public enum GroupTopicType
{
    /// <summary>Pinned note/text. Value: 0</summary>
    Note = 0,
    /// <summary>Pinned message (reply from chat). Value: 2</summary>
    Message = 2,
    /// <summary>Pinned poll. Value: 3</summary>
    Poll = 3
}

/// <summary>
/// Group topic (pinned message/note/poll) in the group board.
/// Equivalent to GroupTopic in zca-js.
/// </summary>
public class GroupTopic
{
    /// <summary>Topic type: 0=note, 2=message, 3=poll.</summary>
    public GroupTopicType Type { get; set; }
    /// <summary>Theme color as negative color number.</summary>
    public int Color { get; set; }
    /// <summary>Emoji icon for the topic.</summary>
    public string Emoji { get; set; } = string.Empty;
    /// <summary>Topic start time (timestamp in milliseconds).</summary>
    public long StartTime { get; set; }
    /// <summary>Topic duration in seconds (0 = permanent).</summary>
    public int Duration { get; set; }
    /// <summary>Parameters specific to the topic type (varies by Type).</summary>
    public JsonElement Params { get; set; }
    /// <summary>Topic ID.</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>Creator's user ID.</summary>
    public string CreatorId { get; set; } = string.Empty;
    /// <summary>Topic creation time (timestamp in milliseconds).</summary>
    public long CreateTime { get; set; }
    /// <summary>Last editor's user ID.</summary>
    public string EditorId { get; set; } = string.Empty;
    /// <summary>Last edit time (timestamp in milliseconds).</summary>
    public long EditTime { get; set; }
    /// <summary>Repeat interval in seconds (for recurring reminders).</summary>
    public int Repeat { get; set; }
    /// <summary>Action type: 0=create, 1=update, 2=delete.</summary>
    public int Action { get; set; }
}

/// <summary>
/// Zalo group classification types.
/// </summary>
public enum GroupType
{
    /// <summary>Regular group chat. Value: 1</summary>
    Group = 1,
    /// <summary>Community group (larger, with hierarchical structure). Value: 2</summary>
    Community = 2
}

/// <summary>
/// Information about a current member in a group.
/// Equivalent to GroupCurrentMem in zca-js.
/// </summary>
public class GroupCurrentMember
{
    /// <summary>User ID.</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>Display name in the group (nickname).</summary>
    public string DName { get; set; } = string.Empty;
    /// <summary>Zalo account name.</summary>
    public string ZaloName { get; set; } = string.Empty;
    /// <summary>Avatar URL.</summary>
    public string Avatar { get; set; } = string.Empty;
    /// <summary>25px avatar thumbnail URL. Equivalent to avatar_25 in zca-js.</summary>
    public string Avatar25 { get; set; } = string.Empty;
    /// <summary>Account status: 0=normal, other=restricted.</summary>
    public int AccountStatus { get; set; }
    /// <summary>Member type: 0=member, 1=deputy, 2=owner.</summary>
    public int Type { get; set; }
}

/// <summary>
/// Extra info for a group (media store flag).
/// Equivalent to GroupInfo.extraInfo in zca-js.
/// </summary>
public class GroupExtraInfo
{
    /// <summary>Enable media store? 0=disabled, 1=enabled.</summary>
    public int EnableMediaStore { get; set; }
}

/// <summary>
/// Full group information from Zalo API.
/// Equivalent to GroupInfo type in zca-js.
/// </summary>
public class GroupFullInfo
{
    /// <summary>Group ID (unique identifier).</summary>
    public string GroupId { get; set; } = string.Empty;
    /// <summary>Group name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Group description.</summary>
    public string Desc { get; set; } = string.Empty;
    /// <summary>Group type: 1=regular, 2=community.</summary>
    public GroupType Type { get; set; }
    /// <summary>Creator's user ID.</summary>
    public long CreatorId { get; set; }
    /// <summary>Group version string (incremented on changes).</summary>
    public string Version { get; set; } = string.Empty;
    /// <summary>Group avatar URL (thumbnail).</summary>
    public string Avt { get; set; } = string.Empty;
    /// <summary>Group avatar URL (full size).</summary>
    public string FullAvt { get; set; } = string.Empty;
    /// <summary>List of member user IDs.</summary>
    public List<string> MemberIds { get; set; } = new();
    /// <summary>List of admin user IDs.</summary>
    public List<string> AdminIds { get; set; } = new();
    /// <summary>Current members with details.</summary>
    public List<GroupCurrentMember> CurrentMems { get; set; } = new();
    /// <summary>Updated members (changes since last fetch). Equivalent to updateMems in zca-js.</summary>
    public List<object> UpdateMems { get; set; } = new();
    /// <summary>Admin list with details. Equivalent to admins in zca-js.</summary>
    public List<object> Admins { get; set; } = new();
    /// <summary>Are there more members not included? 0 = no, 1 = yes.</summary>
    public int HasMoreMember { get; set; }
    /// <summary>Group sub-type.</summary>
    public int SubType { get; set; }
    /// <summary>Total number of members.</summary>
    public int TotalMember { get; set; }
    /// <summary>Maximum number of members allowed.</summary>
    public int MaxMember { get; set; }
    /// <summary>Group permission settings.</summary>
    public GroupSetting? Setting { get; set; }
    /// <summary>Group creation time (timestamp in milliseconds).</summary>
    public long CreatedTime { get; set; }
    /// <summary>Visibility: 0=visible, 1=hidden.</summary>
    public int Visibility { get; set; }
    /// <summary>Global ID for cross-platform identification.</summary>
    public string GlobalId { get; set; } = string.Empty;
    /// <summary>End-to-end encryption enabled? 0 = disabled, 1 = enabled.</summary>
    public int E2ee { get; set; }
    /// <summary>Extra information (media store flags, etc.). Equivalent to extraInfo in zca-js.</summary>
    public GroupExtraInfo? ExtraInfo { get; set; }
}