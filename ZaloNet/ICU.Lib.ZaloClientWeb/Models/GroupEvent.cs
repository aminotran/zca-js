using System.Collections.Generic;
using System.Text.Json;
using ICU.Lib.ZaloClientWeb.Models.Types;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// A member update within a group event (e.g. member joined/left/kicked).
/// Equivalent to GroupEventUpdateMember in zca-js.
/// </summary>
public class GroupEventMember
{
    public string Id { get; set; } = string.Empty;
    public string DName { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;
    public int Type { get; set; }
}

/// <summary>
/// Data for base group events (member join/leave, settings update, role changes, etc.).
/// Used by most GroupEventType values except JoinRequest, pin, board, remind.
/// Equivalent to TGroupEventBase in zca-js.
/// </summary>
public class GroupEventBaseData
{
    public int SubType { get; set; }
    public string GroupId { get; set; } = string.Empty;
    public string CreatorId { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public List<GroupEventMember> UpdateMembers { get; set; } = new();
    public JsonElement? GroupSetting { get; set; }
    public JsonElement? GroupTopic { get; set; }
    public JsonElement? Info { get; set; }
    public JsonElement? ExtraData { get; set; }
    public string Time { get; set; } = string.Empty;
    public string? Avt { get; set; }
    public string? FullAvt { get; set; }
    public int IsAdd { get; set; }
    public int HideGroupInfo { get; set; }
    public string Version { get; set; } = string.Empty;
    public int GroupType { get; set; }

    public static GroupEventBaseData FromJson(JsonElement data)
    {
        var result = new GroupEventBaseData
        {
            SubType = data.TryGetProperty("subType", out var sub) ? sub.GetInt32() : 0,
            GroupId = data.TryGetProperty("groupId", out var gid) ? gid.GetString() ?? "" : "",
            CreatorId = data.TryGetProperty("creatorId", out var cid) ? cid.GetString() ?? "" : "",
            GroupName = data.TryGetProperty("groupName", out var gn) ? gn.GetString() ?? "" : "",
            SourceId = data.TryGetProperty("sourceId", out var si) ? si.GetString() ?? "" : "",
            Time = data.TryGetProperty("time", out var t) ? t.GetString() ?? "" : "",
            Avt = data.TryGetProperty("avt", out var avt) ? avt.GetString() : null,
            FullAvt = data.TryGetProperty("fullAvt", out var favt) ? favt.GetString() : null,
            IsAdd = data.TryGetProperty("isAdd", out var ia) ? ia.GetInt32() : 0,
            HideGroupInfo = data.TryGetProperty("hideGroupInfo", out var hgi) ? hgi.GetInt32() : 0,
            Version = data.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "",
            GroupType = data.TryGetProperty("groupType", out var gt) ? gt.GetInt32() : 0,
            GroupSetting = data.TryGetProperty("groupSetting", out var gs) ? gs.Clone() : null,
            GroupTopic = data.TryGetProperty("groupTopic", out var gtop) ? gtop.Clone() : null,
            Info = data.TryGetProperty("info", out var inf) ? inf.Clone() : null,
            ExtraData = data.TryGetProperty("extraData", out var ex) ? ex.Clone() : null,
        };

        if (data.TryGetProperty("updateMembers", out var members) && members.ValueKind == JsonValueKind.Array)
        {
            foreach (var m in members.EnumerateArray())
            {
                result.UpdateMembers.Add(new GroupEventMember
                {
                    Id = m.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                    DName = m.TryGetProperty("dName", out var dn) ? dn.GetString() ?? "" : "",
                    Avatar = m.TryGetProperty("avatar", out var av) ? av.GetString() ?? "" : "",
                    Type = m.TryGetProperty("type", out var tp) ? tp.GetInt32() : 0
                });
            }
        }

        return result;
    }
}

/// <summary>
/// Data for join request group events.
/// Used by GroupEventType: JoinRequest.
/// </summary>
public class GroupEventJoinRequestData
{
    public string[]? Uids { get; set; }
    public int TotalPending { get; set; }
    public string GroupId { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;

    public static GroupEventJoinRequestData FromJson(JsonElement data)
    {
        var result = new GroupEventJoinRequestData
        {
            GroupId = data.TryGetProperty("groupId", out var gid) ? gid.GetString() ?? "" : "",
            Time = data.TryGetProperty("time", out var t) ? t.GetString() ?? "" : "",
        };
        if (data.TryGetProperty("totalPending", out var tp))
            result.TotalPending = tp.GetInt32();
        if (data.TryGetProperty("uids", out var uids) && uids.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var u in uids.EnumerateArray())
                list.Add(u.GetString() ?? "");
            result.Uids = list.ToArray();
        }
        return result;
    }
}

/// <summary>
/// Data for pin topic group events (new, update, unpin).
/// Used by GroupEventType: NewPinTopic, UpdatePinTopic, UnpinTopic.
/// </summary>
public class GroupEventPinTopicData
{
    public string GroupId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public JsonElement? Topic { get; set; }

    public static GroupEventPinTopicData FromJson(JsonElement data)
    {
        return new GroupEventPinTopicData
        {
            GroupId = data.TryGetProperty("groupId", out var gid) ? gid.GetString() ?? "" : "",
            ActorId = data.TryGetProperty("actorId", out var aid) ? aid.GetString() ?? "" : "",
            Topic = data.TryGetProperty("topic", out var top) ? top.Clone() : null
        };
    }
}

/// <summary>
/// Data for reorder pin topic group events.
/// Used by GroupEventType: ReorderPinTopic.
/// </summary>
public class GroupEventReorderPinData
{
    public string GroupId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;

    public static GroupEventReorderPinData FromJson(JsonElement data)
    {
        return new GroupEventReorderPinData
        {
            GroupId = data.TryGetProperty("groupId", out var gid) ? gid.GetString() ?? "" : "",
            ActorId = data.TryGetProperty("actorId", out var aid) ? aid.GetString() ?? "" : ""
        };
    }
}

/// <summary>
/// Data for board update/remove group events.
/// Used by GroupEventType: UpdateBoard, RemoveBoard.
/// </summary>
public class GroupEventBoardData
{
    public string GroupId { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public JsonElement? Topic { get; set; }

    public static GroupEventBoardData FromJson(JsonElement data)
    {
        return new GroupEventBoardData
        {
            GroupId = data.TryGetProperty("groupId", out var gid) ? gid.GetString() ?? "" : "",
            SourceId = data.TryGetProperty("sourceId", out var sid) ? sid.GetString() ?? "" : "",
            GroupName = data.TryGetProperty("groupName", out var gn) ? gn.GetString() ?? "" : "",
            Topic = data.TryGetProperty("groupTopic", out var top) ? top.Clone() : null
        };
    }
}

/// <summary>
/// Data for remind response group events (accept/reject).
/// Used by GroupEventType: AcceptRemind, RejectRemind.
/// </summary>
public class GroupEventRemindRespondData
{
    public string TopicId { get; set; } = string.Empty;
    public string[]? UpdateMembers { get; set; }
    public string GroupId { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;

    public static GroupEventRemindRespondData FromJson(JsonElement data)
    {
        var result = new GroupEventRemindRespondData
        {
            TopicId = data.TryGetProperty("topicId", out var tid) ? tid.GetString() ?? "" : "",
            GroupId = data.TryGetProperty("groupId", out var gid) ? gid.GetString() ?? "" : "",
            Time = data.TryGetProperty("time", out var t) ? t.GetString() ?? "" : ""
        };
        if (data.TryGetProperty("updateMembers", out var members) && members.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var m in members.EnumerateArray())
                list.Add(m.GetString() ?? "");
            result.UpdateMembers = list.ToArray();
        }
        return result;
    }
}

/// <summary>
/// Data for remind topic group events (a reminder was triggered).
/// Used by GroupEventType: RemindTopic.
/// </summary>
public class GroupEventRemindTopicData
{
    public string GroupId { get; set; } = string.Empty;
    public string CreatorId { get; set; } = string.Empty;

    public static GroupEventRemindTopicData FromJson(JsonElement data)
    {
        return new GroupEventRemindTopicData
        {
            GroupId = data.TryGetProperty("group_id", out var gid) ? gid.GetString() ?? "" : "",
            CreatorId = data.TryGetProperty("creatorId", out var cid) ? cid.GetString() ?? "" : ""
        };
    }
}

/// <summary>
/// Group event wrapper from WebSocket real-time events.
/// <c>Data</c> is strongly-typed based on <c>Type</c>.
/// Equivalent to GroupEvent discriminated union in zca-js.
/// </summary>
public class GroupEvent
{
    /// <summary>Type of group event.</summary>
    public GroupEventType Type { get; set; }

    /// <summary>The raw action string from Zalo (e.g. "join", "leave", "update_setting").</summary>
    public string Act { get; set; } = string.Empty;

    /// <summary>Strongly-typed data object. Cast to the appropriate type based on <see cref="Type"/>.</summary>
    public object? Data { get; set; }

    /// <summary>Group ID where the event occurred.</summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>True if the event was triggered by the current logged-in user.</summary>
    public bool IsSelf { get; set; }

    /// <summary>
    /// Initializes a group event from raw WebSocket data.
    /// Returns the event with strongly-typed Data based on the event type.
    /// </summary>
    public static GroupEvent Initialize(string uid, JsonElement data, GroupEventType type, string act)
    {
        var threadId = data.TryGetProperty("group_id", out var groupIdEl)
            ? groupIdEl.GetString() ?? ""
            : data.TryGetProperty("groupId", out var gIdEl)
                ? gIdEl.GetString() ?? ""
                : "";

        object? eventData;
        bool isSelf;

        if (type == GroupEventType.JoinRequest)
        {
            eventData = GroupEventJoinRequestData.FromJson(data);
            isSelf = false;
        }
        else if (type is GroupEventType.NewPinTopic or GroupEventType.UnpinTopic or GroupEventType.UpdatePinTopic)
        {
            eventData = GroupEventPinTopicData.FromJson(data);
            isSelf = ((GroupEventPinTopicData)eventData).ActorId == uid;
        }
        else if (type == GroupEventType.ReorderPinTopic)
        {
            eventData = GroupEventReorderPinData.FromJson(data);
            isSelf = ((GroupEventReorderPinData)eventData).ActorId == uid;
        }
        else if (type is GroupEventType.UpdateBoard or GroupEventType.RemoveBoard)
        {
            eventData = GroupEventBoardData.FromJson(data);
            isSelf = ((GroupEventBoardData)eventData).SourceId == uid;
        }
        else if (type is GroupEventType.AcceptRemind or GroupEventType.RejectRemind)
        {
            eventData = GroupEventRemindRespondData.FromJson(data);
            var remindData = (GroupEventRemindRespondData)eventData;
            isSelf = remindData.UpdateMembers != null && System.Array.IndexOf(remindData.UpdateMembers, uid) >= 0;
        }
        else if (type == GroupEventType.RemindTopic)
        {
            eventData = GroupEventRemindTopicData.FromJson(data);
            isSelf = ((GroupEventRemindTopicData)eventData).CreatorId == uid;
        }
        else
        {
            eventData = GroupEventBaseData.FromJson(data);
            var baseData = (GroupEventBaseData)eventData;
            isSelf = baseData.UpdateMembers.Exists(m => m.Id == uid) || baseData.SourceId == uid;
        }

        return new GroupEvent
        {
            Type = type,
            Act = act,
            Data = eventData,
            ThreadId = threadId,
            IsSelf = isSelf
        };
    }
}