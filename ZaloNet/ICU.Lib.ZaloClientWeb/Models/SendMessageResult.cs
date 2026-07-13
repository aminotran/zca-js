namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Result of a single sent message (msgId).
/// Equivalent to SendMessageResult in zca-js.
/// </summary>
public class SendMessageResult
{
    /// <summary>Message ID returned by Zalo server</summary>
    public long MsgId { get; set; }
}

/// <summary>
/// Combined response from sending a message with possible attachments.
/// Equivalent to SendMessageResponse in zca-js.
/// </summary>
public class SendMessageResponse
{
    /// <summary>Text message result (null if no text was sent)</summary>
    public SendMessageResult? Message { get; set; }

    /// <summary>Attachment send results</summary>
    public SendMessageResult[] Attachment { get; set; } = Array.Empty<SendMessageResult>();
}
