using System.Collections.Generic;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Represents a product item in a catalog.
/// Equivalent to ProductCatalogItem in zca-js.
/// </summary>
public class ProductCatalogItem
{
    /// <summary>Product price as string (e.g. "100000").</summary>
    public string Price { get; set; } = string.Empty;

    /// <summary>Product description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Relative path used to build the product URL.
    /// Example: https://catalog.zalo.me/{path}
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Product ID.</summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>Product name.</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Currency unit (e.g. "VND").</summary>
    public string CurrencyUnit { get; set; } = string.Empty;

    /// <summary>List of product photo URLs.</summary>
    public List<string> ProductPhotos { get; set; } = new();

    /// <summary>Creation timestamp (unix ms).</summary>
    public long CreateTime { get; set; }

    /// <summary>Catalog ID this product belongs to.</summary>
    public string CatalogId { get; set; } = string.Empty;

    /// <summary>Owner's user ID.</summary>
    public string OwnerId { get; set; } = string.Empty;
}