using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Text style for messages (Bold, Italic, Underline, etc.)
/// Equivalent to TextStyle enum in zca-js.
/// </summary>
public enum TextStyle
{
    Bold = 0,
    Italic = 1,
    Underline = 2,
    StrikeThrough = 3,
    Red = 4,
    Orange = 5,
    Yellow = 6,
    Green = 7,
    Small = 8,
    Big = 9,
    UnorderedList = 10,
    OrderedList = 11,
    Indent = 12,
}

/// <summary>
/// A style applied to a range of text in a message.
/// Equivalent to Style type in zca-js.
/// </summary>
public class Style
{
    /// <summary>Start position of the style (0-based)</summary>
    [JsonPropertyName("start")]
    public int Start { get; set; }

    /// <summary>Length of text the style applies to</summary>
    [JsonPropertyName("len")]
    public int Len { get; set; }

    /// <summary>The style type</summary>
    [JsonPropertyName("st")]
    public string St { get; set; } = "";

    /// <summary>Number of spaces for indent (only when st is Indent)</summary>
    [JsonPropertyName("indentSize")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? IndentSize { get; set; }
}

/// <summary>
/// Urgency level for message.
/// </summary>
public enum Urgency
{
    Default = 0,
    Important = 1,
    Urgent = 2,
}

/// <summary>
/// A mention (@user) in a message to send.
/// Equivalent to Mention type in zca-js.
/// </summary>
public class MessageMention
{
    /// <summary>Start position of the mention in the message text</summary>
    [JsonPropertyName("pos")]
    public int Pos { get; set; }

    /// <summary>Zalo user ID of the mentioned user. Use "-1" for @all.</summary>
    [JsonPropertyName("uid")]
    public string Uid { get; set; } = "";

    /// <summary>Length of the mention text (e.g. 11 for "@John Doe")</summary>
    [JsonPropertyName("len")]
    public int Len { get; set; }

    /// <summary>0 = user, 1 = all (set automatically when uid is "-1")</summary>
    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Type { get; set; }
}

/// <summary>
/// Quoted message reference for replying to a message.
/// Equivalent to SendMessageQuote type in zca-js.
/// </summary>
public class SendMessageQuote
{
    /// <summary>Raw content of the quoted message (string for text, object for other types)</summary>
    [JsonPropertyName("content")]
    public object? Content { get; set; }

    /// <summary>Message type string (e.g. "chat.text", "chat.photo", "chat.video")</summary>
    [JsonPropertyName("msgType")]
    public string MsgType { get; set; } = "";

    /// <summary>Property extension of the quoted message</summary>
    [JsonPropertyName("propertyExt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? PropertyExt { get; set; }

    /// <summary>UID of the quoted message sender</summary>
    [JsonPropertyName("uidFrom")]
    public string UidFrom { get; set; } = "";

    /// <summary>Global message ID</summary>
    [JsonPropertyName("msgId")]
    public string MsgId { get; set; } = "";

    /// <summary>Client message ID</summary>
    [JsonPropertyName("cliMsgId")]
    public string CliMsgId { get; set; } = "";

    /// <summary>Timestamp of the quoted message</summary>
    [JsonPropertyName("ts")]
    public long Ts { get; set; }

    /// <summary>Time-to-live of the quoted message</summary>
    [JsonPropertyName("ttl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Ttl { get; set; }

    /// <summary>Attachments if any (for quote with attachments)</summary>
    [JsonIgnore]
    public object? AttachmentData { get; set; }
}

/// <summary>
/// Full content of a message to send, including optional mentions, styles, quote, etc.
/// Equivalent to MessageContent type in zca-js.
/// </summary>
public class MessageContent
{
    /// <summary>Text content of the message</summary>
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = "";

    /// <summary>Text styles (optional)</summary>
    [JsonIgnore]
    public List<Style>? Styles { get; set; }

    /// <summary>Urgency of the message (optional)</summary>
    [JsonIgnore]
    public Urgency? Urgency { get; set; }

    /// <summary>Quoted message (optional)</summary>
    [JsonIgnore]
    public SendMessageQuote? Quote { get; set; }

    /// <summary>Mentions in the message (optional)</summary>
    [JsonIgnore]
    public List<MessageMention>? Mentions { get; set; }

    /// <summary>Time to live in milliseconds (optional, 0 = forever)</summary>
    [JsonIgnore]
    public int? Ttl { get; set; }

    /// <summary>Create from plain string</summary>
    public static implicit operator MessageContent(string text) => new() { Msg = text };
}