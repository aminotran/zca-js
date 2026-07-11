using ICU.Lib.ZaloClientWeb.Models.Types;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Typing event data from Zalo.
/// Equivalent to TTyping in zca-js.
/// </summary>
public class TypingData
{
    public string Uid { get; set; } = string.Empty;
    public string Ts { get; set; } = string.Empty;
    public int IsPC { get; set; }
}

/// <summary>
/// Group typing event data.
/// Equivalent to TGroupTyping in zca-js.
/// </summary>
public class GroupTypingData
{
    public string Uid { get; set; } = string.Empty;
    public string Ts { get; set; } = string.Empty;
    public int IsPC { get; set; }
    public string Gid { get; set; } = string.Empty;
}

/// <summary>
/// Represents a user typing event.
/// Equivalent to UserTyping class in zca-js.
/// </summary>
public class UserTypingEvent
{
    public ThreadType Type => ThreadType.User;
    public TypingData Data { get; }
    public string ThreadId { get; }
    public bool IsSelf => false;

    public UserTypingEvent(TypingData data)
    {
        Data = data;
        ThreadId = data.Uid;
    }
}

/// <summary>
/// Represents a group typing event.
/// Equivalent to GroupTyping class in zca-js.
/// </summary>
public class GroupTypingEvent
{
    public ThreadType Type => ThreadType.Group;
    public GroupTypingData Data { get; }
    public string ThreadId { get; }
    public bool IsSelf => false;

    public GroupTypingEvent(GroupTypingData data)
    {
        Data = data;
        ThreadId = data.Gid;
    }
}