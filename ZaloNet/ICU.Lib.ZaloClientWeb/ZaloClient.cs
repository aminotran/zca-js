using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using ICU.Lib.ZaloClientWeb.Auth;
using ICU.Lib.ZaloClientWeb.Crypto;
using ICU.Lib.ZaloClientWeb.Exceptions;
using ICU.Lib.ZaloClientWeb.Models;
using ICU.Lib.ZaloClientWeb.Models.Types;
using ICU.Lib.ZaloClientWeb.Utils;

namespace ICU.Lib.ZaloClientWeb;

public class ZaloClient : IDisposable
{
    private readonly ZaloOptions _options;
    private HttpClient _httpClient;
    private CookieContainer _cookieContainer;
    private ZaloContext? _context;
    private bool _disposed;

    public ZaloContext? Context => _context;
    public ZaloApi? Api { get; private set; }
    public ZaloLogger Logger { get; }

    public ZaloClient(ZaloOptions? options = null)
    {
        _options = options ?? new ZaloOptions();
        _cookieContainer = new CookieContainer();
        Logger = new ZaloLogger(_options.Logging);

        var handler = new HttpClientHandler
        {
            CookieContainer = _cookieContainer,
            UseCookies = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            AllowAutoRedirect = true,
        };

        if (_options.Proxy != null)
        {
            handler.Proxy = _options.Proxy;
            handler.UseProxy = true;
        }

        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br, zstd");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        _httpClient.DefaultRequestHeaders.Add("Origin", "https://chat.zalo.me");
        _httpClient.DefaultRequestHeaders.Add("Referer", "https://chat.zalo.me/");
    }

    public async Task<ZaloApi> LoginAsync(Credentials credentials)
    {
        var loginHelper = new LoginHelper(this, _options, _httpClient, _cookieContainer);
        _context = await loginHelper.LoginAsync(credentials);

        if (_context == null)
            throw new ZaloApiException("Login failed - context could not be created");

        _context.CookieContainer = _cookieContainer;

        Logger.Info("Logged in as", _context.Uid.ToString());
        Api = new ZaloApi(_context, _httpClient);
        return Api;
    }

    public async Task<ZaloApi> LoginWithQrAsync(
        string? qrPath = null,
        string? userAgent = null,
        string? language = null,
        Action<string>? onQrCodeGenerated = null)
    {
        userAgent ??= "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:133.0) Gecko/20100101 Firefox/133.0";
        language ??= "vi";

        var loginHelper = new LoginHelper(this, _options, _httpClient, _cookieContainer);
        var qrLoginHelper = new QrLoginHelper(loginHelper, _httpClient, _cookieContainer);
        var credentials = await qrLoginHelper.LoginWithQrAsync(userAgent, language, qrPath, onQrCodeGenerated);

        return await LoginAsync(credentials);
    }

    /// <summary>
    /// Exports current session cookies as a list of CookieItem.
    /// Useful for persisting updated credentials (e.g., after Set-Cookie refresh).
    /// </summary>
    public List<CookieItem> GetCookies()
    {
        var cookies = new List<CookieItem>();
        try
        {
            // Extract cookies from all known Zalo domains
            var domains = new[]
            {
                "chat.zalo.me", "zalo.me", "wpa.chat.zalo.me",
                "id.zalo.me", "tt-profile-wpa.zalo.me",
                "tt-friend-wpa.zalo.me", "tt-group-wpa.zalo.me",
                "tt-sticker-wpa.zalo.me", "tt-chat-wpa.zalo.me",
                "tt-convers-wpa.zalo.me", "tt-alias-wpa.zalo.me",
            };
            foreach (var domain in domains)
            {
                var uri = new Uri($"https://{domain}");
                var domainCookies = _cookieContainer.GetCookies(uri);
                foreach (Cookie c in domainCookies)
                {
                    // Avoid duplicates
                    if (cookies.Any(ex => ex.Name == c.Name && ex.Domain == c.Domain))
                        continue;

                    cookies.Add(new CookieItem
                    {
                        Name = c.Name,
                        Value = c.Value,
                        Domain = c.Domain,
                        Path = c.Path,
                        Secure = c.Secure,
                        HttpOnly = c.HttpOnly,
                        ExpirationDate = new DateTimeOffset(c.Expires, TimeSpan.Zero).ToUnixTimeSeconds(),
                        SameSite = "unspecified",
                        Session = c.Expires == DateTime.MinValue,
                    });
                }
            }
        }
        catch { /* best-effort */ }
        return cookies;
    }

    /// <summary>
    /// Applies a list of cookies to the cookie container.
    /// </summary>
    public void ApplyCookies(List<CookieItem> cookies)
    {
        foreach (var cookie in cookies)
        {
            try
            {
                var domain = cookie.Domain;
                if (string.IsNullOrEmpty(domain)) continue;

                var rawDomain = domain.StartsWith(".") ? domain.Substring(1) : domain;
                var uri = new Uri($"https://{rawDomain}");
                var netCookie = new Cookie(cookie.Name, cookie.Value, cookie.Path, rawDomain);
                netCookie.Secure = cookie.Secure;
                netCookie.HttpOnly = cookie.HttpOnly;

                _cookieContainer.Add(uri, netCookie);

                if (domain.StartsWith("."))
                {
                    var subdomains = new[] {
                        $"chat.{rawDomain}", $"id.{rawDomain}", $"wpa.{rawDomain}",
                        $"tt-profile-wpa.{rawDomain}", $"tt-friend-wpa.{rawDomain}",
                        $"tt-group-wpa.{rawDomain}", $"tt-sticker-wpa.{rawDomain}",
                        $"tt-chat-wpa.{rawDomain}", $"tt-convers-wpa.{rawDomain}",
                        $"tt-alias-wpa.{rawDomain}",
                    };
                    foreach (var sub in subdomains)
                    {
                        try { _cookieContainer.Add(new Uri($"https://{sub}"), netCookie); } catch { }
                    }
                }
            }
            catch
            {
                Logger.Warn("Failed to set cookie:", cookie.Name);
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}

public class ZaloApi
{
    private readonly ZaloContext _context;
    private readonly HttpClient _httpClient;
    private readonly ZaloApiClient _apiClient;
    internal ZaloContext Context => _context;
    internal HttpClient HttpClient => _httpClient;

    internal WebSocket.ZaloListener? _listener;

    private ZaloApiResponse<JsonElement>? _conversationCache;
    private DateTime _conversationCacheTime;

    public ZaloApi(ZaloContext context, HttpClient httpClient)
    {
        _context = context;
        _httpClient = httpClient;
        _apiClient = new ZaloApiClient(context, httpClient);
    }

    /// <summary>
    /// Initializes a new instance with a pre-configured <see cref="ZaloApiClient"/> for DI support.
    /// </summary>
    internal ZaloApi(ZaloApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _context = apiClient.Context;
        _httpClient = apiClient.HttpClient;
    }

    /// <summary>
    /// Gets the underlying <see cref="ZaloApiClient"/> for direct API access.
    /// </summary>
    public ZaloApiClient ApiClient => _apiClient;

    public WebSocket.ZaloListener Listener
    {
        get
        {
            if (_listener == null)
                _listener = new WebSocket.ZaloListener(_context, _httpClient);
            return _listener;
        }
    }

    private long GetTimestamp() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private string GetImei() => _context.Imei;

    // ─── Profile APIs ────────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> GetUserInfoAsync(long userId) => _apiClient.CallGetApiAsync("getUserInfo", new { userId });
    public Task<ZaloApiResponse<JsonElement>> FindUserAsync(string phoneNumber) => _apiClient.CallGetApiAsync("findUser", new { phoneNumber });
    public Task<ZaloApiResponse<JsonElement>> FindUserByUsernameAsync(string username) => _apiClient.CallGetApiAsync("findUserByUsername", new { username });
    public Task<ZaloApiResponse<JsonElement>> GetAccountInfoAsync() => _apiClient.CallGetApiAsync("fetchAccountInfo");
    public long GetOwnId() => _context.Uid;
    public Task<ZaloApiResponse<long>> GetOwnIdAsync() => Task.FromResult(new ZaloApiResponse<long> { Data = _context.Uid });
    public Task<ZaloApiResponse<JsonElement>> UpdateProfileAsync(object profileData) => _apiClient.CallPostApiAsync("updateProfile", profileData);
    public Task<ZaloApiResponse<JsonElement>> UpdateProfileBioAsync(string bio) => _apiClient.CallPostApiAsync("updateProfileBio", new { bio });
    public Task<ZaloApiResponse<JsonElement>> ChangeAccountAvatarAsync(string imagePath) => _apiClient.CallPostApiAsync("changeAccountAvatar", new { imagePath });
    public Task<ZaloApiResponse<JsonElement>> GetAvatarListAsync() => _apiClient.CallGetApiAsync("getAvatarList");
    public Task<ZaloApiResponse<JsonElement>> GetFullAvatarAsync() => _apiClient.CallGetApiAsync("getFullAvatar");
    public Task<ZaloApiResponse<JsonElement>> DeleteAvatarAsync(long avatarId) => _apiClient.CallPostApiAsync("deleteAvatar", new { avatarId });
    public Task<ZaloApiResponse<JsonElement>> ReuseAvatarAsync(long avatarId) => _apiClient.CallPostApiAsync("reuseAvatar", new { avatarId });
    public Task<ZaloApiResponse<JsonElement>> GetAvatarUrlProfileAsync(long userId) => _apiClient.CallGetApiAsync("getAvatarUrlProfile", new { userId });

    // ─── Friend APIs (all encrypted POST) ─────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> GetAllFriendsAsync() => _apiClient.CallEncryptedGetApiAsync("getAllFriends", new { incInvalid = 1, page = 1, count = 20000, avatar_size = 120, actiontime = 0, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> GetFriendRequestStatusAsync(long friendId) => _apiClient.CallEncryptedGetApiAsync("getFriendRequestStatus", new { fid = friendId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> SendFriendRequestAsync(long userId, string? message = null) => _apiClient.CallEncryptedPostApiAsync("sendFriendRequest", new { userId, imei = GetImei(), msg = message ?? "" });
    public Task<ZaloApiResponse<JsonElement>> AcceptFriendRequestAsync(long userId) => _apiClient.CallEncryptedPostApiAsync("acceptFriendRequest", new { fid = userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> RejectFriendRequestAsync(long userId) => _apiClient.CallEncryptedPostApiAsync("rejectFriendRequest", new { fid = userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> RemoveFriendAsync(long userId) => _apiClient.CallEncryptedPostApiAsync("removeFriend", new { fid = userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> UndoFriendRequestAsync(long userId) => _apiClient.CallEncryptedPostApiAsync("undoFriendRequest", new { fid = userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> BlockUserAsync(long userId) => _apiClient.CallEncryptedPostApiAsync("blockUser", new { fid = userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> UnblockUserAsync(long userId) => _apiClient.CallEncryptedPostApiAsync("unblockUser", new { fid = userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> BlockViewFeedAsync(long userId) => _apiClient.CallEncryptedPostApiAsync("blockViewFeed", new { fid = userId, imei = GetImei(), blockType = 1 });
    public Task<ZaloApiResponse<JsonElement>> GetFriendBoardListAsync(long userId) => _apiClient.CallGetApiAsync("getFriendBoardList", new { userId });
    public Task<ZaloApiResponse<JsonElement>> GetFriendOnlinesAsync() => _apiClient.CallGetApiAsync("getFriendOnlines");
    public Task<ZaloApiResponse<JsonElement>> GetFriendRecommendationsAsync() => _apiClient.CallGetApiAsync("getFriendRecommendations");
    public Task<ZaloApiResponse<JsonElement>> ChangeFriendAliasAsync(long userId, string alias) => _apiClient.CallEncryptedPostApiAsync("changeFriendAlias", new { userId, alias, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> RemoveFriendAliasAsync(long userId) => _apiClient.CallEncryptedPostApiAsync("removeFriendAlias", new { userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> GetSentFriendRequestAsync() => _apiClient.CallGetApiAsync("getSentFriendRequest");
    public Task<ZaloApiResponse<JsonElement>> GetCloseFriendsAsync() => _apiClient.CallGetApiAsync("getCloseFriends");
    public Task<ZaloApiResponse<JsonElement>> GetAliasListAsync() => _apiClient.CallGetApiAsync("getAliasList");
    public Task<ZaloApiResponse<JsonElement>> GetRelatedFriendGroupAsync() => _apiClient.CallGetApiAsync("getRelatedFriendGroup");
    public Task<ZaloApiResponse<JsonElement>> GetMultiUsersByPhonesAsync(List<string> phones) => _apiClient.CallPostApiAsync("getMultiUsersByPhones", new { phones });
    public Task<ZaloApiResponse<JsonElement>> InviteUserToGroupsAsync(long userId, List<string> groupIds) => _apiClient.CallPostApiAsync("inviteUserToGroups", new { userId, groupIds });

    // ─── Message APIs (all encrypted POST) ────────────────────────────────
    public async Task<ZaloApiResponse<JsonElement>> SendMessageAsync(MessageContent message, string threadId, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        var isGroup = threadType == ThreadType.Group;
        var hasAttachments = message.Attachments?.Count > 0;

        // Handle mentions
        string? mentionInfo = null;
        if (message.Mentions?.Count > 0 && isGroup)
        {
            var mentionsFinal = message.Mentions
                .Where(m => m.Pos >= 0 && !string.IsNullOrEmpty(m.Uid) && m.Len > 0)
                .Select(m => new
                {
                    pos = m.Pos,
                    uid = m.Uid,
                    len = m.Len,
                    type = m.Uid == "-1" ? 1 : 0
                })
                .ToList();

            var totalMentionLen = mentionsFinal.Sum(m => m.len);
            if (totalMentionLen > message.Msg.Length)
                throw new InvalidOperationException("Invalid mentions: total mention characters exceed message length");

            mentionInfo = JsonSerializer.Serialize(mentionsFinal, _jsonOptions);
        }

        // ─── If has attachments, upload + send in one call ─────────────
        if (hasAttachments)
        {
            var msgText = message.Msg ?? "";
            var hasQuote = message.Quote != null;

            // Determine if text should be sent separately:
            // TS: if (non-image single file AND msg has text + no quote) → send text as desc with attachment
            // else → send text message first, then attachments
            var firstExt = GetFirstAttachmentExtension(message.Attachments!);
            var isSingleFile = message.Attachments!.Count == 1;
            var canBeDesc = isSingleFile && firstExt is "jpg" or "jpeg" or "png" or "webp" or "gif";

            ZaloApiResponse<JsonElement>? textResult = null;

            // If NOT canBeDesc and there's text, send text message separately first
            if ((!canBeDesc && msgText.Length > 0) || (msgText.Length > 0 && hasQuote))
            {
                var textParams = new Dictionary<string, object?>
                {
                    ["message"] = msgText,
                    ["clientId"] = GetTimestamp(),
                    ["ttl"] = message.Ttl ?? 0,
                    [isGroup ? "grid" : "toid"] = threadId,
                    [isGroup ? "visibility" : "imei"] = isGroup ? (object?)0 : GetImei(),
                };

                if (mentionInfo != null)
                    textParams["mentionInfo"] = mentionInfo;

                if (message.Styles?.Count > 0)
                {
                    textParams["textProperties"] = JsonSerializer.Serialize(new
                    {
                        styles = message.Styles.Select(s => new { s.Start, s.Len, s.St }).ToList(),
                        ver = 0
                    }, _jsonOptions);
                }

                if (message.Urgency.HasValue && message.Urgency.Value > 0)
                    textParams["metaData"] = new Dictionary<string, object?> { ["urgency"] = (int)message.Urgency.Value };

                if (hasQuote)
                {
                    AddQuoteParams(textParams, message.Quote!, isGroup);
                    var qEndpoint = isGroup ? "sendMessageGroupQuote" : "sendMessageQuote";
                    textResult = await _apiClient.CallEncryptedPostApiAsync(qEndpoint, textParams);
                }
                else
                {
                    var textEndpoint = isGroup ? (mentionInfo != null ? "sendMessageGroupMention" : "sendMessageGroup") : "sendMessage";
                    textResult = await _apiClient.CallEncryptedPostApiAsync(textEndpoint, textParams);
                }
            }

            // Upload attachments
            var uploadSources = message.Attachments!.Select(a => a).ToArray();
            var uploadResults = await UploadAttachmentAsync(uploadSources, threadId, threadType);

            // Send each attachment
            var attachmentResults = new List<ZaloApiResponse<JsonElement>>();
            foreach (var upload in uploadResults)
            {
                var attachResult = await SendAttachmentMessageAsync(
                    upload, threadId,
                    canBeDesc ? msgText : null,
                    threadType);
                attachmentResults.Add(attachResult);
            }

            // Build combined result
            var combinedData = new Dictionary<string, object?>
            {
                ["textResult"] = textResult?.Data,
                ["attachmentResults"] = attachmentResults.Select(r => (object?)r.Data).ToList(),
                ["isSuccess"] = attachmentResults.All(r => r.IsSuccess)
            };

            if (attachmentResults.All(r => r.IsSuccess))
                return new ZaloApiResponse<JsonElement> { Data = JsonSerializer.SerializeToElement(combinedData, _jsonOptions) };
            else
                return new ZaloApiResponse<JsonElement> { Error = "Attachment sending failed", Data = JsonSerializer.SerializeToElement(combinedData, _jsonOptions) };
        }

        // ─── No attachments: original behavior ────────────────────────
        // Build params
        var paramsDict = new Dictionary<string, object?>
        {
            ["message"] = message.Msg,
            ["clientId"] = ts,
            ["ttl"] = message.Ttl ?? 0,
            [isGroup ? "grid" : "toid"] = threadId,
            [isGroup ? "visibility" : "imei"] = isGroup ? (object?)0 : GetImei(),
        };

        // Add mentionInfo for group
        if (mentionInfo != null)
            paramsDict["mentionInfo"] = mentionInfo;

        // Add styles (textProperties)
        if (message.Styles?.Count > 0)
        {
            var stylesFinal = message.Styles.Select(s => new
            {
                start = s.Start,
                len = s.Len,
                st = s.St
            }).ToList();

            paramsDict["textProperties"] = JsonSerializer.Serialize(new
            {
                styles = stylesFinal,
                ver = 0
            }, _jsonOptions);
        }

        // Add urgency
        if (message.Urgency.HasValue && message.Urgency.Value > 0)
        {
            paramsDict["metaData"] = new Dictionary<string, object?>
            {
                ["urgency"] = (int)message.Urgency.Value
            };
        }

        // Add quote
        if (message.Quote != null)
        {
            AddQuoteParams(paramsDict, message.Quote, isGroup);
            if (isGroup)
                return await _apiClient.CallEncryptedPostApiAsync("sendMessageGroupQuote", paramsDict);
            else
                return await _apiClient.CallEncryptedPostApiAsync("sendMessageQuote", paramsDict);
        }

        // Select endpoint
        string endpoint;
        if (isGroup)
            endpoint = mentionInfo != null ? "sendMessageGroupMention" : "sendMessageGroup";
        else
            endpoint = "sendMessage";

        return await _apiClient.CallEncryptedPostApiAsync(endpoint, paramsDict);
    }

    private void AddQuoteParams(Dictionary<string, object?> dict, SendMessageQuote quote, bool isGroup)
    {
        dict["qmsgOwner"] = quote.UidFrom;
        dict["qmsgId"] = quote.MsgId;
        dict["qmsgCliId"] = quote.CliMsgId;
        dict["qmsgType"] = ZaloUtils.GetClientMessageType(quote.MsgType);
        dict["qmsgTs"] = quote.Ts;
        dict["qmsg"] = quote.Content is string s ? s : ZaloUtils.GetClientMessageType(quote.MsgType).ToString();
        dict["qmsgTTL"] = quote.Ttl ?? 0;

        if (isGroup && quote.Content != null && quote.Content is not string)
        {
            dict["qmsgAttach"] = JsonSerializer.Serialize(PrepareQmsgAttach(quote), _jsonOptions);
        }
    }

    private static string? GetFirstAttachmentExtension(List<object> attachments)
    {
        if (attachments.Count == 0) return null;
        var first = attachments[0];
        if (first is string path)
            return Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return null;
    }

    /// <summary>
    /// Simple SendMessage overload for plain text.
    /// </summary>
    public Task<ZaloApiResponse<JsonElement>> SendMessageAsync(string threadId, string message, ThreadType threadType = ThreadType.User)
        => SendMessageAsync((MessageContent)message, threadId, threadType);

    private static object PrepareQmsgAttach(SendMessageQuote quote)
    {
        if (quote.Content is string) return quote.PropertyExt ?? new { };
        if (quote.MsgType == "chat.todo")
            return new
            {
                properties = new
                {
                    color = 0,
                    size = 0,
                    type = 0,
                    subType = 0,
                    ext = "{\"shouldParseLinkOrContact\":0}"
                }
            };

        return new
        {
            thumbUrl = quote.AttachmentData ?? "",
            oriUrl = quote.AttachmentData ?? "",
            normalUrl = quote.AttachmentData ?? ""
        };
    }

    public Task<ZaloApiResponse<JsonElement>> SendStickerAsync(string threadId, int stickerId, int stickerCategoryId, int type = 1, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        if (threadType == ThreadType.Group)
            return _apiClient.CallEncryptedPostApiAsync("sendStickerGroup",
                new { stickerId, cateId = stickerCategoryId, type, clientId = ts, ttl = 0, zsource = 101, grid = threadId, visibility = 0 });
        else
            return _apiClient.CallEncryptedPostApiAsync("sendSticker",
                new { stickerId, cateId = stickerCategoryId, type, clientId = ts, ttl = 0, zsource = 101, toid = threadId, imei = GetImei() });
    }

    public async Task<ZaloApiResponse<JsonElement>> SendLinkAsync(string threadId, string link, string? msg = null, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        var parseResult = await ParseLinkAsync(link);
        var href = link;
        var src = "";
        var title = "";
        var desc = "";
        var thumb = "";
        var media = "[]";

        if (parseResult.IsSuccess)
        {
            // parseLink response: { data: { href,src,title,desc,thumb,media,... }, error_maps:{} }
            // CallEncryptedGetApiAsync already unwraps the outer data field
            var data = parseResult.Data;
            // The actual link data is nested under "data" property
            if (data.TryGetProperty("data", out var linkData))
                data = linkData;

            href = TryGetString(data, "href") ?? link;
            src = TryGetString(data, "src") ?? "";
            title = TryGetString(data, "title") ?? "";
            desc = TryGetString(data, "desc") ?? "";
            thumb = TryGetString(data, "thumb") ?? "";
            // TS: media = JSON.stringify(res.data.media) — media is an object, not a string
            if (data.TryGetProperty("media", out var mediaEl) && mediaEl.ValueKind == JsonValueKind.Object)
                media = JsonSerializer.Serialize(mediaEl, _jsonOptions);
            else if (data.TryGetProperty("media", out var mediaStr) && mediaStr.ValueKind == JsonValueKind.String)
                media = mediaStr.GetString() ?? "[]";
        }

        var finalMsg = !string.IsNullOrEmpty(msg) ? (msg!.Contains(link) ? msg : msg + " " + link) : link;
        if (threadType == ThreadType.Group)
            return await _apiClient.CallEncryptedPostApiAsync("sendLinkGroup",
                new { msg = finalMsg, href, src, title, desc, thumb, type = 2, media, ttl = 0, clientId = ts, grid = threadId, imei = GetImei() });
        else
        {
            var sendParams = new { msg = finalMsg, href, src, title, desc, thumb, type = 2, media, ttl = 0, clientId = ts, toId = threadId, mentionInfo = "" };
            Console.Error.WriteLine($"[SENDLINK] raw JSON: {System.Text.Json.JsonSerializer.Serialize(sendParams, _jsonOptions)}");
            Console.Error.WriteLine($"[SENDLINK] href={href} src={src} title={title} desc={desc} thumb={thumb} media={media}");
            return await _apiClient.CallEncryptedPostApiAsync("sendLink", sendParams);
        }
    }

    private static string? TryGetString(JsonElement el, string key)
    {
        return el.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
    }

    public async Task<ZaloApiResponse<JsonElement>> SendVideoAsync(string threadId, string videoUrl, string thumbnailUrl, string? msg = null, int duration = 0, int width = 1280, int height = 720, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        // TS does HEAD request to get content-length for fileSize
        int fileSize = 0;
        try
        {
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, videoUrl);
            var headResponse = await _httpClient.SendAsync(headRequest);
            if (headResponse.IsSuccessStatusCode && headResponse.Content.Headers.ContentLength.HasValue)
                fileSize = (int)headResponse.Content.Headers.ContentLength.Value;
        }
        catch { /* best-effort */ }

        var msgInfo = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["videoUrl"] = videoUrl,
            ["thumbUrl"] = thumbnailUrl,
            ["duration"] = duration,
            ["width"] = width,
            ["height"] = height,
            ["fileSize"] = fileSize,
            ["properties"] = new Dictionary<string, object?>
            {
                ["color"] = -1, ["size"] = -1, ["type"] = 1003, ["subType"] = 0,
                ["ext"] = new Dictionary<string, object?> { ["sSrcType"] = -1, ["sSrcStr"] = "", ["msg_warning_type"] = 0 }
            },
            ["title"] = msg ?? ""
        }, _jsonOptions);

        if (threadType == ThreadType.Group)
            return await _apiClient.CallEncryptedPostApiAsync("sendVideoGroup",
                new { grid = threadId, visibility = 0, clientId = ts.ToString(), ttl = 0, zsource = 704, msgType = 5, msgInfo, imei = GetImei() });
        else
            return await _apiClient.CallEncryptedPostApiAsync("sendVideo",
                new { toId = threadId, clientId = ts.ToString(), ttl = 0, zsource = 704, msgType = 5, msgInfo, imei = GetImei() });
    }

    public async Task<ZaloApiResponse<JsonElement>> SendVoiceAsync(string threadId, string voiceUrl, int? fileSize = null, string? msg = null, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();

        // TS does HEAD request to get content-length for fileSize
        int computedFileSize = fileSize ?? 0;
        if (computedFileSize == 0)
        {
            try
            {
                using var headRequest = new HttpRequestMessage(HttpMethod.Head, voiceUrl);
                var headResponse = await _httpClient.SendAsync(headRequest);
                if (headResponse.IsSuccessStatusCode && headResponse.Content.Headers.ContentLength.HasValue)
                    computedFileSize = (int)headResponse.Content.Headers.ContentLength.Value;
            }
            catch { /* best-effort */ }
        }

        var msgInfo = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["voiceUrl"] = voiceUrl,
            ["m4aUrl"] = voiceUrl,
            ["fileSize"] = computedFileSize,
        }, _jsonOptions);

        if (threadType == ThreadType.Group)
            return await _apiClient.CallEncryptedPostApiAsync("sendVoiceGroup",
                new { grid = threadId, visibility = 0, clientId = ts.ToString(), ttl = 0, zsource = -1, msgType = 3, msgInfo, imei = GetImei() });
        else
            return await _apiClient.CallEncryptedPostApiAsync("sendVoice",
                new { toId = threadId, clientId = ts.ToString(), ttl = 0, zsource = -1, msgType = 3, msgInfo, imei = GetImei() });
    }

    public async Task<ZaloApiResponse<JsonElement>> SendCardAsync(string threadId, long userId, string? msg = null, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        var clientId = ts.ToString();
        // TS: calls api.getQR(userId) to get QR code URL
        var qrResult = await GetQrAsync(userId.ToString());
        var qrCodeUrl = "";
        if (qrResult.IsSuccess && qrResult.Data.ValueKind == JsonValueKind.Object)
        {
            qrCodeUrl = TryGetString(qrResult.Data, userId.ToString()) ?? "";
        }
        var msgInfo = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["contactUid"] = userId.ToString(),
            ["qrCodeUrl"] = qrCodeUrl,
        }, _jsonOptions);
        if (threadType == ThreadType.Group)
            return await _apiClient.CallEncryptedPostApiAsync("sendCardGroup",
                new { ttl = 0, msgType = 6, clientId, msgInfo, visibility = 0, grid = threadId });
        else
            return await _apiClient.CallEncryptedPostApiAsync("sendCard",
                new { ttl = 0, msgType = 6, clientId, msgInfo, toId = threadId, imei = GetImei() });
    }
    public Task<ZaloApiResponse<JsonElement>> SendBankCardAsync(string threadId, object cardData, ThreadType threadType = ThreadType.User) => _apiClient.CallEncryptedPostApiAsync("sendBankCard", new { threadId, cardData, threadType });

    /// <summary>
    /// Forward message to a thread.
    /// Equivalent to forwardMessage() in zca-js (multi-thread support + reference).
    /// </summary>
    public Task<ZaloApiResponse<JsonElement>> ForwardMessageAsync(string message, List<string> threadIds, long? referenceMsgId = null, long? referenceTs = null, int? referenceFwLvl = null, int? ttl = null, ThreadType threadType = ThreadType.User)
    {
        if (string.IsNullOrEmpty(message))
            throw new InvalidOperationException("Missing message content");
        if (threadIds == null || threadIds.Count == 0)
            throw new InvalidOperationException("Missing thread IDs");

        var ts = GetTimestamp();
        var clientId = ts.ToString();

        object decorLog;
        object msgInfo;

        if (referenceMsgId.HasValue && referenceTs.HasValue)
        {
            var refId = referenceMsgId.Value.ToString();
            var refTs = referenceTs.Value;
            var fwLvl = referenceFwLvl ?? 2;

            decorLog = new
            {
                fw = new
                {
                    pmsg = new { st = 1, ts = refTs, id = refId },
                    rmsg = new { st = 1, ts = refTs, id = refId },
                    fwLvl = fwLvl,
                }
            };

            msgInfo = new
            {
                message,
                reference = JsonSerializer.Serialize(new
                {
                    type = 3,
                    data = JsonSerializer.Serialize(new
                    {
                        id = refId,
                        ts = refTs,
                        logSrcType = 0,
                        fwLvl = fwLvl,
                    })
                })
            };
        }
        else
        {
            decorLog = new { };
            msgInfo = new { message };
        }

        var isGroup = threadType == ThreadType.Group;

        object paramsObj;
        if (isGroup)
        {
            paramsObj = new
            {
                grids = threadIds.Select(id => new { clientId, grid = id, ttl = ttl ?? 0 }).ToArray(),
                ttl = ttl ?? 0,
                msgType = "1",
                totalIds = threadIds.Count,
                msgInfo = JsonSerializer.Serialize(msgInfo, _jsonOptions),
                decorLog = JsonSerializer.Serialize(decorLog, _jsonOptions),
            };
        }
        else
        {
            paramsObj = new
            {
                toIds = threadIds.Select(id => new { clientId, toUid = id, ttl = ttl ?? 0 }).ToArray(),
                imei = GetImei(),
                ttl = ttl ?? 0,
                msgType = "1",
                totalIds = threadIds.Count,
                msgInfo = JsonSerializer.Serialize(msgInfo, _jsonOptions),
                decorLog = JsonSerializer.Serialize(decorLog, _jsonOptions),
            };
        }

        // Forward uses file service endpoint: mforward instead of forwardMessage
        var fileService = GetFileServiceUrl();
        var endpoint = isGroup ? $"{fileService}/api/group/mforward" : $"{fileService}/api/message/mforward";
        var url = ZaloUtils.MakeUrl(endpoint, null, _context.ApiVersion, _context.ApiType);

        return SendEncryptedPostToUrlAsync(url, paramsObj);
    }

    public Task<ZaloApiResponse<JsonElement>> DeleteMessageAsync(string messageId, string ownerId, bool onlyMe = false, ThreadType threadType = ThreadType.User)
    {
        var isSelf = _context.Uid.ToString() == ownerId;
        var isGroup = threadType == ThreadType.Group;

        // Validation: can't delete own message for everyone - use Undo instead
        if (isSelf && !onlyMe)
            throw new InvalidOperationException("To delete your message for everyone, use undo api instead");

        // Validation: can't delete messages for everyone in private chat
        if (!isGroup && !onlyMe)
            throw new InvalidOperationException("Can't delete message for everyone in a private chat");

        var ts = GetTimestamp();
        var endpoint = isGroup ? "deleteMessageGroup" : "deleteMessage";
        if (isGroup)
            return _apiClient.CallEncryptedPostApiAsync(endpoint,
                new { grid = "", cliMsgId = ts, msgs = new[] { new { cliMsgId = "0", globalMsgId = messageId, ownerId, destId = "" } }, onlyMe = onlyMe ? 1 : 0 });
        else
            return _apiClient.CallEncryptedPostApiAsync(endpoint,
                new { toid = "", cliMsgId = ts, msgs = new[] { new { cliMsgId = "0", globalMsgId = messageId, ownerId, destId = "" } }, onlyMe = onlyMe ? 1 : 0, imei = GetImei() });
    }

    public Task<ZaloApiResponse<JsonElement>> UndoAsync(string messageId, string ownerId, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        if (threadType == ThreadType.Group)
            return _apiClient.CallEncryptedPostApiAsync("undo",
                new { msgId = messageId, clientId = ts, cliMsgId = "0", uidFrom = ownerId, idTo = "" });
        else
            return _apiClient.CallEncryptedPostApiAsync("undo",
                new { msgId = messageId, clientId = ts, cliMsgId = "0", uidFrom = ownerId, idTo = "" });
    }

    public Task<ZaloApiResponse<JsonElement>> SendTypingEventAsync(string threadId, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        if (threadType == ThreadType.Group)
            return _apiClient.CallEncryptedPostApiAsync("sendTypingEventGroup",
                new { msgType = 1, clientId = ts, uid = _context.Uid.ToString(), threadId, isGroup = 1 });
        else
            return _apiClient.CallEncryptedPostApiAsync("sendTypingEvent",
                new { msgType = 1, clientId = ts, uid = _context.Uid.ToString(), threadId, isGroup = 0 });
    }

    public Task<ZaloApiResponse<JsonElement>> SendSeenEventAsync(string threadId, long messageId, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        if (threadType == ThreadType.Group)
            return _apiClient.CallEncryptedPostApiAsync("sendSeenEventGroup",
                new { messageId, clientId = ts, grid = threadId, visibility = 0 });
        else
            return _apiClient.CallEncryptedPostApiAsync("sendSeenEvent",
                new { messageId, clientId = ts, toid = threadId, imei = GetImei() });
    }

    public Task<ZaloApiResponse<JsonElement>> SendDeliveredEventAsync(string threadId, long messageId, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        if (threadType == ThreadType.Group)
            return _apiClient.CallEncryptedPostApiAsync("sendDeliveredEventGroup",
                new { messageId, clientId = ts, grid = threadId, visibility = 0 });
        else
            return _apiClient.CallEncryptedPostApiAsync("sendDeliveredEvent",
                new { messageId, clientId = ts, toid = threadId, imei = GetImei() });
    }

    public Task<ZaloApiResponse<JsonElement>> AddReactionAsync(string messageId, string reactionIcon, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        return _apiClient.CallEncryptedPostApiAsync("addReaction",
            new { react_list = new[] { new { msgId = messageId, reactIcon = reactionIcon, reactType = 0 } }, clientId = ts, imei = GetImei() });
    }

    /// <summary>
    /// Send an uploaded attachment as a message in a thread.
    /// After calling UploadAttachmentAsync, use the returned result to send the attachment.
    /// Equivalent to handleAttachment() in zca-js src/apis/sendMessage.ts.
    /// </summary>
    public async Task<ZaloApiResponse<JsonElement>> SendAttachmentMessageAsync(
        UploadAttachmentResult uploadResult,
        string threadId,
        string? message = null,
        ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        var isGroupMessage = threadType == ThreadType.Group;
        var fileService = GetFileServiceUrl();

        string urlType;
        object paramsObj;

        if (uploadResult.IsImage)
        {
            urlType = "photo_original/send?";
            paramsObj = new
            {
                photoId = uploadResult.PhotoId,
                clientId = ts.ToString(),
                desc = message ?? "",
                width = uploadResult.Width,
                height = uploadResult.Height,
                toid = isGroupMessage ? null : threadId,
                grid = isGroupMessage ? threadId : (string?)null,
                rawUrl = uploadResult.NormalUrl,
                hdUrl = uploadResult.HdUrl,
                thumbUrl = uploadResult.ThumbUrl,
                oriUrl = isGroupMessage ? uploadResult.NormalUrl : (string?)null,
                normalUrl = isGroupMessage ? (string?)null : uploadResult.NormalUrl,
                hdSize = uploadResult.TotalSize.ToString(),
                zsource = -1,
                ttl = 0,
                jcp = "{\"convertible\":\"jxl\"}",
            };
        }
        else
        {
            urlType = "asyncfile/msg?";
            paramsObj = new
            {
                fileId = uploadResult.FileId,
                checksum = uploadResult.Checksum ?? "",
                checksumSha = "",
                extention = Path.GetExtension(uploadResult.FileName ?? "file").TrimStart('.'),
                totalSize = uploadResult.TotalSize,
                fileName = uploadResult.FileName ?? "file",
                clientId = uploadResult.ClientFileId,
                fType = 1,
                fileCount = 0,
                fdata = "{}",
                toid = isGroupMessage ? null : threadId,
                grid = isGroupMessage ? threadId : (string?)null,
                fileUrl = uploadResult.FileUrl,
                zsource = -1,
                ttl = 0,
            };
        }

        var url = ZaloUtils.MakeUrl($"{fileService}/api/{(isGroupMessage ? "group" : "message")}/{urlType}", null, _context.ApiVersion, _context.ApiType);
        return await SendEncryptedPostToUrlAsync(url, paramsObj);
    }

    /// <summary>
    /// Upload files (images, videos, documents) to a thread.
    /// Equivalent to uploadAttachment() in zca-js src/apis/uploadAttachment.ts.
    /// Supports: jpg, jpeg, png, webp (image), mp4 (video), and other file types.
    /// Image uploads return immediately; video/file uploads wait for WebSocket confirm.
    /// </summary>
    public async Task<List<UploadAttachmentResult>> UploadAttachmentAsync(
        object[] sources,
        string threadId,
        ThreadType type = ThreadType.User)
    {
        if (sources == null || sources.Length == 0)
            throw new InvalidOperationException("Missing sources");
        if (string.IsNullOrEmpty(threadId))
            throw new InvalidOperationException("Missing threadId");

        var isGroupMessage = type == ThreadType.Group;
        var fileService = GetFileServiceUrl();
        var urlPrefix = $"{fileService}/api/{(isGroupMessage ? "group" : "message")}/";
        var typeParam = isGroupMessage ? "11" : "2";

        var chunkSize = GetShareFileSetting("chunk_size_file", 491520);
        var maxFile = GetShareFileSetting("max_file", 10);
        var maxSizeMb = GetShareFileSetting("max_size_share_file_v3", 50);

        if (sources.Length > maxFile)
            throw new InvalidOperationException($"Exceed maximum file of {maxFile}");

        var attachmentsData = new List<(string filePath, byte[] fileBuffer, string fileName, string extFile, string fileType, long totalSize, int width, int height, int totalChunk, long clientId, object[] chunkContents)>();
        long baseClientId = GetTimestamp();

        for (int srcIdx = 0; srcIdx < sources.Length; srcIdx++)
        {
            var source = sources[srcIdx];
            bool isFilePath = source is string;
            bool isBuffer = source is byte[];

            if (!isFilePath && !isBuffer)
                throw new InvalidOperationException("Invalid source type: must be file path (string) or byte array");

            byte[] fileBuffer;
            string filePath;
            string fileName;

            if (isFilePath)
            {
                filePath = (string)source;
                if (!File.Exists(filePath))
                    throw new InvalidOperationException($"File not found: {filePath}");
                fileBuffer = await File.ReadAllBytesAsync(filePath);
                fileName = Path.GetFileName(filePath);
            }
            else
            {
                fileBuffer = (byte[])source;
                filePath = $"buffer_{srcIdx}";
                fileName = $"file_{srcIdx}";
            }

            var extFile = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();

            // Validate extension
            var restrictedExt = GetRestrictedExtensions();
            if (restrictedExt.Contains(extFile))
                throw new InvalidOperationException($"File extension \"{extFile}\" is not allowed");

            long totalSize = fileBuffer.Length;
            int width = 0, height = 0;
            string fileType;

            switch (extFile)
            {
                case "jpg":
                case "jpeg":
                case "png":
                case "webp":
                case "gif":
                {
                    if (totalSize > maxSizeMb * 1024L * 1024L)
                        throw new InvalidOperationException($"File {fileName} size exceed maximum size of {maxSizeMb}MB");

                    fileType = "image";

                    // Try to get image dimensions from metadata
                    if (_context.Options.ImageMetadataGetter != null && isFilePath)
                    {
                        var meta = await _context.Options.ImageMetadataGetter(filePath);
                        if (meta != null)
                        {
                            width = meta.Width;
                            height = meta.Height;
                        }
                    }

                    break;
                }
                case "mp4":
                {
                    if (totalSize > maxSizeMb * 1024L * 1024L)
                        throw new InvalidOperationException($"File {fileName} size exceed maximum size of {maxSizeMb}MB");
                    fileType = "video";
                    break;
                }
                default:
                {
                    if (totalSize > maxSizeMb * 1024L * 1024L)
                        throw new InvalidOperationException($"File {fileName} size exceed maximum size of {maxSizeMb}MB");
                    fileType = "others";
                    break;
                }
            }

            var totalChunk = (int)Math.Ceiling((double)totalSize / chunkSize);
            var clientId = baseClientId + srcIdx;

            // Build chunk contents (for building multipart form data)
            var chunks = new object[totalChunk];
            for (int i = 0; i < totalChunk; i++)
            {
                var start = i * chunkSize;
                var length = (int)Math.Min(chunkSize, totalSize - start);
                var chunkData = new byte[length];
                Array.Copy(fileBuffer, start, chunkData, 0, length);
                chunks[i] = chunkData;
            }

            attachmentsData.Add((filePath, fileBuffer, fileName, extFile, fileType, totalSize, width, height, totalChunk, clientId, chunks));
        }

        var results = new List<UploadAttachmentResult>();
        var requests = new List<Task>();

        foreach (var data in attachmentsData)
        {
            var urlType = data.fileType == "image" ? "photo_original/upload" : "asyncfile/upload";

            for (int chunkIdx = 0; chunkIdx < data.totalChunk; chunkIdx++)
            {
                var chunkId = chunkIdx + 1;
                var paramsObj = new Dictionary<string, object?>
                {
                    [isGroupMessage ? "grid" : "toid"] = threadId,
                    ["totalChunk"] = data.totalChunk,
                    ["fileName"] = data.fileName,
                    ["clientId"] = data.clientId,
                    ["totalSize"] = data.totalSize,
                    ["imei"] = GetImei(),
                    ["isE2EE"] = 0,
                    ["jxl"] = 0,
                    ["chunkId"] = chunkId
                };

                var encryptedParams = EncodeAes(JsonSerializer.Serialize(paramsObj, _jsonOptions));
                if (encryptedParams == null)
                    throw new InvalidOperationException("Failed to encrypt message");

                var uploadUrl = $"{urlPrefix}{urlType}";
                uploadUrl = ZaloUtils.MakeUrl(uploadUrl, new Dictionary<string, string> { ["type"] = typeParam, ["params"] = encryptedParams });

                var chunkContent = data.chunkContents[chunkIdx] as byte[] ?? Array.Empty<byte>();
                var content = new MultipartFormDataContent();
                content.Add(new ByteArrayContent(chunkContent), "chunkContent", data.fileName);

                var chunkIdxCapture = chunkIdx;
                var dataCapture = data;

                requests.Add(ProcessChunkResponse(uploadUrl, content, dataCapture, chunkIdxCapture, results, data.totalChunk));
            }
        }

        await Task.WhenAll(requests);

        return results;
    }

    private async Task ProcessChunkResponse(
        string uploadUrl,
        MultipartFormDataContent content,
        (string filePath, byte[] fileBuffer, string fileName, string extFile, string fileType, long totalSize, int width, int height, int totalChunk, long clientId, object[] chunkContents) data,
        int chunkIndex,
        List<UploadAttachmentResult> results,
        int totalChunks)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
            request.Headers.Add("User-Agent", _context.UserAgent);
            if (!string.IsNullOrEmpty(_context.Imei))
                request.Headers.Add("x-zalo-imei", _context.Imei);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            if (!root.TryGetProperty("error_code", out var ecEl) || ecEl.GetInt32() != 0)
                return; // error — skip

            if (!root.TryGetProperty("data", out var dataEl))
                return;

            var rawData = dataEl.GetString();
            if (string.IsNullOrEmpty(rawData))
                return;

            var decrypted = AesHelper.DecryptAesCbc(_context.SecretKey, rawData);
            if (decrypted == null)
                return;

            using var innerDoc = JsonDocument.Parse(decrypted);
            var innerRoot = innerDoc.RootElement;

            if (innerRoot.TryGetProperty("error_code", out var iEc) && iEc.GetInt32() != 0)
                return;

            var innerData = innerRoot.TryGetProperty("data", out var dd) ? dd : innerRoot;

            // Only process response for the FIRST chunk (chunkIndex == 0) or handle per-chunk responses
            if (chunkIndex == 0)
            {
                if (data.fileType == "image")
                {
                    var result = new UploadAttachmentResult
                    {
                        FileType = "image",
                        PhotoId = TryGetString(innerData, "photoId"),
                        NormalUrl = TryGetString(innerData, "normalUrl"),
                        HdUrl = TryGetString(innerData, "hdUrl"),
                        ThumbUrl = TryGetString(innerData, "thumbUrl"),
                        Width = TryGetInt(innerData, "width", data.width),
                        Height = TryGetInt(innerData, "height", data.height),
                        TotalSize = TryGetLong(innerData, "totalSize", data.totalSize),
                        HdSize = TryGetLong(innerData, "totalSize", data.totalSize),
                        Finished = TryGetInt(innerData, "finished", 1),
                        ClientFileId = TryGetLong(innerData, "clientFileId", data.clientId),
                        ChunkId = TryGetInt(innerData, "chunkId", 1),
                    };

                    lock (results)
                    {
                        results.Add(result);
                    }
                }
                else
                {
                    // For video/others — wait for WebSocket callback
                    var fileId = TryGetString(innerData, "fileId");
                    if (!string.IsNullOrEmpty(fileId))
                    {
                        var tcs = new TaskCompletionSource<UploadAttachmentResult>();
                        var fileType = data.fileType;
                        var fileName = data.fileName;
                        var totalSize = data.totalSize;

                        UploadCallback callback = null!;
                        callback = (wsData) =>
                        {
                            lock (results)
                            {
                                var result = new UploadAttachmentResult
                                {
                                    FileType = fileType,
                                    FileId = fileId,
                                    FileUrl = TryGetString(wsData, "url") ?? TryGetString(wsData, "fileUrl"),
                                    Checksum = ZaloUtils.GetMd5LargeFile(data.fileBuffer),
                                    FileName = fileName,
                                    TotalSize = totalSize,
                                    ChunkId = TryGetInt(wsData, "chunkId", chunkIndex + 1),
                                    Finished = 1,
                                    ClientFileId = data.clientId,
                                };
                                results.Add(result);
                            }
                            tcs.TrySetResult(null!);
                        };

                        _context.UploadCallbacks[fileId] = callback;

                        // Timeout after 30 seconds if WebSocket never confirms
                        _ = Task.Delay(30000).ContinueWith(_ =>
                        {
                            if (_context.UploadCallbacks.Remove(fileId))
                            {
                                lock (results)
                                {
                                    var fallbackResult = new UploadAttachmentResult
                                    {
                                        FileType = fileType,
                                        FileId = fileId,
                                        FileName = fileName,
                                        TotalSize = totalSize,
                                        Finished = 0,
                                        ClientFileId = data.clientId,
                                    };
                                    results.Add(fallbackResult);
                                }
                                tcs.TrySetResult(null!);
                            }
                        });

                        await tcs.Task;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Silently fail individual chunk uploads
        }
    }

    private async Task<ZaloApiResponse<JsonElement>> SendEncryptedPostToUrlAsync(string url, object data)
    {
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        var encrypted = AesHelper.EncryptAesCbc(_context.SecretKey, json);
        if (encrypted == null)
            return new ZaloApiResponse<JsonElement> { Error = "Failed to encrypt" };

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("User-Agent", _context.UserAgent);
        if (!string.IsNullOrEmpty(_context.Imei))
            request.Headers.Add("x-zalo-imei", _context.Imei);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["params"] = encrypted });

        var response = await _httpClient.SendAsync(request);
        var responseString = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(responseString);
        var root = doc.RootElement;
        if (!root.TryGetProperty("error_code", out var ecEl) || ecEl.GetInt32() != 0)
        {
            var errMsg = root.TryGetProperty("error_message", out var emEl) ? emEl.GetString() ?? "Unknown" : "Unknown";
            var errCode = root.TryGetProperty("error_code", out var ecEl2) ? ecEl2.GetInt32() : -1;
            return new ZaloApiResponse<JsonElement> { Error = errMsg, ErrorCode = errCode };
        }

        if (!root.TryGetProperty("data", out var dataEl))
            return new ZaloApiResponse<JsonElement> { Error = "No data" };

        var rawData = dataEl.GetString();
        if (string.IsNullOrEmpty(rawData))
            return new ZaloApiResponse<JsonElement> { Data = JsonDocument.Parse("{}").RootElement.Clone() };

        var decrypted = AesHelper.DecryptAesCbc(_context.SecretKey, rawData);
        if (decrypted == null)
            return new ZaloApiResponse<JsonElement> { Error = "Failed to decrypt" };

        using var innerDoc = JsonDocument.Parse(decrypted);
        var innerRoot = innerDoc.RootElement;
        if (innerRoot.TryGetProperty("error_code", out var iEc) && iEc.GetInt32() != 0)
        {
            var iMsg = innerRoot.TryGetProperty("error_message", out var iEm) ? iEm.GetString() ?? "Unknown" : "Unknown";
            return new ZaloApiResponse<JsonElement> { Error = iMsg, ErrorCode = iEc.GetInt32() };
        }

        var respData = innerRoot.TryGetProperty("data", out var iData) ? iData.Clone() : innerRoot.Clone();
        return new ZaloApiResponse<JsonElement> { Data = respData };
    }

    private string? EncodeAes(string json)
    {
        return AesHelper.EncryptAesCbc(_context.SecretKey, json);
    }

    private string GetFileServiceUrl()
    {
        if (_context.ZpwServiceMapV3.TryGetValue("file", out var urls) && urls.Length > 0)
            return urls[0].TrimEnd('/');
        return "https://files.chat.zalo.me";
    }

    private int GetShareFileSetting(string key, int defaultVal)
    {
        if (_context.Settings.TryGetValue("sharefile", out var obj) && obj is Dictionary<string, object> sf)
        {
            if (sf.TryGetValue(key, out var val))
            {
                try { return Convert.ToInt32(val); } catch { }
            }
        }
        return defaultVal;
    }

    private static int TryGetInt(JsonElement el, string key, int defaultVal = 0)
    {
        return el.TryGetProperty(key, out var v) ? v.GetInt32() : defaultVal;
    }

    private static long TryGetLong(JsonElement el, string key, long defaultVal = 0)
    {
        return el.TryGetProperty(key, out var v) ? v.GetInt64() : defaultVal;
    }

    private List<string> GetRestrictedExtensions()
    {
        if (_context.Settings.TryGetValue("sharefile", out var obj) && obj is Dictionary<string, object> sf)
        {
            if (sf.TryGetValue("restricted_ext_file", out var val) && val is List<object> extList)
                return extList.Select(e => e?.ToString()?.ToLowerInvariant() ?? "").ToList();
        }
        return new List<string> { "exe", "bat", "cmd", "msi", "dll", "scr", "pif", "vbs", "js", "jar" };
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // ─── Group APIs ──────────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> CreateGroupAsync(string name, List<long> memberIds) => _apiClient.CallPostApiAsync("createGroup", new { name, memberIds });
    public Task<ZaloApiResponse<JsonElement>> GetAllGroupsAsync() => _apiClient.CallGetApiAsync("getAllGroups");
    public Task<ZaloApiResponse<JsonElement>> GetGroupInfoAsync(string groupId) => _apiClient.CallGetApiAsync("getGroupInfo", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> GetGroupMembersInfoAsync(string groupId) => _apiClient.CallGetApiAsync("getGroupMembersInfo", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> GetGroupChatHistoryAsync(string groupId) => _apiClient.CallGetApiAsync("getGroupChatHistory", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> AddUserToGroupAsync(string groupId, long userId) => _apiClient.CallPostApiAsync("addUserToGroup", new { groupId, userId });
    public Task<ZaloApiResponse<JsonElement>> RemoveUserFromGroupAsync(string groupId, long userId) => _apiClient.CallPostApiAsync("removeUserFromGroup", new { groupId, userId });
    public Task<ZaloApiResponse<JsonElement>> LeaveGroupAsync(string groupId) => _apiClient.CallPostApiAsync("leaveGroup", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> ChangeGroupNameAsync(string groupId, string name) => _apiClient.CallPostApiAsync("changeGroupName", new { groupId, name });
    public Task<ZaloApiResponse<JsonElement>> ChangeGroupAvatarAsync(string groupId, string imagePath) => _apiClient.CallPostApiAsync("changeGroupAvatar", new { groupId, imagePath });
    public Task<ZaloApiResponse<JsonElement>> ChangeGroupOwnerAsync(string groupId, long newOwnerId) => _apiClient.CallPostApiAsync("changeGroupOwner", new { groupId, newOwnerId });
    public Task<ZaloApiResponse<JsonElement>> AddGroupDeputyAsync(string groupId, long userId) => _apiClient.CallPostApiAsync("addGroupDeputy", new { groupId, userId });
    public Task<ZaloApiResponse<JsonElement>> RemoveGroupDeputyAsync(string groupId, long userId) => _apiClient.CallPostApiAsync("removeGroupDeputy", new { groupId, userId });
    public Task<ZaloApiResponse<JsonElement>> AddGroupBlockedMemberAsync(string groupId, long userId) => _apiClient.CallPostApiAsync("addGroupBlockedMember", new { groupId, userId });
    public Task<ZaloApiResponse<JsonElement>> RemoveGroupBlockedMemberAsync(string groupId, long userId) => _apiClient.CallPostApiAsync("removeGroupBlockedMember", new { groupId, userId });
    public Task<ZaloApiResponse<JsonElement>> GetGroupBlockedMemberAsync(string groupId) => _apiClient.CallGetApiAsync("getGroupBlockedMember", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> DisperseGroupAsync(string groupId) => _apiClient.CallPostApiAsync("disperseGroup", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> EnableGroupLinkAsync(string groupId) => _apiClient.CallPostApiAsync("enableGroupLink", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> DisableGroupLinkAsync(string groupId) => _apiClient.CallPostApiAsync("disableGroupLink", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> GetGroupLinkInfoAsync(string groupId) => _apiClient.CallGetApiAsync("getGroupLinkInfo", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> GetGroupLinkDetailAsync(string groupCode) => _apiClient.CallGetApiAsync("getGroupLinkDetail", new { groupCode });
    public Task<ZaloApiResponse<JsonElement>> JoinGroupLinkAsync(string groupCode) => _apiClient.CallPostApiAsync("joinGroupLink", new { groupCode });
    public Task<ZaloApiResponse<JsonElement>> UpdateGroupSettingsAsync(string groupId, object settings) => _apiClient.CallPostApiAsync("updateGroupSettings", new { groupId, settings });
    public Task<ZaloApiResponse<JsonElement>> GetPendingGroupMembersAsync(string groupId) => _apiClient.CallGetApiAsync("getPendingGroupMembers", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> ReviewPendingMemberRequestAsync(string groupId, long userId, bool approve) => _apiClient.CallPostApiAsync("reviewPendingMemberRequest", new { groupId, userId, approve });
    public Task<ZaloApiResponse<JsonElement>> GetGroupInviteBoxInfoAsync(string code) => _apiClient.CallGetApiAsync("getGroupInviteBoxInfo", new { code });
    public Task<ZaloApiResponse<JsonElement>> GetGroupInviteBoxListAsync() => _apiClient.CallGetApiAsync("getGroupInviteBoxList");
    public Task<ZaloApiResponse<JsonElement>> JoinGroupInviteBoxAsync(string code) => _apiClient.CallPostApiAsync("joinGroupInviteBox", new { code });
    public Task<ZaloApiResponse<JsonElement>> DeleteGroupInviteBoxAsync(string code) => _apiClient.CallPostApiAsync("deleteGroupInviteBox", new { code });
    public Task<ZaloApiResponse<JsonElement>> UpgradeGroupToCommunityAsync(string groupId) => _apiClient.CallPostApiAsync("upgradeGroupToCommunity", new { groupId });

    // ─── Conversation APIs ───────────────────────────────────────────────
    public async Task<ZaloApiResponse<JsonElement>> GetConversationAsync()
    {
        try
        {
            if (_conversationCache != null && (DateTime.UtcNow - _conversationCacheTime).TotalSeconds < 60)
                return _conversationCache;

            var convList = new List<JsonElement>();
            var profiles = new Dictionary<string, JsonElement>();
            var groupInfoDict = new Dictionary<string, JsonElement>();

            var friendsResult = await GetAllFriendsAsync();
            if (friendsResult.IsSuccess && friendsResult.Data.ValueKind == JsonValueKind.Array)
            {
                foreach (var friend in friendsResult.Data.EnumerateArray())
                {
                    var userId = friend.TryGetProperty("userId", out var uidEl) ? uidEl.GetString()
                        : friend.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                    if (string.IsNullOrEmpty(userId)) continue;
                    var displayName = friend.TryGetProperty("displayName", out var dnEl) ? dnEl.GetString()
                        : friend.TryGetProperty("name", out var nEl) ? nEl.GetString() : userId;

                    convList.Add(JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["id"] = userId, ["type"] = 0, ["name"] = displayName ?? userId, ["lastMsg"] = "", ["lastTime"] = 0L }, _jsonOptions));
                    profiles[userId!] = JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["displayName"] = displayName ?? userId }, _jsonOptions);
                }
            }

            var groupsResult = await GetAllGroupsAsync();
            if (groupsResult.IsSuccess && groupsResult.Data.ValueKind == JsonValueKind.Object)
            {
                var gData = groupsResult.Data;
                if (gData.TryGetProperty("gridVerMap", out var gridMap) && gridMap.ValueKind == JsonValueKind.Object)
                {
                    foreach (var grp in gridMap.EnumerateObject())
                    {
                        var gid = grp.Name;
                        if (string.IsNullOrEmpty(gid)) continue;
                        convList.Add(JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["id"] = gid, ["type"] = 1, ["name"] = $"Group {gid}", ["lastMsg"] = "", ["lastTime"] = 0L, ["memberCount"] = 0 }, _jsonOptions));
                    }
                }
                else if (gData.TryGetProperty("data", out var gList) && gList.ValueKind == JsonValueKind.Array)
                {
                    foreach (var grp in gList.EnumerateArray())
                    {
                        var gid = grp.TryGetProperty("groupId", out var gidEl) ? gidEl.GetString() : null;
                        if (string.IsNullOrEmpty(gid)) continue;
                        convList.Add(JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["id"] = gid, ["type"] = 1, ["name"] = $"Group {gid}", ["lastMsg"] = "", ["lastTime"] = 0L, ["memberCount"] = 0 }, _jsonOptions));
                    }
                }
            }

            var resultDict = new Dictionary<string, object?>
            {
                ["data"] = new Dictionary<string, object?> { ["conversations"] = convList, ["profiles"] = profiles, ["groupInfo"] = groupInfoDict }
            };

            var result = new ZaloApiResponse<JsonElement> { Data = JsonSerializer.SerializeToElement(resultDict, _jsonOptions), Error = null };
            _conversationCache = result;
            _conversationCacheTime = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex) { return new ZaloApiResponse<JsonElement> { Data = default, Error = ex.Message }; }
    }

    /// <summary>
    /// Get the real conversation list from Zalo's getContext API.
    /// Returns structured data with conversations, profiles, and groupInfo.
    /// Equivalent to zca-js's getContext().
    /// </summary>
    public Task<ZaloApiResponse<JsonElement>> GetContextAsync() => _apiClient.CallGetApiAsync("getContext");

    public Task<ZaloApiResponse<JsonElement>> GetArchivedChatListAsync() => _apiClient.CallGetApiAsync("getArchivedChatList");
    public Task<ZaloApiResponse<JsonElement>> UpdateArchivedChatListAsync(string threadId, bool archive, ThreadType threadType = ThreadType.User) => _apiClient.CallPostApiAsync("updateArchivedChatList", new { threadId, archive, threadType });
    public Task<ZaloApiResponse<JsonElement>> GetHiddenConversationsAsync() => _apiClient.CallGetApiAsync("getHiddenConversations");
    public Task<ZaloApiResponse<JsonElement>> SetHiddenConversationsAsync(List<string> threadIds) => _apiClient.CallPostApiAsync("setHiddenConversations", new { threadIds });
    public Task<ZaloApiResponse<JsonElement>> GetPinConversationsAsync() => _apiClient.CallGetApiAsync("getPinConversations");
    public Task<ZaloApiResponse<JsonElement>> SetPinnedConversationsAsync(List<string> threadIds) => _apiClient.CallPostApiAsync("setPinnedConversations", new { threadIds });
    public Task<ZaloApiResponse<JsonElement>> ResetHiddenConversPinAsync() => _apiClient.CallPostApiAsync("resetHiddenConversPin");
    public Task<ZaloApiResponse<JsonElement>> UpdateHiddenConversPinAsync(string threadId, bool hidden, bool pinned) => _apiClient.CallPostApiAsync("updateHiddenConversPin", new { threadId, hidden, pinned });
    public Task<ZaloApiResponse<JsonElement>> DeleteChatAsync(string threadId) => _apiClient.CallEncryptedPostApiAsync("deleteChat", new { threadId, clientId = GetTimestamp() });
    public Task<ZaloApiResponse<JsonElement>> AddUnreadMarkAsync(string threadId) => _apiClient.CallPostApiAsync("addUnreadMark", new { threadId });
    public Task<ZaloApiResponse<JsonElement>> RemoveUnreadMarkAsync(string threadId) => _apiClient.CallPostApiAsync("removeUnreadMark", new { threadId });
    public Task<ZaloApiResponse<JsonElement>> GetUnreadMarkAsync() => _apiClient.CallGetApiAsync("getUnreadMark");
    public Task<ZaloApiResponse<JsonElement>> GetAutoDeleteChatAsync(string threadId) => _apiClient.CallGetApiAsync("getAutoDeleteChat", new { threadId });
    public Task<ZaloApiResponse<JsonElement>> UpdateAutoDeleteChatAsync(string threadId, int duration) => _apiClient.CallPostApiAsync("updateAutoDeleteChat", new { threadId, duration });

    // ─── Sticker APIs (encrypted GET) ────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> GetStickersAsync(string keyword) => _apiClient.CallEncryptedGetApiAsync("getStickers", new { keyword, gif = 1, guggy = 0, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> GetStickersDetailAsync(int stickerId) => _apiClient.CallEncryptedGetApiAsync("getStickersDetail", new { sid = stickerId });
    public Task<ZaloApiResponse<JsonElement>> GetStickerCategoryDetailAsync(int categoryId) => _apiClient.CallEncryptedGetApiAsync("getStickerCategoryDetail", new { cid = categoryId });
    public Task<ZaloApiResponse<JsonElement>> SearchStickerAsync(string keyword) => _apiClient.CallEncryptedGetApiAsync("searchSticker", new { keyword, limit = 50, srcType = 0, imei = GetImei() });

    // ─── Poll APIs ───────────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> CreatePollAsync(string groupId, string question, List<string> options) => _apiClient.CallPostApiAsync("createPoll", new { groupId, question, options });
    public Task<ZaloApiResponse<JsonElement>> GetPollDetailAsync(string pollId) => _apiClient.CallGetApiAsync("getPollDetail", new { pollId });
    public Task<ZaloApiResponse<JsonElement>> AddPollOptionsAsync(string pollId, List<string> options) => _apiClient.CallPostApiAsync("addPollOptions", new { pollId, options });
    public Task<ZaloApiResponse<JsonElement>> VotePollAsync(string pollId, List<int> optionIds) => _apiClient.CallPostApiAsync("votePoll", new { pollId, optionIds });
    public Task<ZaloApiResponse<JsonElement>> LockPollAsync(string pollId) => _apiClient.CallPostApiAsync("lockPoll", new { pollId });
    public Task<ZaloApiResponse<JsonElement>> SharePollAsync(string pollId, string threadId, ThreadType threadType = ThreadType.User) => _apiClient.CallPostApiAsync("sharePoll", new { pollId, threadId, threadType });

    // ─── Reminder APIs ───────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> CreateReminderAsync(string groupId, string message, long remindTime) => _apiClient.CallPostApiAsync("createReminder", new { groupId, message, remindTime });
    public Task<ZaloApiResponse<JsonElement>> EditReminderAsync(string reminderId, string message, long remindTime) => _apiClient.CallPostApiAsync("editReminder", new { reminderId, message, remindTime });
    public Task<ZaloApiResponse<JsonElement>> RemoveReminderAsync(string reminderId) => _apiClient.CallPostApiAsync("removeReminder", new { reminderId });
    public Task<ZaloApiResponse<JsonElement>> GetReminderAsync(string reminderId) => _apiClient.CallGetApiAsync("getReminder", new { reminderId });
    public Task<ZaloApiResponse<JsonElement>> GetListReminderAsync(string groupId) => _apiClient.CallGetApiAsync("getListReminder", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> GetReminderResponsesAsync(string reminderId) => _apiClient.CallGetApiAsync("getReminderResponses", new { reminderId });

    // ─── Catalog APIs ────────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> CreateCatalogAsync(string name) => _apiClient.CallPostApiAsync("createCatalog", new { name });
    public Task<ZaloApiResponse<JsonElement>> UpdateCatalogAsync(string catalogId, string name) => _apiClient.CallPostApiAsync("updateCatalog", new { catalogId, name });
    public Task<ZaloApiResponse<JsonElement>> DeleteCatalogAsync(string catalogId) => _apiClient.CallPostApiAsync("deleteCatalog", new { catalogId });
    public Task<ZaloApiResponse<JsonElement>> GetCatalogListAsync() => _apiClient.CallGetApiAsync("getCatalogList");
    public Task<ZaloApiResponse<JsonElement>> CreateProductCatalogAsync(string catalogId, object product) => _apiClient.CallPostApiAsync("createProductCatalog", new { catalogId, product });
    public Task<ZaloApiResponse<JsonElement>> UpdateProductCatalogAsync(string productId, object product) => _apiClient.CallPostApiAsync("updateProductCatalog", new { productId, product });
    public Task<ZaloApiResponse<JsonElement>> DeleteProductCatalogAsync(string productId) => _apiClient.CallPostApiAsync("deleteProductCatalog", new { productId });
    public Task<ZaloApiResponse<JsonElement>> GetProductCatalogListAsync(string catalogId) => _apiClient.CallGetApiAsync("getProductCatalogList", new { catalogId });
    public Task<ZaloApiResponse<JsonElement>> UploadProductPhotoAsync(string productId, string imagePath) => _apiClient.CallPostApiAsync("uploadProductPhoto", new { productId, imagePath });

    // ─── Auto Reply APIs ────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> CreateAutoReplyAsync(object autoReplyData) => _apiClient.CallPostApiAsync("createAutoReply", autoReplyData);
    public Task<ZaloApiResponse<JsonElement>> UpdateAutoReplyAsync(string autoReplyId, object autoReplyData) => _apiClient.CallPostApiAsync("updateAutoReply", new { autoReplyId, autoReplyData });
    public Task<ZaloApiResponse<JsonElement>> DeleteAutoReplyAsync(string autoReplyId) => _apiClient.CallPostApiAsync("deleteAutoReply", new { autoReplyId });
    public Task<ZaloApiResponse<JsonElement>> GetAutoReplyListAsync() => _apiClient.CallGetApiAsync("getAutoReplyList");

    // ─── Quick Message APIs ─────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> AddQuickMessageAsync(string message) => _apiClient.CallPostApiAsync("addQuickMessage", new { message });
    public Task<ZaloApiResponse<JsonElement>> UpdateQuickMessageAsync(string quickMessageId, string message) => _apiClient.CallPostApiAsync("updateQuickMessage", new { quickMessageId, message });
    public Task<ZaloApiResponse<JsonElement>> RemoveQuickMessageAsync(string quickMessageId) => _apiClient.CallPostApiAsync("removeQuickMessage", new { quickMessageId });
    public Task<ZaloApiResponse<JsonElement>> GetQuickMessageListAsync() => _apiClient.CallGetApiAsync("getQuickMessageList");

    // ─── Board/Note APIs ────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> GetListBoardAsync(string groupId) => _apiClient.CallGetApiAsync("getListBoard", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> CreateNoteAsync(string groupId, string content) => _apiClient.CallPostApiAsync("createNote", new { groupId, content });
    public Task<ZaloApiResponse<JsonElement>> EditNoteAsync(string noteId, string content) => _apiClient.CallPostApiAsync("editNote", new { noteId, content });

    // ─── Label APIs ─────────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> GetLabelsAsync() => _apiClient.CallGetApiAsync("getLabels");
    public Task<ZaloApiResponse<JsonElement>> UpdateLabelsAsync(List<object> labels) => _apiClient.CallPostApiAsync("updateLabels", new { labels });

    // ─── Settings APIs ───────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> GetSettingsAsync() => _apiClient.CallGetApiAsync("getSettings");
    public Task<ZaloApiResponse<JsonElement>> UpdateSettingsAsync(object settings) => _apiClient.CallPostApiAsync("updateSettings", settings);
    public Task<ZaloApiResponse<JsonElement>> UpdateLangAsync(string language) => _apiClient.CallPostApiAsync("updateLang", new { language });
    public Task<ZaloApiResponse<JsonElement>> SetMuteAsync(string threadId, int muteDuration, ThreadType threadType = ThreadType.User) => _apiClient.CallPostApiAsync("setMute", new { threadId, muteDuration, threadType });
    public Task<ZaloApiResponse<JsonElement>> GetMuteAsync(string threadId, ThreadType threadType = ThreadType.User) => _apiClient.CallGetApiAsync("getMute", new { threadId, threadType });
    public Task<ZaloApiResponse<JsonElement>> UpdateActiveStatusAsync(bool isActive) => _apiClient.CallPostApiAsync("updateActiveStatus", new { isActive });
    public Task<ZaloApiResponse<JsonElement>> KeepAliveAsync() => _apiClient.CallGetApiAsync("keepAlive");
    public Task<ZaloApiResponse<JsonElement>> LastOnlineAsync(long userId) => _apiClient.CallGetApiAsync("lastOnline", new { userId });
    public Task<ZaloApiResponse<JsonElement>> GetQrAsync(string userId) => _apiClient.CallEncryptedPostApiAsync("getQR", new { fids = new[] { userId } });
    public Task<ZaloApiResponse<JsonElement>> GetCookieAsync() => _apiClient.CallGetApiAsync("getCookie");
    public Task<ZaloApiResponse<JsonElement>> ParseLinkAsync(string url) => _apiClient.CallEncryptedGetApiAsync("parseLink", new { link = url, version = 1, imei = GetImei() });

    // ─── Report API (encrypted POST for both user/group) ─────────────────
    public Task<ZaloApiResponse<JsonElement>> SendReportAsync(string threadId, int reason, string? content = null, ThreadType threadType = ThreadType.User)
    {
        if (threadType == ThreadType.Group)
            return _apiClient.CallEncryptedPostApiAsync("sendReportGroup",
                new { uidTo = threadId, type = 14, reason, content = content ?? "", imei = GetImei() });
        else
            return _apiClient.CallEncryptedPostApiAsync("sendReport",
                new { idTo = threadId, objId = "person.profile", reason = reason.ToString(), content });
    }

    public Task<ZaloApiResponse<JsonElement>> GetBizAccountAsync() => _apiClient.CallGetApiAsync("getBizAccount");
    public Task<ZaloApiResponse<JsonElement>> CustomApiCallAsync(string method, string endpoint, object? data = null, bool isGet = true)
        => _apiClient.CallCustomApiAsync(method, endpoint, data, isGet);
}