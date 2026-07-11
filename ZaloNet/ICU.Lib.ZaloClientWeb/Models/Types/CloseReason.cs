namespace ICU.Lib.ZaloClientWeb.Models.Types;

/// <summary>
/// WebSocket close reason codes used by Zalo server.
/// <para>Reference: https://developer.mozilla.org/en-US/docs/Web/API/CloseEvent </para>
/// </summary>
public enum CloseReason
{
    /// <summary>
    /// Normal closure — the connection was intentionally closed by the client.
    /// Used when calling Stop() on the listener.
    /// <para>Value: 1000</para>
    /// </summary>
    ManualClosure = 1000,

    /// <summary>
    /// Abnormal closure — the connection was lost unexpectedly (network issue, timeout, etc.).
    /// The WebSocket was closed without sending a Close frame.
    /// <para>Value: 1006</para>
    /// </summary>
    AbnormalClosure = 1006,

    /// <summary>
    /// Duplicate connection — another client instance with the same session has opened a new connection.
    /// Zalo only allows one WebSocket connection per session.
    /// <para>Value: 3000</para>
    /// </summary>
    DuplicateConnection = 3000,

    /// <summary>
    /// Kicked by server — the account was logged in from another device/location.
    /// This typically means the session was invalidated.
    /// <para>Value: 3003</para>
    /// </summary>
    KickConnection = 3003
}