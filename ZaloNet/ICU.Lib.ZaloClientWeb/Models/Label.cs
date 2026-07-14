using System.Collections.Generic;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Represents a conversation label/tag.
/// Equivalent to LabelData in zca-js.
/// </summary>
public class LabelData
{
    /// <summary>Label ID.</summary>
    public int Id { get; set; }

    /// <summary>Label display text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Label text key for localization.</summary>
    public string TextKey { get; set; } = string.Empty;

    /// <summary>List of conversation IDs this label is applied to.</summary>
    public List<string> Conversations { get; set; } = new();

    /// <summary>Label color (hex string, e.g. "#FF0000").</summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>Offset/order of this label in the list.</summary>
    public int Offset { get; set; }

    /// <summary>Emoji icon for this label.</summary>
    public string Emoji { get; set; } = string.Empty;

    /// <summary>Creation timestamp (unix ms).</summary>
    public long CreateTime { get; set; }
}