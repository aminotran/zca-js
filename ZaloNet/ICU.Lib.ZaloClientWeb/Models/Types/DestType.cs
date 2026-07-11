namespace ICU.Lib.ZaloClientWeb.Models.Types;

/// <summary>
/// Zalo destination types — indicates the type of recipient for sending messages/events.
/// <para>Reference: Zalo internal API parameter for "dst" field in request payloads.</para>
/// </summary>
public enum DestType
{
    /// <summary>
    /// Destination is a group chat.
    /// <para>Value: 1</para>
    /// </summary>
    Group = 1,

    /// <summary>
    /// Destination is a single user (1-to-1 chat).
    /// <para>Value: 3</para>
    /// </summary>
    User = 3,

    /// <summary>
    /// Destination is an Official Account (OA) page.
    /// <para>Value: 5</para>
    /// </summary>
    Page = 5
}