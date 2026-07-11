using System;
using System.IO;
using System.Threading.Tasks;
using ICU.Lib.ZaloClientWeb.Models;

namespace ICU.Lib.ZaloClientWeb.Utils;

/// <summary>
/// Provides image metadata retrieval helpers.
/// Equivalent to image metadata features in zca-js (utils.ts getImageMetaData).
/// Uses System.IO.File to get file size; image dimensions need an external library.
/// </summary>
public static class ImageHelper
{
    /// <summary>
    /// Gets image metadata from a file path.
    /// Equivalent to getImageMetaData() in zca-js.
    /// NOTE: Width/Height resolution requires an external image library (e.g. ImageSharp, SkiaSharp).
    /// </summary>
    public static async Task<ImageFileMetadata?> GetImageMetadataAsync(string filePath, ImageMetadataGetter? metadataGetter = null)
    {
        if (metadataGetter == null)
            return null; // No metadata getter configured

        var imageData = await metadataGetter(filePath);
        if (imageData == null) return null;

        var fileName = Path.GetFileName(filePath);

        return new ImageFileMetadata
        {
            FileName = fileName,
            TotalSize = imageData.Size,
            Width = imageData.Width,
            Height = imageData.Height
        };
    }

    /// <summary>
    /// Gets GIF metadata from a file path.
    /// Equivalent to getGifMetaData() in zca-js.
    /// </summary>
    public static async Task<ImageFileMetadata?> GetGifMetadataAsync(string filePath, ImageMetadataGetter? metadataGetter = null)
    {
        if (metadataGetter == null) return null;

        var gifData = await metadataGetter(filePath);
        if (gifData == null) return null;

        return new ImageFileMetadata
        {
            FileName = Path.GetFileName(filePath),
            TotalSize = gifData.Size,
            Width = gifData.Width,
            Height = gifData.Height
        };
    }

    /// <summary>
    /// Gets the file size from disk.
    /// Equivalent to getFileSize() in zca-js.
    /// </summary>
    public static Task<long> GetFileSizeAsync(string filePath)
    {
        return Task.FromResult(new FileInfo(filePath).Length);
    }

    /// <summary>
    /// Gets the file extension from a file path.
    /// Equivalent to getFileExtension() in zca-js.
    /// </summary>
    public static string GetFileExtension(string filePath)
    {
        return Path.GetExtension(filePath).TrimStart('.');
    }

    /// <summary>
    /// Gets the file name from a file path.
    /// Equivalent to getFileName() in zca-js.
    /// </summary>
    public static string GetFileName(string filePath)
    {
        return Path.GetFileName(filePath);
    }
}

/// <summary>
/// Represents image/file metadata for upload to Zalo.
/// </summary>
public class ImageFileMetadata
{
    public string FileName { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}