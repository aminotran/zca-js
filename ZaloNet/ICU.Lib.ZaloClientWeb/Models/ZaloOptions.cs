using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Image metadata getter delegate for retrieving image dimensions and size.
/// Corresponds to ImageMetadataGetter type in zca-js.
/// </summary>
/// <param name="filePath">Path to the image file</param>
/// <returns>Image metadata or null if failed</returns>
public delegate Task<ImageMetadata?> ImageMetadataGetter(string filePath);

/// <summary>
/// Image metadata returned by ImageMetadataGetter.
/// </summary>
public class ImageMetadata
{
    public int Width { get; set; }
    public int Height { get; set; }
    public long Size { get; set; }
}

/// <summary>
/// Configuration options for ZaloClient.
/// Equivalent to the Options type in zca-js context.ts.
/// </summary>
public class ZaloOptions
{
    /// <summary>
    /// Whether to listen to self-sent messages/events.
    /// </summary>
    public bool SelfListen { get; set; } = false;

    /// <summary>
    /// Whether to check for updates on startup.
    /// </summary>
    public bool CheckUpdate { get; set; } = true;

    /// <summary>
    /// Whether to enable logging.
    /// </summary>
    public bool Logging { get; set; } = true;

    /// <summary>
    /// Zalo API type version.
    /// </summary>
    public int ApiType { get; set; } = 30;

    /// <summary>
    /// Zalo API version number.
    /// </summary>
    public int ApiVersion { get; set; } = 671;

    /// <summary>
    /// Optional HTTP proxy agent.
    /// </summary>
    public IWebProxy? Proxy { get; set; }

    /// <summary>
    /// Optional image metadata getter (e.g. using ImageSharp).
    /// </summary>
    public ImageMetadataGetter? ImageMetadataGetter { get; set; }

    /// <summary>
    /// Optional callback for logging API request/response details.
    /// Useful for debugging API calls.
    /// </summary>
    public Action<string>? ApiLogCallback { get; set; }
}
