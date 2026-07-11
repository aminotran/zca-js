namespace ICU.Lib.ZaloClientWeb.Models.Types;

/// <summary>
/// Represents the type of conversation thread in Zalo.
/// </summary>
public enum ThreadType
{
    /// <summary>
    /// 1-to-1 user conversation (private chat between two users).
    /// Value: 0
    /// </summary>
    User = 0,

    /// <summary>
    /// Group conversation (multiple users).
    /// Value: 1
    /// </summary>
    Group = 1
}