namespace ICU.Lib.ZaloClientWeb.Models.Types;

/// <summary>
/// Avatar size constants used for requesting specific avatar sizes from Zalo API.
/// Equivalent to AvatarSize enum in zca-js Enum.ts.
/// </summary>
public enum AvatarSize
{
    /// <summary>Small avatar (120px). Default for most requests.</summary>
    Small = 120,
    /// <summary>Large avatar (240px). Higher resolution.</summary>
    Large = 240,
}