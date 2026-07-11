using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ICU.Lib.ZaloClientWeb.Exceptions;
using ICU.Lib.ZaloClientWeb.Models;
using ICU.Lib.ZaloClientWeb.Utils;

namespace ICU.Lib.ZaloClientWeb.Auth;

/// <summary>
/// Handles QR code login flow for Zalo.
/// Equivalent to loginQR.ts in zca-js (apis/loginQR.ts).
/// Flow: load page → get version → verify client → generate QR → wait scan → wait confirm → check session → get cookies
/// </summary>
public class QrLoginHelper
{
    private readonly LoginHelper _loginHelper;
    private readonly HttpClient _httpClient;
    private readonly CookieContainer _cookieContainer;
    private readonly ZaloLogger _logger;

    /// <summary>Fired at each stage of the QR login process.</summary>
    public event EventHandler<QrLoginEvent>? OnQrEvent;

    public QrLoginHelper(LoginHelper loginHelper, HttpClient httpClient, CookieContainer cookieContainer)
    {
        _loginHelper = loginHelper;
        _httpClient = httpClient;
        _cookieContainer = cookieContainer;
        _logger = loginHelper.Logger;
    }

    /// <summary>
    /// Performs QR code login and returns credentials for cookie login.
    /// Implements the full Zalo QR login flow matching zca-js loginQR().
    /// </summary>
    /// <param name="userAgent">User-Agent string.</param>
    /// <param name="language">Language code (e.g. "vi").</param>
    /// <param name="qrPath">Path to save the QR code PNG image.</param>
    /// <param name="onQrCodeGenerated">Callback when QR code data URL is generated (for display).</param>
    /// <returns>Credentials for cookie-based login.</returns>
    public async Task<Credentials> LoginWithQrAsync(
        string userAgent,
        string language,
        string? qrPath = null,
        Action<string>? onQrCodeGenerated = null)
    {
        _logger.Info("Starting QR login...");

        using var cts = new CancellationTokenSource();

        try
        {
            // Step 1: Load login page and extract JS version
            _logger.Info("Loading Zalo login page...");
            var loginVersion = await LoadLoginPageAsync(userAgent, language);
            if (string.IsNullOrEmpty(loginVersion))
                throw new ZaloApiException("Cannot get API login version");

            _logger.Info("Got login version:", loginVersion);

            // Step 2: Get login info
            await GetLoginInfoAsync(loginVersion, userAgent, language);

            // Step 3: Verify client (device)
            await VerifyClientAsync(loginVersion, userAgent, language);

            // Step 4: Generate QR code
            var qrResult = await GenerateQrAsync(loginVersion, userAgent, language);
            if (qrResult == null)
                throw new ZaloApiException("Unable to generate QR code");

            var qrCode = qrResult["code"]?.ToString() ?? "";
            var imageBase64 = qrResult["image"]?.ToString()?.Replace("data:image/png;base64,", "") ?? "";
            var token = qrResult["token"]?.ToString() ?? "";

            // Step 5: Save QR code image
            var savePath = qrPath ?? "qr.png";
            if (!string.IsNullOrEmpty(imageBase64))
            {
                var imageBytes = Convert.FromBase64String(imageBase64);
                await File.WriteAllBytesAsync(savePath, imageBytes);
                _logger.Info("Scan the QR code at", $"'{savePath}'", "to proceed with login");
            }

            OnQrEvent?.Invoke(this, new QrLoginEvent
            {
                Type = QrLoginEventType.QrCodeGenerated,
                QrCode = imageBase64,
                Token = token,
                FilePath = savePath
            });

            onQrCodeGenerated?.Invoke(imageBase64);

            // Step 6: Wait for scan (polling with 100s timeout)
            var qrTimeout = Task.Delay(100_000, cts.Token);
            var scanTask = WaitingScanAsync(qrCode, loginVersion, userAgent, language, cts.Token);
            var completed = await Task.WhenAny(scanTask, qrTimeout);

            if (completed == qrTimeout)
            {
                cts.Cancel();
                _logger.Info("QR expired!");

                OnQrEvent?.Invoke(this, new QrLoginEvent
                {
                    Type = QrLoginEventType.QrCodeExpired
                });

                throw new ZaloApiException("QR code expired. Please try again.");
            }

            var scanResult = await scanTask;
            if (scanResult == null)
                throw new ZaloApiException("Cannot get scan result");

            var displayName = scanResult.TryGetValue("display_name", out var dn) ? dn?.ToString() : "";
            var avatar = scanResult.TryGetValue("avatar", out var av) ? av?.ToString() : "";

            OnQrEvent?.Invoke(this, new QrLoginEvent
            {
                Type = QrLoginEventType.QrCodeScanned,
                DisplayName = displayName,
                Avatar = avatar
            });

            // Step 7: Wait for confirm
            var confirmResult = await WaitingConfirmAsync(qrCode, loginVersion, userAgent, language, cts.Token);
            if (confirmResult == null)
                throw new ZaloApiException("Cannot get confirm result");

            if (confirmResult.TryGetValue("error_code", out var errCode) && errCode?.ToString() == "-13")
            {
                OnQrEvent?.Invoke(this, new QrLoginEvent
                {
                    Type = QrLoginEventType.QrCodeDeclined
                });
                throw new ZaloApiLoginQrDeclinedException();
            }

            if (confirmResult.TryGetValue("error_code", out var ec) && ec?.ToString() != "0")
            {
                var errMsg = confirmResult.TryGetValue("error_message", out var em) ? em?.ToString() : "Unknown error";
                throw new ZaloApiException($"QR login error: {errMsg}");
            }

            // Step 8: Check session
            var sessionResult = await CheckSessionAsync(userAgent, language);
            if (sessionResult == null)
                throw new ZaloApiException("Cannot get session, login failed");

            _logger.Info("Successfully logged in via QR code");

            // Step 9: Get user info
            var userInfo = await GetUserInfoAsync(userAgent, language);
            if (userInfo == null)
                throw new ZaloApiException("Can't get account info");

            if (userInfo.TryGetValue("logged", out var logged) && logged?.ToString() == "False")
                throw new ZaloApiException("Can't login");

            // Extract cookies from container for Zalo domains
            var cookieItems = new List<CookieItem>();
            var domains = new[] { "chat.zalo.me", "id.zalo.me", "zalo.me", ".zalo.me" };
            foreach (var domain in domains)
            {
                var uri = new Uri($"https://{domain.TrimStart('.')}");
                var cookies = _cookieContainer.GetCookies(uri);
                foreach (Cookie cookie in cookies)
                {
                    if (!string.IsNullOrEmpty(cookie.Name))
                    {
                        cookieItems.Add(new CookieItem
                        {
                            Name = cookie.Name,
                            Value = cookie.Value,
                            Domain = cookie.Domain,
                            Path = cookie.Path,
                            Secure = cookie.Secure,
                            HttpOnly = cookie.HttpOnly,
                            ExpirationDate = cookie.Expires == DateTime.MinValue
                                ? 0 : new DateTimeOffset(cookie.Expires, TimeSpan.Zero).ToUnixTimeSeconds()
                        });
                    }
                }
            }

            var imei = ZaloUtils.GenerateZaloUuid(userAgent);

            return new Credentials
            {
                Imei = imei,
                Cookie = cookieItems,
                UserAgent = userAgent,
                Language = language
            };
        }
        catch (OperationCanceledException)
        {
            throw new ZaloApiException("QR login was cancelled.");
        }
    }

    private async Task<string?> LoadLoginPageAsync(string userAgent, string language)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            "https://id.zalo.me/account?continue=https%3A%2F%2Fchat.zalo.me%2F");

        request.Headers.TryAddWithoutValidation("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
        request.Headers.TryAddWithoutValidation("accept-language", $"{language}-VN,{language};q=0.9");
        request.Headers.TryAddWithoutValidation("user-agent", userAgent);
        request.Headers.TryAddWithoutValidation("origin", "https://chat.zalo.me");
        request.Headers.TryAddWithoutValidation("referer", "https://chat.zalo.me/");

        var response = await _httpClient.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        var regex = new Regex(@"https:\/\/stc-zlogin\.zdn\.vn\/main-([\d.]+)\.js");
        var match = regex.Match(html);
        return match.Success ? match.Groups[1].Value : null;
    }

    private async Task GetLoginInfoAsync(string version, string userAgent, string language)
    {
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("continue", "https://zalo.me/pc"),
            new KeyValuePair<string, string>("v", version)
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "https://id.zalo.me/account/logininfo")
        {
            Content = form
        };

        request.Headers.TryAddWithoutValidation("accept", "*/*");
        request.Headers.TryAddWithoutValidation("accept-language", $"{language}-VN,{language};q=0.9");
        request.Headers.TryAddWithoutValidation("user-agent", userAgent);
        request.Headers.TryAddWithoutValidation("referer", "https://id.zalo.me/account?continue=https%3A%2F%2Fzalo.me%2Fpc");
        request.Headers.TryAddWithoutValidation("origin", "https://id.zalo.me");

        await _httpClient.SendAsync(request);
    }

    private async Task VerifyClientAsync(string version, string userAgent, string language)
    {
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("type", "device"),
            new KeyValuePair<string, string>("continue", "https://zalo.me/pc"),
            new KeyValuePair<string, string>("v", version)
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "https://id.zalo.me/account/verify-client")
        {
            Content = form
        };

        request.Headers.TryAddWithoutValidation("accept", "*/*");
        request.Headers.TryAddWithoutValidation("accept-language", $"{language}-VN,{language};q=0.9");
        request.Headers.TryAddWithoutValidation("user-agent", userAgent);
        request.Headers.TryAddWithoutValidation("referer", "https://id.zalo.me/account?continue=https%3A%2F%2Fzalo.me%2Fpc");
        request.Headers.TryAddWithoutValidation("origin", "https://id.zalo.me");

        await _httpClient.SendAsync(request);
    }

    private async Task<Dictionary<string, object?>?> GenerateQrAsync(string version, string userAgent, string language)
    {
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("continue", "https://zalo.me/pc"),
            new KeyValuePair<string, string>("v", version)
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "https://id.zalo.me/account/authen/qr/generate")
        {
            Content = form
        };

        request.Headers.TryAddWithoutValidation("accept", "*/*");
        request.Headers.TryAddWithoutValidation("accept-language", $"{language}-VN,{language};q=0.9");
        request.Headers.TryAddWithoutValidation("user-agent", userAgent);
        request.Headers.TryAddWithoutValidation("referer", "https://id.zalo.me/account?continue=https%3A%2F%2Fzalo.me%2Fpc");
        request.Headers.TryAddWithoutValidation("origin", "https://id.zalo.me");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(content);

        if (dict == null || (dict.TryGetValue("error_code", out var ec) && ec.GetInt32() != 0))
            return null;

        if (!dict.TryGetValue("data", out var dataEl) || dataEl.ValueKind != System.Text.Json.JsonValueKind.Object)
            return null;

        var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(dataEl.GetRawText());
        return data;
    }

    private async Task<Dictionary<string, object?>?> WaitingScanAsync(string code, string version, string userAgent, string language, CancellationToken ct)
    {
        try
        {
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("continue", "https://chat.zalo.me/"),
                new KeyValuePair<string, string>("v", version)
            });

            var request = new HttpRequestMessage(HttpMethod.Post, "https://id.zalo.me/account/authen/qr/waiting-scan")
            {
                Content = form
            };

            request.Headers.TryAddWithoutValidation("accept", "*/*");
            request.Headers.TryAddWithoutValidation("accept-language", $"{language}-VN,{language};q=0.9");
            request.Headers.TryAddWithoutValidation("user-agent", userAgent);
            request.Headers.TryAddWithoutValidation("referer", "https://id.zalo.me/account?continue=https%3A%2F%2Fchat.zalo.me%2F");
            request.Headers.TryAddWithoutValidation("origin", "https://id.zalo.me");

            var response = await _httpClient.SendAsync(request, ct);
            var content = await response.Content.ReadAsStringAsync();
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(content);

            if (dict == null) return null;
            if (dict.TryGetValue("error_code", out var ec) && ec.GetInt32() == 8)
                return await WaitingScanAsync(code, version, userAgent, language, ct);

            if (!dict.TryGetValue("data", out var dataEl) || dataEl.ValueKind != System.Text.Json.JsonValueKind.Object)
                return null;

            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(dataEl.GetRawText());
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return null;
        }
    }

    private async Task<Dictionary<string, object?>?> WaitingConfirmAsync(string code, string version, string userAgent, string language, CancellationToken ct)
    {
        try
        {
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("gToken", ""),
                new KeyValuePair<string, string>("gAction", "CONFIRM_QR"),
                new KeyValuePair<string, string>("continue", "https://chat.zalo.me/"),
                new KeyValuePair<string, string>("v", version)
            });

            _logger.Info("Please confirm on your phone");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://id.zalo.me/account/authen/qr/waiting-confirm")
            {
                Content = form
            };

            request.Headers.TryAddWithoutValidation("accept", "*/*");
            request.Headers.TryAddWithoutValidation("accept-language", $"{language}-VN,{language};q=0.9");
            request.Headers.TryAddWithoutValidation("user-agent", userAgent);
            request.Headers.TryAddWithoutValidation("referer", "https://id.zalo.me/account?continue=https%3A%2F%2Fchat.zalo.me%2F");
            request.Headers.TryAddWithoutValidation("origin", "https://id.zalo.me");

            var response = await _httpClient.SendAsync(request, ct);
            var content = await response.Content.ReadAsStringAsync();

            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(content);
            if (dict == null) return null;
            if (dict.TryGetValue("error_code", out var ec) && ec.GetInt32() == 8)
                return await WaitingConfirmAsync(code, version, userAgent, language, ct);

            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(content);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return null;
        }
    }

    private async Task<Dictionary<string, object?>?> CheckSessionAsync(string userAgent, string language)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                "https://id.zalo.me/account/checksession?continue=https%3A%2F%2Fchat.zalo.me%2Findex.html");

            request.Headers.TryAddWithoutValidation("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            request.Headers.TryAddWithoutValidation("accept-language", $"{language}-VN,{language};q=0.9");
            request.Headers.TryAddWithoutValidation("user-agent", userAgent);
            request.Headers.TryAddWithoutValidation("referer", "https://id.zalo.me/account?continue=https%3A%2F%2Fchat.zalo.me%2F");

            var response = await _httpClient.SendAsync(request);
            return new Dictionary<string, object?> { ["status"] = (int)response.StatusCode };
        }
        catch
        {
            return null;
        }
    }

    private async Task<Dictionary<string, object?>?> GetUserInfoAsync(string userAgent, string language)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                "https://jr.chat.zalo.me/jr/userinfo");

            request.Headers.TryAddWithoutValidation("accept", "*/*");
            request.Headers.TryAddWithoutValidation("accept-language", $"{language}-VN,{language};q=0.9");
            request.Headers.TryAddWithoutValidation("user-agent", userAgent);
            request.Headers.TryAddWithoutValidation("referer", "https://chat.zalo.me/");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(content);

            if (dict == null) return null;
            if (!dict.TryGetValue("data", out var dataEl) || dataEl.ValueKind != System.Text.Json.JsonValueKind.Object)
                return null;

            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(dataEl.GetRawText());
        }
        catch
        {
            return null;
        }
    }
}