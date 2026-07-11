using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ICU.Lib.ZaloClientWeb.Crypto;
using ICU.Lib.ZaloClientWeb.Models;

namespace ICU.Lib.ZaloClientWeb.Utils;

/// <summary>
/// Helper class for making Zalo API calls with proper authentication and encryption.
/// Equivalent to the apiFactory() utility in zca-js.
/// </summary>
public static class ApiMethods
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Calls a Zalo GET API endpoint and returns the raw JSON response as a JsonElement.
    /// </summary>
    public static async Task<ZaloApiResponse<JsonElement>> CallGetApiAsync(ZaloContext ctx, HttpClient httpClient, string endpoint, object? parameters = null)
    {
        var url = BuildApiUrl(ctx, endpoint, parameters, true);
        return await SendApiRequestAsync(ctx, httpClient, url, HttpMethod.Get);
    }

    /// <summary>
    /// Calls a Zalo POST API endpoint and returns the raw JSON response as a JsonElement.
    /// </summary>
    public static async Task<ZaloApiResponse<JsonElement>> CallPostApiAsync(ZaloContext ctx, HttpClient httpClient, string endpoint, object? data = null)
    {
        var url = BuildApiUrl(ctx, endpoint);
        return await SendApiRequestAsync(ctx, httpClient, url, HttpMethod.Post, data);
    }

    /// <summary>
    /// Calls a custom API endpoint and returns the raw JSON response as a JsonElement.
    /// </summary>
    public static async Task<ZaloApiResponse<JsonElement>> CallCustomApiAsync(ZaloContext ctx, HttpClient httpClient, string method, string endpoint, object? data = null, bool isGet = true)
    {
        var url = BuildApiUrl(ctx, endpoint);
        return await SendApiRequestAsync(ctx, httpClient, url, isGet ? HttpMethod.Get : HttpMethod.Post, data);
    }

    /// <summary>
    /// Builds the complete API URL with Zalo version parameters and authentication.
    /// </summary>
    private static string BuildApiUrl(ZaloContext ctx, string endpoint, object? parameters = null, bool flattenToQuery = false)
    {
        var baseUrl = endpoint.StartsWith("http") ? endpoint : $"https://wpa.chat.zalo.me/api/{endpoint}";
        var extraParams = new Dictionary<string, string>();

        if (parameters != null && flattenToQuery)
        {
            var dict = ObjectToDictionary(parameters);
            foreach (var kvp in dict)
            {
                if (kvp.Value != null)
                    extraParams[kvp.Key] = kvp.Value.ToString() ?? "";
            }
        }

        return ZaloUtils.MakeUrl(baseUrl, extraParams.Count > 0 ? extraParams : null, ctx.ApiVersion, ctx.ApiType);
    }

    /// <summary>
    /// Sends an HTTP request to the Zalo API and handles the encrypted response.
    /// </summary>
    private static async Task<ZaloApiResponse<JsonElement>> SendApiRequestAsync(ZaloContext ctx, HttpClient httpClient, string url, HttpMethod method, object? data = null)
    {
        try
        {
            var request = new HttpRequestMessage(method, url);

            request.Headers.Add("User-Agent", ctx.UserAgent);
            if (!string.IsNullOrEmpty(ctx.Imei))
                request.Headers.Add("x-zalo-imei", ctx.Imei);

            if (data != null && method == HttpMethod.Post)
            {
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                var formData = new Dictionary<string, string>
                {
                    ["data"] = json
                };

                if (!string.IsNullOrEmpty(ctx.SecretKey))
                {
                    var encrypted = AesHelper.EncryptAesCbc(ctx.SecretKey, json);
                    if (encrypted != null)
                        formData["data"] = encrypted;
                }

                request.Content = new FormUrlEncodedContent(formData);
            }

            var response = await httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            if (!root.TryGetProperty("error_code", out var errorCodeElement) || errorCodeElement.GetInt32() != 0)
            {
                var errorMsg = root.TryGetProperty("error_message", out var errMsgEl)
                    ? errMsgEl.GetString() ?? "Unknown error"
                    : "Unknown error";
                var errorCode = root.TryGetProperty("error_code", out var errCodeEl)
                    ? errCodeEl.GetInt32()
                    : -1;
                return new ZaloApiResponse<JsonElement>
                {
                    Error = errorMsg,
                    ErrorCode = errorCode
                };
            }

            if (!root.TryGetProperty("data", out var dataElement))
            {
                return new ZaloApiResponse<JsonElement> { Error = "No data in response" };
            }

            var rawData = dataElement.GetString();
            if (string.IsNullOrEmpty(rawData))
            {
                using var emptyDoc = JsonDocument.Parse("{}");
                return new ZaloApiResponse<JsonElement> { Data = emptyDoc.RootElement.Clone() };
            }

            string decryptedData;
            if (!string.IsNullOrEmpty(ctx.SecretKey))
            {
                decryptedData = AesHelper.DecryptAesCbc(ctx.SecretKey, rawData);
                if (decryptedData == null)
                    return new ZaloApiResponse<JsonElement> { Error = "Failed to decrypt response" };
            }
            else
            {
                decryptedData = rawData;
            }

            using var innerDoc = JsonDocument.Parse(decryptedData);
            var innerRoot = innerDoc.RootElement;

            if (innerRoot.TryGetProperty("error_code", out var innerErrorCode) && innerErrorCode.GetInt32() != 0)
            {
                var innerErrorMsg = innerRoot.TryGetProperty("error_message", out var innerErrMsg)
                    ? innerErrMsg.GetString() ?? "Unknown error"
                    : "Unknown error";
                return new ZaloApiResponse<JsonElement>
                {
                    Error = innerErrorMsg,
                    ErrorCode = innerErrorCode.GetInt32()
                };
            }

            JsonElement responseData = innerRoot.TryGetProperty("data", out var innerData)
                ? innerData.Clone()
                : innerRoot.Clone();

            return new ZaloApiResponse<JsonElement> { Data = responseData };
        }
        catch (Exception ex)
        {
            return new ZaloApiResponse<JsonElement> { Error = $"Request failed: {ex.Message}" };
        }
    }

    private static Dictionary<string, object?> ObjectToDictionary(object obj)
    {
        return obj.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(
                prop => char.ToLowerInvariant(prop.Name[0]) + prop.Name.Substring(1),
                prop => prop.GetValue(obj, null)
            );
    }
}