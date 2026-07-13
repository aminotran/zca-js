using System.Collections.Generic;
using System.Text.Json;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Strongly-typed settings for file sharing, parsed from server settings.
/// Equivalent to ShareFileSettings type in zca-js context.ts.
/// </summary>
public class ShareFileSettings
{
    /// <summary>Domain list for big file uploads</summary>
    public List<string> BigFileDomainList { get; set; } = new();

    /// <summary>Maximum file size for v2 sharing in bytes</summary>
    public int MaxSizeShareFileV2 { get; set; } = 0;

    /// <summary>Maximum file size for v3 sharing in MB</summary>
    public int MaxSizeShareFileV3 { get; set; } = 50;

    /// <summary>Whether to show 1GB file upload icon</summary>
    public bool FileUploadShowIcon1Gb { get; set; }

    /// <summary>Comma-separated string of restricted extensions</summary>
    public string RestrictedExt { get; set; } = "";

    /// <summary>Minimum time between file uploads in ms</summary>
    public int NextFileTime { get; set; } = 0;

    /// <summary>Maximum number of files per upload</summary>
    public int MaxFile { get; set; } = 10;

    /// <summary>Maximum photo size in bytes</summary>
    public int MaxSizePhoto { get; set; } = 0;

    /// <summary>Maximum file size in bytes</summary>
    public int MaxSizeShareFile { get; set; } = 0;

    /// <summary>Maximum resize photo size in bytes</summary>
    public int MaxSizeResizePhoto { get; set; } = 0;

    /// <summary>Maximum GIF size in bytes</summary>
    public int MaxSizeGif { get; set; } = 0;

    /// <summary>Maximum original photo size in bytes</summary>
    public int MaxSizeOriginalPhoto { get; set; } = 0;

    /// <summary>Chunk size for file uploads in bytes (default: 491520 = 480KB)</summary>
    public int ChunkSizeFile { get; set; } = 491520;

    /// <summary>List of restricted file extensions</summary>
    public List<string> RestrictedExtFile { get; set; } = new();

    /// <summary>
    /// Parse ShareFileSettings from the server settings dictionary.
    /// </summary>
    public static ShareFileSettings FromSettings(Dictionary<string, object> settings)
    {
        var result = new ShareFileSettings();
        if (settings.TryGetValue("sharefile", out var obj) && obj is Dictionary<string, object> sf)
        {
            TryGetInt(sf, "max_size_share_file_v3", v => result.MaxSizeShareFileV3 = v);
            TryGetInt(sf, "max_file", v => result.MaxFile = v);
            TryGetInt(sf, "chunk_size_file", v => result.ChunkSizeFile = v);
            TryGetInt(sf, "max_size_share_file_v2", v => result.MaxSizeShareFileV2 = v);
            TryGetInt(sf, "max_size_photo", v => result.MaxSizePhoto = v);
            TryGetInt(sf, "max_size_share_file", v => result.MaxSizeShareFile = v);
            TryGetInt(sf, "max_size_resize_photo", v => result.MaxSizeResizePhoto = v);
            TryGetInt(sf, "max_size_gif", v => result.MaxSizeGif = v);
            TryGetInt(sf, "max_size_original_photo", v => result.MaxSizeOriginalPhoto = v);
            TryGetInt(sf, "next_file_time", v => result.NextFileTime = v);

            if (sf.TryGetValue("restricted_ext_file", out var extList) && extList is List<object> extObjs)
                result.RestrictedExtFile = extObjs.ConvertAll(e => e?.ToString()?.ToLowerInvariant() ?? "");
            
            if (sf.TryGetValue("restricted_ext", out var extStr))
                result.RestrictedExt = extStr?.ToString() ?? "";

            if (sf.TryGetValue("big_file_domain_list", out var domains) && domains is List<object> domainObjs)
                result.BigFileDomainList = domainObjs.ConvertAll(e => e?.ToString() ?? "");

            if (sf.TryGetValue("file_upload_show_icon_1GB", out var showIcon))
                result.FileUploadShowIcon1Gb = showIcon?.ToString() == "True" || showIcon?.ToString() == "true";
        }
        return result;
    }

    private static void TryGetInt(Dictionary<string, object> dict, string key, System.Action<int> setter)
    {
        if (dict.TryGetValue(key, out var val))
        {
            try { setter(Convert.ToInt32(val)); } catch { }
        }
    }
}