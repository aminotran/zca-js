using System.Text.Json;
using System.Text.Json.Serialization;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Upload callback type for video/file uploads that need WebSocket confirmation.
/// Equivalent to UploadCallback in zca-js context.ts.
/// </summary>
public delegate void UploadCallback(JsonElement wsData);

/// <summary>
/// Source for file upload — can be a file path string or a buffer with metadata.
/// Equivalent to AttachmentSource in zca-js.
/// </summary>
public class AttachmentSource
{
    /// <summary>File path (if loading from disk).</summary>
    public string? FilePath { get; set; }

    /// <summary>File data buffer (if loading from memory).</summary>
    public byte[]? Data { get; set; }

    /// <summary>Original filename (required when using Data).</summary>
    public string? FileName { get; set; }

    /// <summary>Image/video metadata (width, height, totalSize).</summary>
    public AttachmentMetadata? Metadata { get; set; }
}

public class AttachmentMetadata
{
    public long TotalSize { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>
/// Response from uploading an image attachment.
/// Equivalent to UploadAttachmentImageResponse in zca-js.
/// </summary>
public class UploadAttachmentImageResponse
{
    public string FileType => "image";

    public string? NormalUrl { get; set; }
    public string? PhotoId { get; set; }
    public int Finished { get; set; }
    public string? HdUrl { get; set; }
    public string? ThumbUrl { get; set; }
    public long ClientFileId { get; set; }
    public int ChunkId { get; set; }

    public int Width { get; set; }
    public int Height { get; set; }
    public long TotalSize { get; set; }
    public long HdSize { get; set; }
}

/// <summary>
/// Response from uploading a video attachment.
/// Equivalent to UploadAttachmentVideoResponse in zca-js.
/// </summary>
public class UploadAttachmentVideoResponse
{
    public string FileType => "video";

    public int Finished { get; set; }
    public long ClientFileId { get; set; }
    public int ChunkId { get; set; }

    public string? FileUrl { get; set; }
    public string? FileId { get; set; }
    public string? Checksum { get; set; }
    public long TotalSize { get; set; }
    public string? FileName { get; set; }
}

/// <summary>
/// Response from uploading a file attachment (non-image, non-video).
/// Equivalent to UploadAttachmentFileResponse in zca-js.
/// </summary>
public class UploadAttachmentFileResponse
{
    public string FileType => "others";

    public int Finished { get; set; }
    public long ClientFileId { get; set; }
    public int ChunkId { get; set; }

    public string? FileUrl { get; set; }
    public string? FileId { get; set; }
    public string? Checksum { get; set; }
    public long TotalSize { get; set; }
    public string? FileName { get; set; }
}

/// <summary>
/// Union type for upload attachment responses.
/// </summary>
public class UploadAttachmentResult
{
    public string FileType { get; set; } = "others";

    // Image fields
    public string? NormalUrl { get; set; }
    public string? PhotoId { get; set; }
    public string? HdUrl { get; set; }
    public string? ThumbUrl { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long HdSize { get; set; }

    // Video/File fields
    public string? FileUrl { get; set; }
    public string? FileId { get; set; }
    public string? Checksum { get; set; }
    public string? FileName { get; set; }

    // Common fields
    public int Finished { get; set; }
    public long ClientFileId { get; set; }
    public int ChunkId { get; set; }
    public long TotalSize { get; set; }

    public bool IsImage => FileType == "image";
    public bool IsVideo => FileType == "video";
}