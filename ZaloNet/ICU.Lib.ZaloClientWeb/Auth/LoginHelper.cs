using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ICU.Lib.ZaloClientWeb.Crypto;
using ICU.Lib.ZaloClientWeb.Exceptions;
using ICU.Lib.ZaloClientWeb.Models;
using ICU.Lib.ZaloClientWeb.Utils;

namespace ICU.Lib.ZaloClientWeb.Auth;

/// <summary>
/// Handles cookie-based login to Zalo API.
/// Implements the full login flow matching zca-js (apis/login.ts):
/// 1. Encrypt params with ParamsEncryptor
/// 2. GET /api/login/getLoginInfo → decrypt response → get LoginInfo
/// 3. GET /api/login/getServerInfo for settings
/// </summary>
public class LoginHelper
{
    private readonly ZaloClient _client;
    private readonly ZaloOptions _options;
    private readonly HttpClient _httpClient;
    private readonly CookieContainer _cookieContainer;

    internal ZaloLogger Logger { get; }

    public LoginHelper(ZaloClient client, ZaloOptions options, HttpClient httpClient, CookieContainer cookieContainer)
    {
        _client = client;
        _options = options;
        _httpClient = httpClient;
        _cookieContainer = cookieContainer;
        Logger = new ZaloLogger(options.Logging);
    }

    /// <summary>
    /// Performs login with cookie credentials.
    /// </summary>
    public async Task<ZaloContext> LoginAsync(Credentials credentials)
    {
        ValidateParams(credentials);

        var ctx = new ZaloContext(_options)
        {
            Imei = credentials.Imei,
            UserAgent = credentials.UserAgent,
            Language = credentials.Language ?? "vi"
        };

        _client.ApplyCookies(credentials.Cookie);

        Logger.Info("Performing login...");
        var loginData = await PerformLoginAsync(ctx);
        if (loginData == null)
            throw new ZaloApiException("Login failed: could not get login info");

        Logger.Info("Getting server info...");
        var serverInfo = await GetServerInfoAsync(ctx);
        if (serverInfo == null)
            throw new ZaloApiException("Failed to get server info");

        ctx.SecretKey = loginData.ZpwEnk;
        ctx.Uid = loginData.Uid;
        ctx.Settings = serverInfo.Settings ?? new Dictionary<string, object>();
        ctx.ExtraVer = serverInfo.ExtraVer;
        ctx.LoginInfo = loginData;
        ctx.ZpwServiceMapV3 = loginData.ZpwServiceMapV3;
        // ctx.ZpwWsUrls is populated from the parsed login response which has zpw_ws as string[]
        // But LoginInfo.ZpwWs is string? (single URL). We need the array from the raw response.
        // The raw LoginResponse has ZpwWs as string[] - we populate ZpwWsUrls from there
        ctx.ZpwWs = loginData.ZpwWs;
        // ZpwWsUrls should be the zpw_ws array from the login response
        if (loginData.ZpwWsUrls != null && loginData.ZpwWsUrls.Length > 0)
            ctx.ZpwWsUrls = loginData.ZpwWsUrls;
        else if (loginData.ZpwWs != null && !string.IsNullOrEmpty(loginData.ZpwWs))
            ctx.ZpwWsUrls = new[] { loginData.ZpwWs };
        else if (loginData.ZpwServiceMapV3.ContainsKey("chat"))
            ctx.ZpwWsUrls = loginData.ZpwServiceMapV3["chat"];
        else
            ctx.ZpwWsUrls = Array.Empty<string>();

        if (string.IsNullOrEmpty(ctx.SecretKey))
            throw new ZaloApiException("Context initialization failed - no secret key (zpw_enk)");

        Logger.Info("Logged in as", ctx.Uid.ToString());
        return ctx;
    }

    private void ValidateParams(Credentials credentials)
    {
        if (string.IsNullOrEmpty(credentials.Imei))
            throw new ZaloApiException("Missing required param: imei");
        if (credentials.Cookie == null || credentials.Cookie.Count == 0)
            throw new ZaloApiException("Missing required param: cookie");
        if (string.IsNullOrEmpty(credentials.UserAgent))
            throw new ZaloApiException("Missing required param: userAgent");
    }

    /// <summary>
    /// Calls getLoginInfo API. Equivalent to login() in zca-js.
    /// Uses UTF-8 key encryption (matching cryptojs.enc.Utf8.parse behavior).
    /// </summary>
    private async Task<LoginInfo?> PerformLoginAsync(ZaloContext ctx)
    {
        Logger.Verbose("=== STEP: PerformLoginAsync (getLoginInfo) ===");
        try
        {
            var (encryptedParams, enk) = await GetEncryptParamsAsync(ctx, "getlogininfo");
            Logger.Verbose("EncryptParams generated. enk length:", enk?.Length.ToString() ?? "null");

            var formData = new Dictionary<string, string>(encryptedParams)
            {
                ["nretry"] = "0"
            };

            var url = ZaloUtils.MakeUrl("https://wpa.chat.zalo.me/api/login/getLoginInfo",
                formData, ctx.ApiVersion, ctx.ApiType);
            Logger.Verbose("GET URL:", url);

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("user-agent", ctx.UserAgent);
            request.Headers.TryAddWithoutValidation("origin", "https://chat.zalo.me");
            request.Headers.TryAddWithoutValidation("referer", "https://chat.zalo.me/");

            var response = await _httpClient.SendAsync(request);
            Logger.Verbose("Response status:", ((int)response.StatusCode).ToString());
            if (!response.IsSuccessStatusCode)
                throw new ZaloApiException($"Failed to fetch login info: {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync();
            Logger.Verbose("Raw response (first 200 chars):", json.Substring(0, Math.Min(200, json.Length)));

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("error_code", out var errEl) || errEl.GetInt32() != 0)
            {
                var errMsg = root.TryGetProperty("error_message", out var em) ? em.GetString() : "Unknown error";
                throw new ZaloApiException($"Login failed: {errMsg}");
            }

            if (!root.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.String)
                throw new ZaloApiException("Failed to fetch login info: no data");

            var encryptedData = dataEl.GetString();
            Logger.Verbose("Encrypted data length:", encryptedData?.Length.ToString() ?? "null");
            if (string.IsNullOrEmpty(encryptedData))
                throw new ZaloApiException("Failed to fetch login info: empty data");

            // Decrypt: the "enk" from ParamsEncryptor is a 32-char hex string
            // In zca-js: decryptResp(encryptedParams.enk, data.data) → decodeRespAES → cryptojs.enc.Utf8.parse(key)
            // So the key is the ASCII bytes of the 32-char string, NOT base64-decoded
            string decryptedStr;
            if (!string.IsNullOrEmpty(enk))
            {
                Logger.Verbose("Decrypting with enk (UTF-8 key)");
                decryptedStr = AesHelper.DecryptResponseAes(enk, encryptedData);
                Logger.Verbose("Decrypted string exists:", (decryptedStr != null).ToString());
                if (decryptedStr == null)
                {
                    Logger.Warn("DecryptResponseAes with UTF-8 key failed, falling back to Base64 key...");
                    decryptedStr = AesHelper.DecryptAesCbc(enk, encryptedData);
                    Logger.Verbose("Fallback decryption exists:", (decryptedStr != null).ToString());
                }
                if (decryptedStr == null)
                    throw new ZaloApiException("Failed to decrypt login response");
            }
            else
            {
                Logger.Warn("No enk provided, using raw encrypted data as decrypted");
                decryptedStr = encryptedData;
            }

            // The decrypted response has a wrapper: {"error_code":0,"error_message":"","data":{...actual login info...}}
            Logger.Verbose("Decrypted response (first 300 chars):", decryptedStr.Substring(0, Math.Min(300, decryptedStr.Length)));

            using var loginDoc = JsonDocument.Parse(decryptedStr);
            var loginRoot = loginDoc.RootElement;
            if (loginRoot.TryGetProperty("data", out var innerData) && innerData.ValueKind == JsonValueKind.Object)
            {
                decryptedStr = innerData.GetRawText();
                Logger.Verbose("Extracted inner 'data' object from wrapper");
            }
            else
            {
                Logger.Warn("No 'data' wrapper found in decrypted response - trying direct parse");
            }

            var loginInfo = JsonSerializer.Deserialize<LoginResponse>(decryptedStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (loginInfo == null)
                throw new ZaloApiException("Failed to parse login response");

            Logger.Info($"Login parsed: uid={loginInfo.Uid}, zpw_enk={(string.IsNullOrEmpty(loginInfo.ZpwEnk) ? "NULL" : "OK")}");
            Logger.Info($"Login parsed: zpw_ws count={loginInfo.ZpwWs?.Length ?? 0}, hasServiceMap={loginInfo.ZpwServiceMapV3?.Count > 0}");

            if (string.IsNullOrEmpty(loginInfo.ZpwEnk))
                Logger.Warn("ZpwEnk is null/empty in decrypted response");
            if (string.IsNullOrEmpty(loginInfo.Uid))
                Logger.Warn("Uid is null/empty in decrypted response");

            return new LoginInfo
            {
                Uid = long.TryParse(loginInfo.Uid, out var uid) ? uid : 0,
                ZpwEnk = loginInfo.ZpwEnk,
                ZpwWs = loginInfo.ZpwWs?.Length > 0 ? loginInfo.ZpwWs[0] : null,
                ZpwWsUrls = loginInfo.ZpwWs?.Length > 0 ? loginInfo.ZpwWs : null,
                ZpwServiceMapV3 = loginInfo.ZpwServiceMapV3 ?? new Dictionary<string, string[]>(),
                Send2MeId = loginInfo.Send2MeId,
                Language = loginInfo.Language,
                PublicIp = loginInfo.PublicIp,
                Haspcclient = loginInfo.Haspcclient
            };
        }
        catch (HttpRequestException ex)
        {
            Logger.Error("Login failed (HTTP):", ex.Message);
            throw new ZaloApiException($"Login failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Calls getServerInfo API. Equivalent to getServerInfo() in zca-js.
    /// </summary>
    private async Task<ServerInfoData?> GetServerInfoAsync(ZaloContext ctx)
    {
        try
        {
            var (encryptedParams, _) = await GetEncryptParamsAsync(ctx, "getserverinfo");

            if (!encryptedParams.TryGetValue("signkey", out var signKey) || string.IsNullOrEmpty(signKey))
                throw new ZaloApiException("Missing signkey for getServerInfo");

            var formData = new Dictionary<string, string>
            {
                ["imei"] = ctx.Imei,
                ["type"] = ctx.ApiType.ToString(),
                ["client_version"] = ctx.ApiVersion.ToString(),
                ["computer_name"] = "Web",
                ["signkey"] = signKey
            };

            var url = ZaloUtils.MakeUrl("https://wpa.chat.zalo.me/api/login/getServerInfo",
                formData, ctx.ApiVersion, ctx.ApiType, addApiVersionParams: false);

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("user-agent", ctx.UserAgent);
            request.Headers.TryAddWithoutValidation("origin", "https://chat.zalo.me");
            request.Headers.TryAddWithoutValidation("referer", "https://chat.zalo.me/");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new ZaloApiException($"Failed to fetch server info: {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error_code", out var errEl) && errEl.GetInt32() != 0)
            {
                var errMsg = root.TryGetProperty("error_message", out var em) ? em.GetString() : "Unknown error";
                throw new ZaloApiException($"Failed to fetch server info: {errMsg}");
            }

            if (!root.TryGetProperty("data", out var dataEl))
                throw new ZaloApiException("Failed to fetch server info: no data");

            var serverInfo = JsonSerializer.Deserialize<ServerInfoResponse>(dataEl.GetRawText());

            ExtraVerInfo? extraVerObj = null;
            string? extraVerStr = null;
            if (serverInfo?.ExtraVer != null)
            {
                try 
                { 
                    extraVerStr = serverInfo.ExtraVer.Value.GetRawText();
                    extraVerObj = JsonSerializer.Deserialize<ExtraVerInfo>(extraVerStr); 
                }
                catch { /* best-effort */ }
            }

            return new ServerInfoData
            {
                Settings = serverInfo != null
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>((serverInfo.Setttings ?? serverInfo.Settings)?.GetRawText() ?? "{}")
                    : new Dictionary<string, object>(),
                ExtraVer = extraVerObj,
                ExtraVerStr = extraVerStr
            };
        }
        catch (HttpRequestException ex)
        {
            Logger.Error("Failed to fetch server info:", ex.Message);
            throw new ZaloApiException($"Failed to fetch server info: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Builds encrypted parameters for API calls. Equivalent to getEncryptParam() in zca-js.
    /// </summary>
    private async Task<(Dictionary<string, string> Params, string? Enk)> GetEncryptParamsAsync(ZaloContext ctx, string type)
    {
        var data = new Dictionary<string, object>
        {
            ["computer_name"] = "Web",
            ["imei"] = ctx.Imei,
            ["language"] = ctx.Language,
            ["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        var encryptedData = await EncryptParamAsync(ctx, data);
        var finalParams = new Dictionary<string, string>();

        if (encryptedData == null)
        {
            foreach (var kvp in data)
                finalParams[kvp.Key] = kvp.Value?.ToString() ?? "";
        }
        else
        {
            foreach (var kvp in encryptedData.EncryptedParams)
                finalParams[kvp.Key] = kvp.Value;
            finalParams["params"] = encryptedData.EncodedData;
        }

        finalParams["type"] = ctx.ApiType.ToString();
        finalParams["client_version"] = ctx.ApiVersion.ToString();

        if (type == "getserverinfo")
        {
            var signDict = new Dictionary<string, object>
            {
                ["imei"] = ctx.Imei,
                ["type"] = ctx.ApiType,
                ["client_version"] = ctx.ApiVersion,
                ["computer_name"] = "Web"
            };
            finalParams["signkey"] = ZaloUtils.GetSignKey(type, signDict);
        }
        else
        {
            var signDict = new Dictionary<string, object>();
            foreach (var kvp in finalParams)
                signDict[kvp.Key] = kvp.Value;
            finalParams["signkey"] = ZaloUtils.GetSignKey(type, signDict);
        }

        return (finalParams, encryptedData?.Enk);
    }

    /// <summary>
    /// Creates encrypted parameters using ParamsEncryptor. Equivalent to _encryptParam() in zca-js.
    /// The encrypt key from ParamsEncryptor is used as Base64 key for AES (not UTF-8),
    /// because it's a derived hex-like string that's then base64-encoded.
    /// But in zca-js: ParamsEncryptor.encodeAES(encryptedKey, stringifiedData, "base64", false)
    /// uses the key as UTF-8 bytes. So we use EncryptAesCbc which takes base64 key.
    /// Actually the encryptKey is a 32-char hex string, used as ASCII bytes for AES key.
    /// We need EncryptAesCbcWithUtf8Key for this.
    /// </summary>
    private Task<EncryptResult?> EncryptParamAsync(ZaloContext ctx, Dictionary<string, object> data)
    {
        try
        {
            var encryptor = new ParamsEncryptor(ctx.ApiType, ctx.Imei, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var stringifiedData = JsonSerializer.Serialize(data);
            var encryptKey = encryptor.GetEncryptKey();

            // In zca-js: ParamsEncryptor.encodeAES(encryptedKey, data, "base64", false)
            // The key is UTF-8 bytes of the encryptKey string
            var encodedData = AesHelper.EncryptAesCbcWithUtf8Key(encryptKey, stringifiedData, "base64");
            var paramsDict = encryptor.GetParams();

            if (paramsDict == null || string.IsNullOrEmpty(encodedData))
                return Task.FromResult<EncryptResult?>(null);

            return Task.FromResult<EncryptResult?>(new EncryptResult
            {
                EncodedData = encodedData,
                EncryptedParams = paramsDict,
                Enk = encryptKey
            });
        }
        catch (Exception ex)
        {
            throw new ZaloApiException($"Failed to encrypt params: {ex.Message}");
        }
    }

    private class EncryptResult
    {
        public string EncodedData { get; set; } = "";
        public Dictionary<string, string> EncryptedParams { get; set; } = new();
        public string Enk { get; set; } = "";
    }

    private class LoginResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("uid")]
        public string? Uid { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("zpw_enk")]
        public string? ZpwEnk { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("zpw_ws")]
        public string[]? ZpwWs { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("send2me_id")]
        public string? Send2MeId { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("language")]
        public string? Language { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("public_ip")]
        public string? PublicIp { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("haspcclient")]
        public int Haspcclient { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("zpw_service_map_v3")]
        public Dictionary<string, string[]>? ZpwServiceMapV3 { get; set; }
    }

    private class ServerInfoResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("setttings")]
        public JsonElement? Setttings { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("settings")]
        public JsonElement? Settings { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("extra_ver")]
        public JsonElement? ExtraVer { get; set; }
    }

    internal class ServerInfoData
    {
        public Dictionary<string, object> Settings { get; set; } = new();
        public string? ExtraVerStr { get; set; }
        public ExtraVerInfo? ExtraVer { get; set; }
    }
}