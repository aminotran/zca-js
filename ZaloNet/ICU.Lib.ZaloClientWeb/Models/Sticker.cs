namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Full sticker detail information.
/// Equivalent to StickerDetail in zca-js.
/// </summary>
public class StickerDetail
{
    /// <summary>Sticker ID.</summary>
    public int Id { get; set; }

    /// <summary>Category ID this sticker belongs to.</summary>
    public int CateId { get; set; }

    /// <summary>Sticker type.</summary>
    public int Type { get; set; }

    /// <summary>Sticker text/label.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Sticker URI.</summary>
    public string Uri { get; set; } = string.Empty;

    /// <summary>FKey value.</summary>
    public int Fkey { get; set; }

    /// <summary>Status.</summary>
    public int Status { get; set; }

    /// <summary>Sticker image URL.</summary>
    public string StickerUrl { get; set; } = string.Empty;

    /// <summary>Sticker sprite sheet URL (for animation).</summary>
    public string StickerSpriteUrl { get; set; } = string.Empty;

    /// <summary>Sticker WebP URL (animated). Can be null.</summary>
    public string? StickerWebpUrl { get; set; }

    /// <summary>Total frames for animated stickers.</summary>
    public int TotalFrames { get; set; }

    /// <summary>Duration in ms per frame.</summary>
    public int Duration { get; set; }

    /// <summary>Effect ID.</summary>
    public int EffectId { get; set; }

    /// <summary>Checksum hash.</summary>
    public string Checksum { get; set; } = string.Empty;

    /// <summary>Extension data.</summary>
    public int Ext { get; set; }

    /// <summary>Source identifier.</summary>
    public int Source { get; set; }

    /// <summary>Version number.</summary>
    public int Version { get; set; }
}

/// <summary>
/// Basic sticker information (used in message payloads).
/// Equivalent to StickerBasic in zca-js.
/// </summary>
public class StickerBasic
{
    /// <summary>Sticker type.</summary>
    public int Type { get; set; }

    /// <summary>Category ID.</summary>
    public int CateId { get; set; }

    /// <summary>Sticker ID.</summary>
    public int StickerId { get; set; }
}

/// <summary>
/// Sticker category detail.
/// Equivalent to sticker category response in zca-js.
/// </summary>
public class StickerCategoryDetail
{
    /// <summary>Category ID.</summary>
    public int Id { get; set; }

    /// <summary>Category name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Category icon URL.</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>List of stickers in this category.</summary>
    public System.Collections.Generic.List<StickerDetail> Stickers { get; set; } = new();
}