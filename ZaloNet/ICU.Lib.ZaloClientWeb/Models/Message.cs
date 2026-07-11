using System.Collections.Generic;
using System.Text.Json;
using ICU.Lib.ZaloClientWeb.Models.Types;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Attachment content from a Zalo message (links, cards, etc.)
/// Equivalent to TAttachmentContent in zca-js.
/// </summary>
public class AttachmentContent
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Href { get; set; } = string.Empty;
    public string Thumb { get; set; } = string.Empty;
    public int ChildNumber { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Params { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Quote information in a message (reply).
/// Equivalent to TQuote in zca-js.
/// </summary>
public class Quote
{
    public string OwnerId { get; set; } = string.Empty;
    public long CliMsgId { get; set; }
    public long GlobalMsgId { get; set; }
    public int CliMsgType { get; set; }
    public long Ts { get; set; }
    public string Msg { get; set; } = string.Empty;
    public string Attach { get; set; } = string.Empty;
    public string FromD { get; set; } = string.Empty;
    public int Ttl { get; set; }
}

/// <summary>
/// Mention information in a group message.
/// Equivalent to TMention in zca-js.
/// </summary>
public class Mention
{
    public string Uid { get; set; } = string.Empty;
    public int Pos { get; set; }
    public int Len { get; set; }
    public int Type { get; set; }
}

/// <summary>
/// Raw message data from Zalo API.
/// Equivalent to TMessage in zca-js.
/// </summary>
public class MessageData
{
    public string ActionId { get; set; } = string.Empty;
    public string MsgId { get; set; } = string.Empty;
    public string CliMsgId { get; set; } = string.Empty;
    public string MsgType { get; set; } = string.Empty;
    public string UidFrom { get; set; } = string.Empty;
    public string IdTo { get; set; } = string.Empty;
    public string DName { get; set; } = string.Empty;
    public string Ts { get; set; } = string.Empty;
    public int Status { get; set; }
    public JsonElement? Content { get; set; }
    public string Notify { get; set; } = string.Empty;
    public int Ttl { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Uin { get; set; } = string.Empty;
    public string RealMsgId { get; set; } = string.Empty;
    public Quote? Quote { get; set; }
    public List<Mention>? Mentions { get; set; }
}

/// <summary>
/// Represents a user-to-user message.
/// Equivalent to UserMessage class in zca-js.
/// </summary>
public class UserMessageInfo
{
    public ThreadType Type => ThreadType.User;
    public MessageData Data { get; }
    public string ThreadId { get; }
    public bool IsSelf { get; }

    public UserMessageInfo(string uid, MessageData data)
    {
        Data = data;
        ThreadId = data.UidFrom == "0" ? data.IdTo : data.UidFrom;
        IsSelf = data.UidFrom == "0";

        if (data.IdTo == "0") data.IdTo = uid;
        if (data.UidFrom == "0") data.UidFrom = uid;
        if (data.Quote != null)
            data.Quote.OwnerId = data.Quote.OwnerId.ToString();
    }
}

/// <summary>
/// Represents a group message.
/// Equivalent to GroupMessage class in zca-js.
/// </summary>
public class GroupMessageInfo
{
    public ThreadType Type => ThreadType.Group;
    public MessageData Data { get; }
    public string ThreadId { get; }
    public bool IsSelf { get; }

    public GroupMessageInfo(string uid, MessageData data)
    {
        Data = data;
        ThreadId = data.IdTo;
        IsSelf = data.UidFrom == "0";

        if (data.UidFrom == "0") data.UidFrom = uid;
        if (data.Quote != null)
            data.Quote.OwnerId = data.Quote.OwnerId.ToString();
    }
}