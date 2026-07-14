namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Scope of auto-reply rules.
/// Equivalent to AutoReplyScope in zca-js.
/// </summary>
public enum AutoReplyScope
{
    /// <summary>Apply to everyone.</summary>
    Everyone = 0,
    /// <summary>Apply to strangers (non-friends).</summary>
    Stranger = 1,
    /// <summary>Apply to specific friends only.</summary>
    SpecificFriends = 2,
    /// <summary>Apply to all friends except specific ones.</summary>
    FriendsExcept = 3
}

/// <summary>
/// Represents an auto-reply rule item.
/// Equivalent to AutoReplyItem in zca-js.
/// </summary>
public class AutoReplyItem
{
    /// <summary>Auto-reply rule ID.</summary>
    public int Id { get; set; }

    /// <summary>Weight/priority of this rule (higher = more priority).</summary>
    public int Weight { get; set; }

    /// <summary>Whether this rule is enabled.</summary>
    public bool Enable { get; set; }

    /// <summary>Last modification timestamp (unix ms).</summary>
    public long ModifiedTime { get; set; }

    /// <summary>Start time when this rule becomes active (unix ms).</summary>
    public long StartTime { get; set; }

    /// <summary>End time when this rule expires (unix ms).</summary>
    public long EndTime { get; set; }

    /// <summary>The auto-reply content/text.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Scope of this auto-reply rule.</summary>
    public AutoReplyScope Scope { get; set; }

    /// <summary>List of specific user IDs this rule applies to (when scope is SpecificFriends). Can be null.</summary>
    public string[]? Uids { get; set; }

    /// <summary>Owner's user ID.</summary>
    public long OwnerId { get; set; }

    /// <summary>Recurrence pattern (e.g. ["mon", "tue", ...]).</summary>
    public string[] Recurrence { get; set; } = System.Array.Empty<string>();

    /// <summary>Creation timestamp (unix ms).</summary>
    public long CreatedTime { get; set; }
}