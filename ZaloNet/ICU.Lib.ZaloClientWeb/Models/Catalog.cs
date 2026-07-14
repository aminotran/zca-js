namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Represents a catalog item (product group/category).
/// Equivalent to CatalogItem in zca-js.
/// </summary>
public class CatalogItem
{
    /// <summary>Catalog ID.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Catalog name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Version number.</summary>
    public int Version { get; set; }

    /// <summary>Owner's user ID.</summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>Whether this is the default catalog.</summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Relative path used to build the catalog URL.
    /// Example: https://catalog.zalo.me/{path}
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Catalog photo URL (can be null).</summary>
    public string? CatalogPhoto { get; set; }

    /// <summary>Total number of products in this catalog.</summary>
    public int TotalProduct { get; set; }

    /// <summary>Creation timestamp (unix ms).</summary>
    public long CreatedTime { get; set; }
}