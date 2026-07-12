using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ICU.Lib.ZaloClientWeb.Crypto;
using ICU.Lib.ZaloClientWeb.Models;

namespace ICU.Lib.ZaloClientWeb.Utils;

/// <summary>
/// Utility functions for Zalo API operations.
/// Equivalent to utils.ts in zca-js.
/// </summary>
public static class ZaloUtils
{
    /// <summary>
    /// Generates MD5 sign key for API requests.
    /// Equivalent to getSignKey() in zca-js.
    /// </summary>
    public static string GetSignKey(string type, Dictionary<string, object> paramsDict)
    {
        var keys = paramsDict.Keys.Where(k => paramsDict.ContainsKey(k)).OrderBy(k => k).ToList();
        var sb = new StringBuilder("zsecure" + type);
        foreach (var key in keys)
            sb.Append(paramsDict[key]?.ToString() ?? "");

        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Builds URL with optional Zalo API version params.
    /// Equivalent to makeURL() in zca-js.
    /// </summary>
    /// <param name="baseUrl">Base URL.</param>
    /// <param name="extraParams">Extra query parameters.</param>
    /// <param name="apiVersion">Zalo API version (zpw_ver). Ignored if addApiVersionParams is false.</param>
    /// <param name="apiType">Zalo API type (zpw_type). Ignored if addApiVersionParams is false.</param>
    /// <param name="addApiVersionParams">If false, does NOT add zpw_ver and zpw_type (used for getServerInfo).</param>
    public static string MakeUrl(string baseUrl, Dictionary<string, string>? extraParams = null, int apiVersion = 671, int apiType = 30, bool addApiVersionParams = true)
    {
        var uri = new UriBuilder(baseUrl);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        if (extraParams != null)
        {
            foreach (var kvp in extraParams)
                query[kvp.Key] = kvp.Value;
        }

        if (addApiVersionParams)
        {
            if (string.IsNullOrEmpty(query["zpw_ver"]))
                query["zpw_ver"] = apiVersion.ToString();
            if (string.IsNullOrEmpty(query["zpw_type"]))
                query["zpw_type"] = apiType.ToString();
        }

        uri.Query = query.ToString()!;
        return uri.ToString();
    }

    /// <summary>
    /// Gets default HTTP headers for Zalo API requests.
    /// Equivalent to getDefaultHeaders() in zca-js.
    /// </summary>
    public static Dictionary<string, string> GetDefaultHeaders(CookieContainer? cookieContainer = null, string? userAgent = null)
    {
        var headers = new Dictionary<string, string>
        {
            ["Accept"] = "application/json, text/plain, */*",
            ["Accept-Encoding"] = "gzip, deflate, br, zstd",
            ["Accept-Language"] = "en-US,en;q=0.9",
            ["Content-Type"] = "application/x-www-form-urlencoded",
            ["Origin"] = "https://chat.zalo.me",
            ["Referer"] = "https://chat.zalo.me/",
        };

        if (!string.IsNullOrEmpty(userAgent))
            headers["User-Agent"] = userAgent;

        return headers;
    }

    /// <summary>
    /// Generates Zalo UUID (IMEI) from user agent.
    /// Equivalent to generateZaloUUID() in zca-js.
    /// </summary>
    public static string GenerateZaloUuid(string userAgent)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(userAgent));
        var md5Str = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        return $"{Guid.NewGuid():N}-{md5Str}";
    }

    /// <summary>
    /// Gets client message type number from type string.
    /// Equivalent to getClientMessageType() in zca-js.
    /// </summary>
    public static int GetClientMessageType(string msgType)
    {
        return msgType switch
        {
            "webchat" => 1,
            "chat.voice" => 31,
            "chat.photo" => 32,
            "chat.sticker" => 36,
            "chat.doodle" => 37,
            "chat.recommended" => 38,
            "chat.link" => 38,
            "chat.video.msg" => 44,
            "share.file" => 46,
            "chat.gif" => 49,
            "chat.location.new" => 43,
            _ => 1
        };
    }

    /// <summary>
    /// Converts hex color to negative color number used by Zalo API.
    /// Equivalent to hexToNegativeColor() in zca-js.
    /// </summary>
    public static int HexToNegativeColor(string hex)
    {
        if (!hex.StartsWith("#"))
            hex = "#" + hex;

        var hexValue = hex.Substring(1);
        if (hexValue.Length == 6)
            hexValue = "FF" + hexValue;

        var decimalValue = Convert.ToInt64(hexValue, 16);
        return decimalValue > 0x7FFFFFFF ? (int)(decimalValue - 4294967296) : (int)decimalValue;
    }

    /// <summary>
    /// Converts negative color number from Zalo API to hex color code.
    /// Equivalent to negativeColorToHex() in zca-js.
    /// </summary>
    public static string NegativeColorToHex(int negativeColor)
    {
        var positiveColor = (uint)(negativeColor + 4294967296);
        return "#" + positiveColor.ToString("X8").Substring(2);
    }

    /// <summary>
    /// Encrypts a 4-digit PIN to MD5 hash.
    /// Equivalent to encryptPin() in zca-js.
    /// </summary>
    public static string EncryptPin(string pin)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(pin));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Validates PIN against MD5 hash.
    /// Equivalent to validatePin() in zca-js.
    /// </summary>
    public static bool ValidatePin(string encryptedPin, string pin)
    {
        return EncryptPin(pin).Equals(encryptedPin, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Computes MD5 hash for large files in chunks (for file upload).
    /// Equivalent to getMd5LargeFileObject() in zca-js.
    /// </summary>
    public static string GetMd5LargeFile(byte[] buffer, int chunkSize = 2097152)
    {
        using var md5 = MD5.Create();
        var totalChunks = (int)Math.Ceiling((double)buffer.Length / chunkSize);

        for (int i = 0; i < totalChunks; i++)
        {
            var start = i * chunkSize;
            var length = Math.Min(chunkSize, buffer.Length - start);
            md5.TransformBlock(buffer, start, length, buffer, start);
        }

        md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return BitConverter.ToString(md5.Hash ?? Array.Empty<byte>()).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Converts a timestamp to a formatted time string.
    /// Equivalent to formatTime() in zca-js.
    /// </summary>
    public static string FormatTime(string format, long timestamp)
    {
        var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
        return format
            .Replace("%H", date.Hour.ToString("D2"))
            .Replace("%M", date.Minute.ToString("D2"))
            .Replace("%S", date.Second.ToString("D2"))
            .Replace("%d", date.Day.ToString("D2"))
            .Replace("%m", date.Month.ToString("D2"))
            .Replace("%Y", date.Year.ToString());
    }

    /// <summary>
    /// Gets full time string from millisecond timestamp.
    /// Equivalent to getFullTimeFromMillisecond() in zca-js.
    /// </summary>
    public static string GetFullTimeFromMilliseconds(long ms)
    {
        var date = DateTimeOffset.FromUnixTimeMilliseconds(ms).DateTime;
        return $"{date.Hour:D2}:{date.Minute:D2} {date.Day:D2}/{date.Month:D2}/{date.Year}";
    }

    /// <summary>
    /// Pads a string on the left with a specified character.
    /// Equivalent to strPadLeft() in zca-js.
    /// </summary>
    public static string StrPadLeft(string input, char padChar, int totalWidth)
    {
        if (input.Length >= totalWidth)
            return input.Length > totalWidth ? input.Substring(input.Length - totalWidth) : input;
        return new string(padChar, totalWidth - input.Length) + input;
    }

    /// <summary>
    /// Removes keys with null/undefined values from a dictionary.
    /// Equivalent to removeUndefinedKeys() in zca-js.
    /// </summary>
    public static Dictionary<string, object?> RemoveNullKeys(Dictionary<string, object?> dict)
    {
        var result = new Dictionary<string, object?>();
        foreach (var kvp in dict)
        {
            if (kvp.Value != null)
                result[kvp.Key] = kvp.Value;
        }
        return result;
    }

    /// <summary>
    /// Decodes a Base64 string to a byte array.
    /// Equivalent to decodeBase64ToBuffer() in zca-js.
    /// </summary>
    public static byte[] DecodeBase64ToBuffer(string data)
    {
        return Convert.FromBase64String(data);
    }

    /// <summary>
    /// Decodes a Uint8Array (byte array) to UTF-8 string.
    /// Equivalent to decodeUnit8Array() in zca-js.
    /// </summary>
    public static string? DecodeUint8Array(byte[] data)
    {
        try { return Encoding.UTF8.GetString(data); }
        catch { return null; }
    }

    /// <summary>
    /// Maps a group event action string to GroupEventType integer.
    /// </summary>
    public static int GetGroupEventType(string act)
    {
        return act switch
        {
            "join_request" => 1, "join" => 2, "leave" => 3,
            "remove_member" => 4, "block_member" => 5, "update_setting" => 6,
            "update_avatar" => 7, "update" => 8, "new_link" => 9,
            "add_admin" => 10, "remove_admin" => 11, "new_pin_topic" => 12,
            "update_pin_topic" => 13, "update_topic" => 14, "update_board" => 15,
            "remove_board" => 16, "reorder_pin_topic" => 17, "unpin_topic" => 18,
            "remove_topic" => 19, "accept_remind" => 20, "reject_remind" => 21,
            "remind_topic" => 22, _ => 0
        };
    }

    /// <summary>
    /// Maps a friend event action string to FriendEventType integer.
    /// </summary>
    public static int GetFriendEventType(string act)
    {
        return act switch
        {
            "add" => 1, "remove" => 2, "block" => 3, "unblock" => 4,
            "block_call" => 5, "unblock_call" => 6, "req_v2" => 7,
            "reject" => 8, "undo_req" => 9, "seen_fr_req" => 10,
            "pin_unpin" => 11, "pin_create" => 12, _ => 0
        };
    }

    /// <summary>
    /// Parses and decrypts a Zalo API response.
    /// </summary>
    public static async Task<ZaloApiResponse<T>> HandleZaloResponse<T>(HttpResponseMessage response, string? secretKey = null, bool isEncrypted = true)
    {
        var result = new ZaloApiResponse<T>();

        if (!response.IsSuccessStatusCode)
        {
            result.Error = $"Request failed with status code {(int)response.StatusCode}";
            return result;
        }

        try
        {
            var json = await response.Content.ReadAsStringAsync();
            var parsed = JsonSerializer.Deserialize<ZaloResponseData>(json);
            if (parsed == null)
            {
                result.Error = "Failed to parse response";
                return result;
            }

            if (parsed.ErrorCode != 0)
            {
                result.Error = parsed.ErrorMessage;
                result.ErrorCode = parsed.ErrorCode;
                return result;
            }

            string decodedDataStr;
            if (isEncrypted && !string.IsNullOrEmpty(secretKey) && !string.IsNullOrEmpty(parsed.Data))
            {
                decodedDataStr = AesHelper.DecryptAesCbc(secretKey, parsed.Data);
                if (decodedDataStr == null)
                {
                    result.Error = "Failed to decrypt response";
                    return result;
                }
            }
            else
            {
                decodedDataStr = parsed.Data ?? "{}";
            }

            var decoded = JsonSerializer.Deserialize<ZaloResponseData<T>>(decodedDataStr);
            if (decoded == null)
            {
                result.Error = "Failed to parse decoded response";
                return result;
            }

            if (decoded.ErrorCode != 0)
            {
                result.Error = decoded.ErrorMessage;
                result.ErrorCode = decoded.ErrorCode;
                return result;
            }

            result.Data = decoded.Data;
        }
        catch (Exception ex)
        {
            result.Error = $"Failed to parse response: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Decodes event data from WebSocket events.
    /// Equivalent to decodeEventData() in zca-js.
    /// </summary>
    public static async Task<T?> DecodeEventData<T>(string rawData, int encryptType, string? cipherKey = null)
    {
        if (encryptType == 0)
            return JsonSerializer.Deserialize<T>(rawData);

        var decodedBuffer = Convert.FromBase64String(encryptType == 1 ? rawData : Uri.UnescapeDataString(rawData));

        byte[] decryptedBuffer;
        if (encryptType != 1)
        {
            if (string.IsNullOrEmpty(cipherKey) || decodedBuffer.Length < 48)
                return default;

            var key = Convert.FromBase64String(cipherKey);
            var result = AesHelper.DecryptEventDataGcm(key, decodedBuffer);
            if (result == null) return default;
            decryptedBuffer = result;
        }
        else
        {
            decryptedBuffer = decodedBuffer;
        }

        byte[] decompressedBuffer;
        if (encryptType == 3)
        {
            decompressedBuffer = decryptedBuffer;
        }
        else
        {
            using var compressedStream = new System.IO.MemoryStream(decryptedBuffer);
            using var deflateStream = new System.IO.Compression.DeflateStream(compressedStream, System.IO.Compression.CompressionMode.Decompress);
            using var resultStream = new System.IO.MemoryStream();
            await deflateStream.CopyToAsync(resultStream);
            decompressedBuffer = resultStream.ToArray();
        }

        var decodedData = Encoding.UTF8.GetString(decompressedBuffer);
        if (string.IsNullOrEmpty(decodedData))
            return default;

        return JsonSerializer.Deserialize<T>(decodedData);
    }

    private class ZaloResponseData
    {
        public int ErrorCode { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string? Data { get; set; }
    }

    private class ZaloResponseData<T>
    {
        public int ErrorCode { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public T? Data { get; set; }
    }
}

/// <summary>
/// Generic Zalo API response wrapper.
/// </summary>
public class ZaloApiResponse<T>
{
    public T? Data { get; set; }
    public string? Error { get; set; }
    public int? ErrorCode { get; set; }
    public bool IsSuccess => Error == null;
}