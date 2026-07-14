using System.Text.Json;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Repeat mode for reminders.
/// Equivalent to ReminderRepeatMode in zca-js.
/// </summary>
public enum ReminderRepeatMode
{
    /// <summary>No repeat. Value: 0</summary>
    None = 0,
    /// <summary>Repeat daily. Value: 1</summary>
    Daily = 1,
    /// <summary>Repeat weekly. Value: 2</summary>
    Weekly = 2,
    /// <summary>Repeat monthly. Value: 3</summary>
    Monthly = 3,
}

/// <summary>
/// User-level reminder information.
/// Equivalent to ReminderUser in zca-js.
/// </summary>
public class ReminderUser
{
    /// <summary>Creator's user ID.</summary>
    public string CreatorUid { get; set; } = string.Empty;

    /// <summary>Target user ID this reminder is for.</summary>
    public string ToUid { get; set; } = string.Empty;

    /// <summary>Emoji icon for the reminder.</summary>
    public string Emoji { get; set; } = string.Empty;

    /// <summary>Theme color as negative number.</summary>
    public int Color { get; set; }

    /// <summary>Reminder ID.</summary>
    public string ReminderId { get; set; } = string.Empty;

    /// <summary>Creation timestamp (unix ms).</summary>
    public long CreateTime { get; set; }

    /// <summary>Repeat mode.</summary>
    public ReminderRepeatMode Repeat { get; set; }

    /// <summary>Start timestamp (unix ms).</summary>
    public long StartTime { get; set; }

    /// <summary>Last edit timestamp (unix ms).</summary>
    public long EditTime { get; set; }

    /// <summary>End/expiration timestamp (unix ms).</summary>
    public long EndTime { get; set; }

    /// <summary>Reminder parameters (title, etc.).</summary>
    public ReminderParams? Params { get; set; }

    /// <summary>Type.</summary>
    public int Type { get; set; }
}

/// <summary>
/// Reminder parameters (title, etc.).
/// </summary>
public class ReminderParams
{
    /// <summary>Reminder title/text.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Whether the title has been set manually.</summary>
    public bool SetTitle { get; set; }
}

/// <summary>
/// Response members for group reminders (accepted/rejected counts).
/// </summary>
public class ReminderResponseMembers
{
    /// <summary>Number of members who rejected.</summary>
    public int RejectMember { get; set; }

    /// <summary>Current user's response: 0=no response, 1=accepted, 2=rejected.</summary>
    public int MyResp { get; set; }

    /// <summary>Number of members who accepted.</summary>
    public int AcceptMember { get; set; }
}

/// <summary>
/// Repeat information for reminders.
/// </summary>
public class ReminderRepeatInfo
{
    /// <summary>List of repeat timestamps.</summary>
    public JsonElement? ListTs { get; set; }
}

/// <summary>
/// Group-level reminder information.
/// Equivalent to ReminderGroup in zca-js.
/// </summary>
public class ReminderGroup
{
    /// <summary>Last editor's user ID.</summary>
    public string EditorId { get; set; } = string.Empty;

    /// <summary>Emoji icon for the reminder.</summary>
    public string Emoji { get; set; } = string.Empty;

    /// <summary>Theme color as negative number.</summary>
    public int Color { get; set; }

    /// <summary>Group ID this reminder belongs to.</summary>
    public string GroupId { get; set; } = string.Empty;

    /// <summary>Creator's user ID.</summary>
    public string CreatorId { get; set; } = string.Empty;

    /// <summary>Last edit timestamp (unix ms).</summary>
    public long EditTime { get; set; }

    /// <summary>Event type.</summary>
    public int EventType { get; set; }

    /// <summary>Response member counts.</summary>
    public ReminderResponseMembers? ResponseMem { get; set; }

    /// <summary>Reminder parameters.</summary>
    public ReminderParams? Params { get; set; }

    /// <summary>Type.</summary>
    public int Type { get; set; }

    /// <summary>Duration in seconds.</summary>
    public int Duration { get; set; }

    /// <summary>Repeat info (can be null).</summary>
    public ReminderRepeatInfo? RepeatInfo { get; set; }

    /// <summary>Repeat data (array of unknown objects).</summary>
    public JsonElement RepeatData { get; set; }

    /// <summary>Creation timestamp (unix ms).</summary>
    public long CreateTime { get; set; }

    /// <summary>Repeat mode.</summary>
    public ReminderRepeatMode Repeat { get; set; }

    /// <summary>Start timestamp (unix ms).</summary>
    public long StartTime { get; set; }

    /// <summary>Reminder ID.</summary>
    public string Id { get; set; } = string.Empty;
}