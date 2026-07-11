namespace ICU.Lib.ZaloClientWeb.Models.Types;

/// <summary>
/// WebSocket close reason codes used by Zalo.
/// </summary>
public enum CloseReason
{
    ManualClosure = 1000,
    AbnormalClosure = 1006,
    DuplicateConnection = 3000,
    KickConnection = 3003
}