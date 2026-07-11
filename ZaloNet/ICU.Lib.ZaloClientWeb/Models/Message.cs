using System.Collections.Generic;
using System.Text.Json;
using ICU.Lib.ZaloClientWeb.Models.Types;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Attachment content from a Zalo message (links, cards, etc.).
/// Used when MsgType is "chat.link" or similar rich content types.
/// Equivalent to TAttachmentContent in zca-js.
/// </summary>
public class AttachmentContent
{
    /// <summary>Link/channel title.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Link description text.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>URL of the link/content being shared.</summary>
    public string Href { get; set; } = string.Empty;
    /// <summary>Thumbnail image URL.</summary>
    public string Thumb { get; set; } = string.Empty;
    /// <summary>Number of child items (for multi-item attachments).</summary>
    public int ChildNumber { get; set; }
    /// <summary>Action type (e.g. "open_url", "open_oa").</summary>
    public string Action { get; set; } = string.Empty;
    /// <summary>JSON-encoded action parameters.</summary>
    public string Params { get; set; } = string.Empty;
    /// <summary>Type of attachment (e.g. "link", "biz").</summary>
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Quote information in a message (reply-to).
/// Present when a message is a reply to another message.
/// Equivalent to TQuote in zca-js.
/// </summary>
public class Quote
{
    /// <summary>User ID of the original message owner.</summary>
    public string OwnerId { get; set; } = string.Empty;
    /// <summary>Client-side message ID of the quoted message.</summary>
    public long CliMsgId { get; set; }
    /// <summary>Global (server-side) message ID of the quoted message.</summary>
    public long GlobalMsgId { get; set; }
    /// <summary>Type of the quoted message (1=text, 31=voice, 32=photo, etc.).</summary>
    public int CliMsgType { get; set; }
    /// <summary>Timestamp of the quoted message (milliseconds).</summary>
    public long Ts { get; set; }
    /// <summary>Text content of the quoted message.</summary>
    public string Msg { get; set; } = string.Empty;
    /// <summary>Attachment data of the quoted message (if any).</summary>
    public string Attach { get; set; } = string.Empty;
    /// <summary>Display name of the original sender.</summary>
    public string FromD { get; set; } = string.Empty;
    /// <summary>Time-to-live in seconds.</summary>
    public int Ttl { get; set; }
}

/// <summary>
/// Mention information in a group message.
/// Equivalent to TMention in zca-js.
/// </summary>
public class Mention
{
    /// <summary>User ID of the mentioned person.</summary>
    public string Uid { get; set; } = string.Empty;
    /// <summary>Start position of the mention in the text (character index).</summary>
    public int Pos { get; set; }
    /// <summary>Length of the mention text in characters.</summary>
    public int Len { get; set; }
    /// <summary>Type of mention: 0 = @mention, 1 = @everyone.</summary>
    public int Type { get; set; }
}

/// <summary>
/// Raw message data from Zalo API.
/// Equivalent to TMessage in zca-js.
/// </summary>
public class MessageData
{
    /// <summary>Unique action ID for this message (used for deduplication).</summary>
    public string ActionId { get; set; } = string.Empty;

    /// <summary>Server-assigned message ID (globally unique).</summary>
    public string MsgId { get; set; } = string.Empty;

    /// <summary>Client-generated message ID (for tracking sent messages).</summary>
    public string CliMsgId { get; set; } = string.Empty;

    /// <summary>Message type string: "webchat", "chat.photo", "chat.sticker", "chat.link", "share.file", etc.
    /// <para>See <see cref="Utils.ZaloUtils.GetClientMessageType"/> for mapping.</para>
    /// </summary>
    public string MsgType { get; set; } = string.Empty;

    /// <summary>Sender UID. "0" if the current logged-in user is the sender.</summary>
    public string UidFrom { get; set; } = string.Empty;

    /// <summary>Recipient UID (for 1-to-1) or Group ID (for group chat).</summary>
    public string IdTo { get; set; } = string.Empty;

    /// <summary>Display name of the sender.</summary>
    public string DName { get; set; } = string.Empty;

    /// <summary>Timestamp in milliseconds.</summary>
    public string Ts { get; set; } = string.Empty;

    /// <summary>Message status: 0 = sent, 1 = delivered, 2 = seen.</summary>
    public int Status { get; set; }

    /// <summary>Message content (text string, JSON for rich messages, or raw data).</summary>
    public JsonElement? Content { get; set; }

    /// <summary>Notification text shown in the conversation list.</summary>
    public string Notify { get; set; } = string.Empty;

    /// <summary>Time-to-live in seconds.</summary>
    public int Ttl { get; set; }

    /// <summary>User ID (same as uidFrom in most cases).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>User identification number.</summary>
    public string Uin { get; set; } = string.Empty;

    /// <summary>Real message ID (for message recall operations).</summary>
    public string RealMsgId { get; set; } = string.Empty;

    /// <summary>Quoted/reply message data (if this is a reply).</summary>
    public Quote? Quote { get; set; }

    /// <summary>Mention data (for group messages that @mention users).</summary>
    public List<Mention>? Mentions { get; set; }
}

/// <summary>
/// Represents a user-to-user (1-to-1) message.
/// Equivalent to UserMessage class in zca-js.
/// </summary>
public class UserMessageInfo
{
    /// <summary>Always <see cref="ThreadType.User"/>.</summary>
    public ThreadType Type => ThreadType.User;

    /// <summary>Raw message data from Zalo API.</summary>
    public MessageData Data { get; }

    /// <summary>Conversation thread ID (the other user's UID).</summary>
    public string ThreadId { get; }

    /// <summary>True if this message was sent by the current logged-in account.</summary>
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
    /// <summary>Always <see cref="ThreadType.Group"/>.</summary>
    public ThreadType Type => ThreadType.Group;

    /// <summary>Raw message data from Zalo API.</summary>
    public MessageData Data { get; }

    /// <summary>Group ID of the conversation.</summary>
    public string ThreadId { get; }

    /// <summary>True if this message was sent by the current logged-in account.</summary>
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