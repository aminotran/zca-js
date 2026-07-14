namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Extra version information from Zalo server.
/// Equivalent to ExtraVer type in zca-js context.ts.
/// </summary>
public class ExtraVerInfo
{
    /// <summary>Phonebook version number.</summary>
    public int Phonebook { get; set; }

    /// <summary>Conversation label version.</summary>
    public string ConvLabel { get; set; } = string.Empty;

    /// <summary>Friend list version.</summary>
    public string Friend { get; set; } = string.Empty;

    /// <summary>Sticker GIF suggest version.</summary>
    public int VerStickerGiphySuggest { get; set; }

    /// <summary>GIF category version.</summary>
    public int VerGiphyCate { get; set; }

    /// <summary>Alias/friend-nickname version.</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>Sticker category list version.</summary>
    public int VerStickerCateList { get; set; }

    /// <summary>Blocked friend list version.</summary>
    public string BlockFriend { get; set; } = string.Empty;
}