using System.Collections.Generic;
using System.Text.Json;
using ICU.Lib.ZaloClientWeb.Models.Types;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// A member involved in a group event (e.g. join/leave).
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
/// Base data for most group events.
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
}

/// <summary>
/// Group event wrapper from WebSocket real-time events.
/// Equivalent to GroupEvent discriminated union in zca-js.
/// </summary>
public class GroupEvent
{
    public GroupEventType Type { get; set; }
    public string Act { get; set; } = string.Empty;
    public JsonElement Data { get; set; }
    public string ThreadId { get; set; } = string.Empty;
    public bool IsSelf { get; set; }

    /// <summary>
    /// Initializes a group event from raw WebSocket data.
    /// Equivalent to initializeGroupEvent() in zca-js.
    /// </summary>
    public static GroupEvent Initialize(string uid, JsonElement data, GroupEventType type, string act)
    {
        var threadId = data.TryGetProperty("group_id", out var groupIdEl)
            ? groupIdEl.GetString() ?? ""
            : data.TryGetProperty("groupId", out var gIdEl)
                ? gIdEl.GetString() ?? ""
                : "";

        bool isSelf = false;

        if (type == GroupEventType.JoinRequest)
        {
            isSelf = false;
        }
        else if (type is GroupEventType.NewPinTopic or GroupEventType.UnpinTopic or GroupEventType.UpdatePinTopic or GroupEventType.ReorderPinTopic)
        {
            isSelf = data.TryGetProperty("actorId", out var actorId) && actorId.GetString() == uid;
        }
        else if (type is GroupEventType.UpdateBoard or GroupEventType.RemoveBoard)
        {
            isSelf = data.TryGetProperty("sourceId", out var sourceId) && sourceId.GetString() == uid;
        }
        else if (type is GroupEventType.AcceptRemind or GroupEventType.RejectRemind)
        {
            if (data.TryGetProperty("updateMembers", out var members) && members.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in members.EnumerateArray())
                {
                    if (m.GetString() == uid) { isSelf = true; break; }
                }
            }
        }
        else if (type == GroupEventType.RemindTopic)
        {
            isSelf = data.TryGetProperty("creatorId", out var creatorId) && creatorId.GetString() == uid;
        }
        else
        {
            if (data.TryGetProperty("updateMembers", out var baseMembers) && baseMembers.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in baseMembers.EnumerateArray())
                {
                    if (m.TryGetProperty("id", out var id) && id.GetString() == uid) { isSelf = true; break; }
                }
            }
            if (!isSelf && data.TryGetProperty("sourceId", out var srcId) && srcId.GetString() == uid)
                isSelf = true;
        }

        return new GroupEvent
        {
            Type = type,
            Act = act,
            Data = data.Clone(),
            ThreadId = threadId,
            IsSelf = isSelf
        };
    }
}