using System.Collections.Generic;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Represents a quick message (canned response) item.
/// Equivalent to QuickMessage in zca-js.
/// </summary>
public class QuickMessage
{
    /// <summary>Quick message ID.</summary>
    public int Id { get; set; }

    /// <summary>Trigger keyword.</summary>
    public string Keyword { get; set; } = string.Empty;

    /// <summary>Type.</summary>
    public int Type { get; set; }

    /// <summary>Creation timestamp (unix ms).</summary>
    public long CreatedTime { get; set; }

    /// <summary>Last modified timestamp (unix ms).</summary>
    public long LastModified { get; set; }

    /// <summary>Quick message content.</summary>
    public QuickMessageContent? Message { get; set; }

    /// <summary>Optional media attachments. Can be null.</summary>
    public QuickMessageMedia? Media { get; set; }
}

/// <summary>
/// Content of a quick message.
/// </summary>
public class QuickMessageContent
{
    /// <summary>Message title/text.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional parameters (JSON string or null).</summary>
    public string? Params { get; set; }
}

/// <summary>
/// Media items in a quick message.
/// </summary>
public class QuickMessageMedia
{
    /// <summary>List of media items.</summary>
    public List<QuickMessageMediaItem> Items { get; set; } = new();
}

/// <summary>
/// A single media item in a quick message.
/// </summary>
public class QuickMessageMediaItem
{
    /// <summary>Media type.</summary>
    public int Type { get; set; }

    /// <summary>Photo ID.</summary>
    public int PhotoId { get; set; }

    /// <summary>Item title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Image width.</summary>
    public int Width { get; set; }

    /// <summary>Image height.</summary>
    public int Height { get; set; }

    /// <summary>Preview thumbnail URL.</summary>
    public string PreviewThumb { get; set; } = string.Empty;

    /// <summary>Raw image URL (original quality).</summary>
    public string RawUrl { get; set; } = string.Empty;

    /// <summary>Thumbnail URL.</summary>
    public string ThumbUrl { get; set; } = string.Empty;

    /// <summary>Normal quality URL.</summary>
    public string NormalUrl { get; set; } = string.Empty;

    /// <summary>HD quality URL.</summary>
    public string HdUrl { get; set; } = string.Empty;
}