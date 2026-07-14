using System.Collections.Generic;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Types of board items in a group.
/// Equivalent to BoardType in zca-js.
/// </summary>
public enum BoardType
{
    /// <summary>Pinned note. Value: 1</summary>
    Note = 1,
    /// <summary>Pinned message. Value: 2</summary>
    PinnedMessage = 2,
    /// <summary>Poll. Value: 3</summary>
    Poll = 3,
}

/// <summary>
/// Poll options with vote counts and voters.
/// Equivalent to PollOptions in zca-js.
/// </summary>
public class PollOptions
{
    /// <summary>Option display text.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Number of votes for this option.</summary>
    public int Votes { get; set; }

    /// <summary>Whether the current user voted for this option.</summary>
    public bool Voted { get; set; }

    /// <summary>List of voter user IDs.</summary>
    public List<string> Voters { get; set; } = new();

    /// <summary>Option ID.</summary>
    public int OptionId { get; set; }
}

/// <summary>
/// Poll detail information.
/// Equivalent to PollDetail in zca-js.
/// </summary>
public class PollDetail
{
    /// <summary>Creator's user ID.</summary>
    public string Creator { get; set; } = string.Empty;

    /// <summary>Poll question/title.</summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>List of poll options.</summary>
    public List<PollOptions> Options { get; set; } = new();

    /// <summary>Whether the current user has voted.</summary>
    public bool Joined { get; set; }

    /// <summary>Whether the poll is closed.</summary>
    public bool Closed { get; set; }

    /// <summary>Poll ID.</summary>
    public int PollId { get; set; }

    /// <summary>Allow multiple choices? 0=no, 1=yes.</summary>
    public bool AllowMultiChoices { get; set; }

    /// <summary>Allow adding new options? 0=no, 1=yes.</summary>
    public bool AllowAddNewOption { get; set; }

    /// <summary>Is anonymous voting? 0=no, 1=yes.</summary>
    public bool IsAnonymous { get; set; }

    /// <summary>Poll type.</summary>
    public int PollType { get; set; }

    /// <summary>Creation timestamp (unix ms).</summary>
    public long CreatedTime { get; set; }

    /// <summary>Last update timestamp (unix ms).</summary>
    public long UpdatedTime { get; set; }

    /// <summary>Expiration timestamp (unix ms). 0 = no expiration.</summary>
    public long ExpiredTime { get; set; }

    /// <summary>Hide vote preview before voting? 0=no, 1=yes.</summary>
    public bool IsHideVotePreview { get; set; }

    /// <summary>Total number of votes.</summary>
    public int NumVote { get; set; }
}

/// <summary>
/// Note detail in a group board.
/// Equivalent to NoteDetail in zca-js.
/// </summary>
public class NoteDetail
{
    /// <summary>Note ID.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Note type.</summary>
    public int Type { get; set; }

    /// <summary>Theme color as negative number.</summary>
    public int Color { get; set; }

    /// <summary>Emoji icon.</summary>
    public string Emoji { get; set; } = string.Empty;

    /// <summary>Start timestamp (unix ms).</summary>
    public long StartTime { get; set; }

    /// <summary>Duration in seconds. 0 = permanent.</summary>
    public int Duration { get; set; }

    /// <summary>Note parameters/body.</summary>
    public NoteParams? Params { get; set; }

    /// <summary>Creator's user ID.</summary>
    public string CreatorId { get; set; } = string.Empty;

    /// <summary>Last editor's user ID.</summary>
    public string EditorId { get; set; } = string.Empty;

    /// <summary>Creation timestamp (unix ms).</summary>
    public long CreateTime { get; set; }

    /// <summary>Last edit timestamp (unix ms).</summary>
    public long EditTime { get; set; }

    /// <summary>Repeat interval in seconds.</summary>
    public int Repeat { get; set; }
}

/// <summary>
/// Parameters for a note.
/// Equivalent to NoteDetail.params in zca-js.
/// </summary>
public class NoteParams
{
    /// <summary>Note title/content.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional extra data.</summary>
    public string? Extra { get; set; }
}

/// <summary>
/// Pinned message detail in a group board.
/// Equivalent to PinnedMessageDetail in zca-js.
/// </summary>
public class PinnedMessageDetail
{
    /// <summary>Pinned message ID.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Type.</summary>
    public int Type { get; set; }

    /// <summary>Theme color as negative number.</summary>
    public int Color { get; set; }

    /// <summary>Emoji icon.</summary>
    public string Emoji { get; set; } = string.Empty;

    /// <summary>Start timestamp (unix ms).</summary>
    public long StartTime { get; set; }

    /// <summary>Duration in seconds.</summary>
    public int Duration { get; set; }

    /// <summary>Parameters (varies by type).</summary>
    public System.Text.Json.JsonElement Params { get; set; }

    /// <summary>Creator's user ID.</summary>
    public string CreatorId { get; set; } = string.Empty;

    /// <summary>Last editor's user ID.</summary>
    public string EditorId { get; set; } = string.Empty;

    /// <summary>Creation timestamp (unix ms).</summary>
    public long CreateTime { get; set; }

    /// <summary>Last edit timestamp (unix ms).</summary>
    public long EditTime { get; set; }

    /// <summary>Repeat interval in seconds.</summary>
    public int Repeat { get; set; }
}