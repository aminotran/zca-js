using System.Collections.Generic;
using System.Text.Json;
using ICU.Lib.ZaloClientWeb.Models.Types;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Group settings like permissions.
/// Equivalent to GroupSetting in zca-js.
/// </summary>
public class GroupSetting
{
    public int BlockName { get; set; }
    public int SignAdminMsg { get; set; }
    public int AddMemberOnly { get; set; }
    public int SetTopicOnly { get; set; }
    public int EnableMsgHistory { get; set; }
    public int JoinAppr { get; set; }
    public int LockCreatePost { get; set; }
    public int LockCreatePoll { get; set; }
    public int LockSendMsg { get; set; }
    public int LockViewMember { get; set; }
    public int BannFeature { get; set; }
    public int DirtyMedia { get; set; }
    public int BanDuration { get; set; }
}

/// <summary>
/// Types of group topics.
/// </summary>
public enum GroupTopicType
{
    Note = 0,
    Message = 2,
    Poll = 3
}

/// <summary>
/// Group topic (pinned message/note/poll).
/// Equivalent to GroupTopic in zca-js.
/// </summary>
public class GroupTopic
{
    public GroupTopicType Type { get; set; }
    public int Color { get; set; }
    public string Emoji { get; set; } = string.Empty;
    public long StartTime { get; set; }
    public int Duration { get; set; }
    public JsonElement Params { get; set; }
    public string Id { get; set; } = string.Empty;
    public string CreatorId { get; set; } = string.Empty;
    public long CreateTime { get; set; }
    public string EditorId { get; set; } = string.Empty;
    public long EditTime { get; set; }
    public int Repeat { get; set; }
    public int Action { get; set; }
}

/// <summary>
/// Group types.
/// </summary>
public enum GroupType
{
    Group = 1,
    Community = 2
}

/// <summary>
/// A current member of a group.
/// Equivalent to GroupCurrentMem in zca-js.
/// </summary>
public class GroupCurrentMember
{
    public string Id { get; set; } = string.Empty;
    public string DName { get; set; } = string.Empty;
    public string ZaloName { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;
    public int AccountStatus { get; set; }
    public int Type { get; set; }
}

/// <summary>
/// Full group information.
/// Equivalent to GroupInfo type in zca-js.
/// </summary>
public class GroupFullInfo
{
    public string GroupId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Desc { get; set; } = string.Empty;
    public GroupType Type { get; set; }
    public long CreatorId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Avt { get; set; } = string.Empty;
    public string FullAvt { get; set; } = string.Empty;
    public List<string> MemberIds { get; set; } = new();
    public List<string> AdminIds { get; set; } = new();
    public List<GroupCurrentMember> CurrentMems { get; set; } = new();
    public int HasMoreMember { get; set; }
    public int SubType { get; set; }
    public int TotalMember { get; set; }
    public int MaxMember { get; set; }
    public GroupSetting? Setting { get; set; }
    public long CreatedTime { get; set; }
    public int Visibility { get; set; }
    public string GlobalId { get; set; } = string.Empty;
    public int E2ee { get; set; }
}