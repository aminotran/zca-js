using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ICU.Lib.ZaloClientWeb.Crypto;
using ICU.Lib.ZaloClientWeb.Exceptions;
using ICU.Lib.ZaloClientWeb.Models;
using ICU.Lib.ZaloClientWeb.Utils;

namespace ICU.Lib.ZaloClientWeb.Auth;

/// <summary>
/// Handles cookie-based login to Zalo API.
/// Equivalent to login.ts in zca-js (apis/login.ts).
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
    /// Equivalent to login() in zca-js.
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

        // Apply cookies to container
        _client.ApplyCookies(credentials.Cookie);

        // Simulate the login requests
        var loginData = await PerformLoginAsync(ctx, credentials);
        if (loginData == null)
            throw new ZaloApiException("Login failed");

        var serverInfo = await GetServerInfoAsync(ctx, credentials);
        if (serverInfo == null)
            throw new ZaloApiException("Failed to get server info");

        ctx.SecretKey = loginData.ZpwEnk;
        ctx.Uid = loginData.Uid;
        ctx.Settings = serverInfo.Settings ?? new Dictionary<string, object>();
        ctx.ExtraVer = serverInfo.ExtraVer;
        ctx.LoginInfo = loginData;
        ctx.ZpwServiceMapV3 = loginData.ZpwServiceMapV3;
        ctx.ZpwWs = loginData.ZpwWs;
        ctx.ZpwWsUrls = loginData.ZpwServiceMapV3.ContainsKey("chat") 
            ? loginData.ZpwServiceMapV3["chat"] 
            : Array.Empty<string>();

        if (string.IsNullOrEmpty(ctx.SecretKey))
            throw new ZaloApiException("Context initialization failed - no secret key");

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

    private async Task<LoginInfo?> PerformLoginAsync(ZaloContext ctx, Credentials credentials)
    {
        // TODO: Implement actual Zalo login API call
        // This is a stub - the actual implementation would:
        // 1. Build the login request URL with encrypted params
        // 2. Send POST request to Zalo login endpoint
        // 3. Parse and decrypt the response
        // 4. Return LoginInfo

        Logger.Info("Performing login...");
        
        // Placeholder for actual login request
        var paramsEncryptor = new ParamsEncryptor(_options.ApiType, credentials.Imei, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var encryptParams = paramsEncryptor.GetParams();

        // Build the URL
        var url = ZaloUtils.MakeUrl("https://wpa.chat.zalo.me/api/login", 
            new Dictionary<string, string>
            {
                ["imei"] = credentials.Imei,
                ["computerName"] = "Unknown",
                ["params"] = System.Text.Json.JsonSerializer.Serialize(encryptParams)
            },
            _options.ApiVersion, _options.ApiType);

        // The actual implementation would make the HTTP request here
        // and decrypt the response using AesHelper.DecryptResponse()

        // For now, we'll simulate a successful login
        await Task.Delay(100);

        return new LoginInfo
        {
            Uid = 0, // Will be populated from actual API
            ZpwEnk = null, // Will be populated from actual API
            ZpwWs = null
        };
    }

    private async Task<ServerInfo?> GetServerInfoAsync(ZaloContext ctx, Credentials credentials)
    {
        // TODO: Implement actual server info API call
        Logger.Info("Getting server info...");
        await Task.Delay(100);
        return new ServerInfo();
    }

    internal class ServerInfo
    {
        public Dictionary<string, object>? Settings { get; set; } = new();
        public string? ExtraVer { get; set; }
    }
}