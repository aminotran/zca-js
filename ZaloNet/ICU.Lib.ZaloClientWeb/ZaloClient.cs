using ICU.Lib.ZaloClientWeb.Auth;
using ICU.Lib.ZaloClientWeb.Crypto;
using ICU.Lib.ZaloClientWeb.Exceptions;
using ICU.Lib.ZaloClientWeb.Models;
using ICU.Lib.ZaloClientWeb.Models.Types;
using ICU.Lib.ZaloClientWeb.Utils;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ICU.Lib.ZaloClientWeb;

public class ZaloClient : IDisposable
{
    private readonly ZaloOptions _options;
    private HttpClient _httpClient;
    private CookieContainer _cookieContainer;
    private bool _disposed;

    public ZaloContext? Context { get; private set; }
    public ZaloApi? Api { get; private set; }
    public ZaloLogger Logger { get; }

    public ZaloClient(ZaloOptions? options = null)
    {
        _options = options ?? new ZaloOptions();
        _cookieContainer = new CookieContainer();
        Logger = new ZaloLogger(_options.Logging);

        HttpClientHandler handler = new()
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
        LoginHelper loginHelper = new(this, _options, _httpClient, _cookieContainer);
        Context = await loginHelper.LoginAsync(credentials);

        if (Context == null)
            throw new ZaloApiException("Login failed - context could not be created");

        Context.CookieContainer = _cookieContainer;

        Logger.Info("Logged in as", Context.Uid.ToString());
        Api = new ZaloApi(Context, _httpClient);
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

        LoginHelper loginHelper = new(this, _options, _httpClient, _cookieContainer);
        QrLoginHelper qrLoginHelper = new(loginHelper, _httpClient, _cookieContainer);
        Credentials credentials = await qrLoginHelper.LoginWithQrAsync(userAgent, language, qrPath, onQrCodeGenerated);

        return await LoginAsync(credentials);
    }

    /// <summary>
    /// Exports current session cookies as a list of CookieItem.
    /// Useful for persisting updated credentials (e.g., after Set-Cookie refresh).
    /// </summary>
    public List<CookieItem> GetCookies()
    {
        List<CookieItem> cookies = new();
        try
        {
            // Extract cookies from all known Zalo domains
            string[] domains = new[]
            {
                "chat.zalo.me", "zalo.me", "wpa.chat.zalo.me",
                "id.zalo.me", "tt-profile-wpa.zalo.me",
                "tt-friend-wpa.zalo.me", "tt-group-wpa.zalo.me",
                "tt-sticker-wpa.zalo.me", "tt-chat-wpa.zalo.me",
                "tt-convers-wpa.zalo.me", "tt-alias-wpa.zalo.me",
            };
            foreach (string? domain in domains)
            {
                Uri uri = new($"https://{domain}");
                CookieCollection domainCookies = _cookieContainer.GetCookies(uri);
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
        foreach (CookieItem cookie in cookies)
        {
            try
            {
                string domain = cookie.Domain;
                if (string.IsNullOrEmpty(domain)) continue;

                string rawDomain = domain.StartsWith(".") ? domain.Substring(1) : domain;
                Uri uri = new($"https://{rawDomain}");
                Cookie netCookie = new(cookie.Name, cookie.Value, cookie.Path, rawDomain);
                netCookie.Secure = cookie.Secure;
                netCookie.HttpOnly = cookie.HttpOnly;

                _cookieContainer.Add(uri, netCookie);

                if (domain.StartsWith("."))
                {
                    string[] subdomains = new[] {
                        $"chat.{rawDomain}", $"id.{rawDomain}", $"wpa.{rawDomain}",
                        $"tt-profile-wpa.{rawDomain}", $"tt-friend-wpa.{rawDomain}",
                        $"tt-group-wpa.{rawDomain}", $"tt-sticker-wpa.{rawDomain}",
                        $"tt-chat-wpa.{rawDomain}", $"tt-convers-wpa.{rawDomain}",
                        $"tt-alias-wpa.{rawDomain}",
                    };
                    foreach (string? sub in subdomains)
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
    internal ZaloContext Context { get; }
    internal HttpClient HttpClient { get; }

    internal WebSocket.ZaloListener? _listener;

    private ZaloApiResponse<JsonElement>? _conversationCache;
    private DateTime _conversationCacheTime;

    public ZaloApi(ZaloContext context, HttpClient httpClient)
    {
        Context = context;
        HttpClient = httpClient;
        ApiClient = new ZaloApiClient(context, httpClient);
    }

    /// <summary>
    /// Initializes a new instance with a pre-configured <see cref="ZaloApiClient"/> for DI support.
    /// </summary>
    internal ZaloApi(ZaloApiClient apiClient)
    {
        ApiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        Context = apiClient.Context;
        HttpClient = apiClient.HttpClient;
    }

    /// <summary>
    /// Gets the underlying <see cref="ZaloApiClient"/> for direct API access.
    /// </summary>
    public ZaloApiClient ApiClient { get; }

    public WebSocket.ZaloListener Listener
    {
        get
        {
            if (_listener == null)
                _listener = new WebSocket.ZaloListener(Context, HttpClient);
            return _listener;
        }
    }

    private long GetTimestamp() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private string GetImei() => Context.Imei;

    // ─── Profile APIs ────────────────────────────────────────────────────
    public async Task<ZaloApiResponse<Models.ApiModels.getUserInfoModel.ResponseModel?>> GetUserInfoAsync(long userId)
    {
        Models.ApiModels.getUserInfoModel.RequestModel requestModel = new()
        {
            phonebook_version = Context.ExtraVer.Phonebook,
            friend_pversion_map = new List<string> { $"{userId}_0" },
            avatar_size = (int)AvatarSize.Small,
            language = Context.Language,
            show_online_status = 1,
            imei = GetImei()
        };
        ZaloApiResponse<JsonElement> responseResult = await ApiClient.CallPostApiAsync("getUserInfo", requestModel);
        //string a = responseResult.Data.ToString();
        Models.ApiModels.getUserInfoModel.ResponseModel? data = JsonSerializer.Deserialize<ICU.Lib.ZaloClientWeb.Models.ApiModels.getUserInfoModel.ResponseModel>(responseResult.Data);
        ZaloApiResponse<Models.ApiModels.getUserInfoModel.ResponseModel?> result = new()
        {
            Data = data,
            Error = responseResult.Error,
            ErrorCode = responseResult.ErrorCode
        };
        return result;
    }
    public Task<ZaloApiResponse<JsonElement>> FindUserAsync(string phoneNumber) => ApiClient.CallGetApiAsync("findUser", new { phoneNumber });
    public Task<ZaloApiResponse<JsonElement>> FindUserByUsernameAsync(string username) => ApiClient.CallGetApiAsync("findUserByUsername", new { username });
    public Task<ZaloApiResponse<JsonElement>> GetAccountInfoAsync() => ApiClient.CallGetApiAsync("fetchAccountInfo");
    public long GetOwnId() => Context.Uid;
    public Task<ZaloApiResponse<long>> GetOwnIdAsync() => Task.FromResult(new ZaloApiResponse<long> { Data = Context.Uid });
    public Task<ZaloApiResponse<JsonElement>> UpdateProfileAsync(object profileData) => ApiClient.CallPostApiAsync("updateProfile", profileData);
    public Task<ZaloApiResponse<JsonElement>> UpdateProfileBioAsync(string bio) => ApiClient.CallPostApiAsync("updateProfileBio", new { bio });
    public Task<ZaloApiResponse<JsonElement>> ChangeAccountAvatarAsync(string imagePath) => ApiClient.CallPostApiAsync("changeAccountAvatar", new { imagePath });
    public Task<ZaloApiResponse<JsonElement>> GetAvatarListAsync() => ApiClient.CallGetApiAsync("getAvatarList");
    public Task<ZaloApiResponse<JsonElement>> GetFullAvatarAsync() => ApiClient.CallGetApiAsync("getFullAvatar");
    public Task<ZaloApiResponse<JsonElement>> DeleteAvatarAsync(long avatarId) => ApiClient.CallPostApiAsync("deleteAvatar", new { avatarId });
    public Task<ZaloApiResponse<JsonElement>> ReuseAvatarAsync(long avatarId) => ApiClient.CallPostApiAsync("reuseAvatar", new { avatarId });
    public Task<ZaloApiResponse<JsonElement>> GetAvatarUrlProfileAsync(long userId) => ApiClient.CallGetApiAsync("getAvatarUrlProfile", new { userId });

    // ─── Friend APIs (all encrypted POST) ─────────────────────────────────
    public async Task<ZaloApiResponse<List<Models.ApiModels.getAllFriendsModel.ResponseModel>?>> GetAllFriendsAsync()
    {
        Models.ApiModels.getAllFriendsModel.RequestModel bodyRequest = new()
        {
            incInvalid = 1,
            page = 1,
            count = 20000,
            avatar_size = (int)AvatarSize.Small,
            actiontime = 0,
            imei = GetImei()
        };
        ZaloApiResponse<JsonElement> responseResult = await ApiClient.CallEncryptedGetApiAsync("getAllFriends", bodyRequest);
        List<Models.ApiModels.getAllFriendsModel.ResponseModel>? data = JsonSerializer.Deserialize<List<Models.ApiModels.getAllFriendsModel.ResponseModel>>(responseResult.Data);
        ZaloApiResponse<List<Models.ApiModels.getAllFriendsModel.ResponseModel>?> result = new()
        {
            Data = data,
            Error = responseResult.Error,
            ErrorCode = responseResult.ErrorCode
        };
        return result;
    }
    public Task<ZaloApiResponse<JsonElement>> GetFriendRequestStatusAsync(long friendId) => ApiClient.CallEncryptedGetApiAsync("getFriendRequestStatus", new { fid = friendId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> SendFriendRequestAsync(long userId, string? message = null) => ApiClient.CallEncryptedPostApiAsync("sendFriendRequest", new { userId, imei = GetImei(), msg = message ?? "" });
    public Task<ZaloApiResponse<JsonElement>> AcceptFriendRequestAsync(long userId) => ApiClient.CallEncryptedPostApiAsync("acceptFriendRequest", new { fid = userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> RejectFriendRequestAsync(long userId) => ApiClient.CallEncryptedPostApiAsync("rejectFriendRequest", new { fid = userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> RemoveFriendAsync(long userId) => ApiClient.CallEncryptedPostApiAsync("removeFriend", new { fid = userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> UndoFriendRequestAsync(long userId) => ApiClient.CallEncryptedPostApiAsync("undoFriendRequest", new { fid = userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> BlockUserAsync(long userId) => ApiClient.CallEncryptedPostApiAsync("blockUser", new { fid = userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> UnblockUserAsync(long userId) => ApiClient.CallEncryptedPostApiAsync("unblockUser", new { fid = userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> BlockViewFeedAsync(long userId) => ApiClient.CallEncryptedPostApiAsync("blockViewFeed", new { fid = userId, imei = GetImei(), blockType = 1 });
    public Task<ZaloApiResponse<JsonElement>> GetFriendBoardListAsync(long userId) => ApiClient.CallGetApiAsync("getFriendBoardList", new { userId });
    public Task<ZaloApiResponse<JsonElement>> GetFriendOnlinesAsync() => ApiClient.CallGetApiAsync("getFriendOnlines");
    public Task<ZaloApiResponse<JsonElement>> GetFriendRecommendationsAsync() => ApiClient.CallGetApiAsync("getFriendRecommendations");
    public Task<ZaloApiResponse<JsonElement>> ChangeFriendAliasAsync(long userId, string alias) => ApiClient.CallEncryptedPostApiAsync("changeFriendAlias", new { userId, alias, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> RemoveFriendAliasAsync(long userId) => ApiClient.CallEncryptedPostApiAsync("removeFriendAlias", new { userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> GetSentFriendRequestAsync() => ApiClient.CallGetApiAsync("getSentFriendRequest");
    public Task<ZaloApiResponse<JsonElement>> GetCloseFriendsAsync() => ApiClient.CallGetApiAsync("getCloseFriends");
    public Task<ZaloApiResponse<JsonElement>> GetAliasListAsync() => ApiClient.CallGetApiAsync("getAliasList");
    public Task<ZaloApiResponse<JsonElement>> GetRelatedFriendGroupAsync() => ApiClient.CallGetApiAsync("getRelatedFriendGroup");
    public Task<ZaloApiResponse<JsonElement>> GetMultiUsersByPhonesAsync(List<string> phones) => ApiClient.CallPostApiAsync("getMultiUsersByPhones", new { phones });
    public Task<ZaloApiResponse<JsonElement>> InviteUserToGroupsAsync(long userId, List<string> groupIds) => ApiClient.CallPostApiAsync("inviteUserToGroups", new { userId, groupIds });

    // ─── Message APIs (all encrypted POST) ────────────────────────────────
    public async Task<ZaloApiResponse<JsonElement>> SendMessageAsync(MessageContent message, string threadId, ThreadType threadType = ThreadType.User)
    {
        long ts = GetTimestamp();
        bool isGroup = threadType == ThreadType.Group;
        bool hasAttachments = message.Attachments?.Count > 0;

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

            int totalMentionLen = mentionsFinal.Sum(m => m.len);
            if (totalMentionLen > message.Msg.Length)
                throw new InvalidOperationException("Invalid mentions: total mention characters exceed message length");

            mentionInfo = JsonSerializer.Serialize(mentionsFinal, _jsonOptions);
        }

        // ─── If has attachments, upload + send in one call ─────────────
        if (hasAttachments)
        {
            string msgText = message.Msg ?? "";
            bool hasQuote = message.Quote != null;

            // Determine if text should be sent separately:
            // TS: if (non-image single file AND msg has text + no quote) → send text as desc with attachment
            // else → send text message first, then attachments
            string? firstExt = GetFirstAttachmentExtension(message.Attachments!);
            bool isSingleFile = message.Attachments!.Count == 1;
            bool canBeDesc = isSingleFile && firstExt is "jpg" or "jpeg" or "png" or "webp" or "gif";

            ZaloApiResponse<JsonElement>? textResult = null;

            // If NOT canBeDesc and there's text, send text message separately first
            if ((!canBeDesc && msgText.Length > 0) || (msgText.Length > 0 && hasQuote))
            {
                Dictionary<string, object?> textParams = new()
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
                    string qEndpoint = isGroup ? "sendMessageGroupQuote" : "sendMessageQuote";
                    textResult = await ApiClient.CallEncryptedPostApiAsync(qEndpoint, textParams);
                }
                else
                {
                    string textEndpoint = isGroup ? (mentionInfo != null ? "sendMessageGroupMention" : "sendMessageGroup") : "sendMessage";
                    textResult = await ApiClient.CallEncryptedPostApiAsync(textEndpoint, textParams);
                }
            }

            // Upload attachments
            object[] uploadSources = message.Attachments!.Select(a => a).ToArray();
            List<UploadAttachmentResult> uploadResults = await UploadAttachmentAsync(uploadSources, threadId, threadType);

            // Send each attachment
            List<ZaloApiResponse<JsonElement>> attachmentResults = new();
            foreach (UploadAttachmentResult upload in uploadResults)
            {
                ZaloApiResponse<JsonElement> attachResult = await SendAttachmentMessageAsync(
                    upload, threadId,
                    canBeDesc ? msgText : null,
                    threadType);
                attachmentResults.Add(attachResult);
            }

            // Build combined result
            Dictionary<string, object?> combinedData = new()
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
        Dictionary<string, object?> paramsDict = new()
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
                return await ApiClient.CallEncryptedPostApiAsync("sendMessageGroupQuote", paramsDict);
            else
                return await ApiClient.CallEncryptedPostApiAsync("sendMessageQuote", paramsDict);
        }

        // Select endpoint
        string endpoint;
        if (isGroup)
            endpoint = mentionInfo != null ? "sendMessageGroupMention" : "sendMessageGroup";
        else
            endpoint = "sendMessage";

        return await ApiClient.CallEncryptedPostApiAsync(endpoint, paramsDict);
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
        object first = attachments[0];
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
        long ts = GetTimestamp();
        if (threadType == ThreadType.Group)
            return ApiClient.CallEncryptedPostApiAsync("sendStickerGroup",
                new { stickerId, cateId = stickerCategoryId, type, clientId = ts, ttl = 0, zsource = 101, grid = threadId, visibility = 0 });
        else
            return ApiClient.CallEncryptedPostApiAsync("sendSticker",
                new { stickerId, cateId = stickerCategoryId, type, clientId = ts, ttl = 0, zsource = 101, toid = threadId, imei = GetImei() });
    }

    public async Task<ZaloApiResponse<JsonElement>> SendLinkAsync(string threadId, string link, string? msg = null, ThreadType threadType = ThreadType.User)
    {
        long ts = GetTimestamp();
        ZaloApiResponse<JsonElement> parseResult = await ParseLinkAsync(link);
        string href = link;
        string src = "";
        string title = "";
        string desc = "";
        string thumb = "";
        string media = "[]";

        if (parseResult.IsSuccess)
        {
            // parseLink response: { data: { href,src,title,desc,thumb,media,... }, error_maps:{} }
            // CallEncryptedGetApiAsync already unwraps the outer data field
            JsonElement data = parseResult.Data;
            // The actual link data is nested under "data" property
            if (data.TryGetProperty("data", out JsonElement linkData))
                data = linkData;

            href = TryGetString(data, "href") ?? link;
            src = TryGetString(data, "src") ?? "";
            title = TryGetString(data, "title") ?? "";
            desc = TryGetString(data, "desc") ?? "";
            thumb = TryGetString(data, "thumb") ?? "";
            // TS: media = JSON.stringify(res.data.media) — media is an object, not a string
            if (data.TryGetProperty("media", out JsonElement mediaEl) && mediaEl.ValueKind == JsonValueKind.Object)
                media = JsonSerializer.Serialize(mediaEl, _jsonOptions);
            else if (data.TryGetProperty("media", out JsonElement mediaStr) && mediaStr.ValueKind == JsonValueKind.String)
                media = mediaStr.GetString() ?? "[]";
        }

        string finalMsg = !string.IsNullOrEmpty(msg) ? (msg!.Contains(link) ? msg : msg + " " + link) : link;
        if (threadType == ThreadType.Group)
            return await ApiClient.CallEncryptedPostApiAsync("sendLinkGroup",
                new { msg = finalMsg, href, src, title, desc, thumb, type = 2, media, ttl = 0, clientId = ts, grid = threadId, imei = GetImei() });
        else
        {
            var sendParams = new { msg = finalMsg, href, src, title, desc, thumb, type = 2, media, ttl = 0, clientId = ts, toId = threadId, mentionInfo = "" };
            Console.Error.WriteLine($"[SENDLINK] raw JSON: {System.Text.Json.JsonSerializer.Serialize(sendParams, _jsonOptions)}");
            Console.Error.WriteLine($"[SENDLINK] href={href} src={src} title={title} desc={desc} thumb={thumb} media={media}");
            return await ApiClient.CallEncryptedPostApiAsync("sendLink", sendParams);
        }
    }

    private static string? TryGetString(JsonElement el, string key)
    {
        return el.TryGetProperty(key, out JsonElement prop) && prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
    }

    public async Task<ZaloApiResponse<JsonElement>> SendVideoAsync(string threadId, string videoUrl, string thumbnailUrl, string? msg = null, int duration = 0, int width = 1280, int height = 720, ThreadType threadType = ThreadType.User)
    {
        long ts = GetTimestamp();
        // TS does HEAD request to get content-length for fileSize
        int fileSize = 0;
        try
        {
            using HttpRequestMessage headRequest = new(HttpMethod.Head, videoUrl);
            HttpResponseMessage headResponse = await HttpClient.SendAsync(headRequest);
            if (headResponse.IsSuccessStatusCode && headResponse.Content.Headers.ContentLength.HasValue)
                fileSize = (int)headResponse.Content.Headers.ContentLength.Value;
        }
        catch { /* best-effort */ }

        string msgInfo = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["videoUrl"] = videoUrl,
            ["thumbUrl"] = thumbnailUrl,
            ["duration"] = duration,
            ["width"] = width,
            ["height"] = height,
            ["fileSize"] = fileSize,
            ["properties"] = new Dictionary<string, object?>
            {
                ["color"] = -1,
                ["size"] = -1,
                ["type"] = 1003,
                ["subType"] = 0,
                ["ext"] = new Dictionary<string, object?> { ["sSrcType"] = -1, ["sSrcStr"] = "", ["msg_warning_type"] = 0 }
            },
            ["title"] = msg ?? ""
        }, _jsonOptions);

        if (threadType == ThreadType.Group)
            return await ApiClient.CallEncryptedPostApiAsync("sendVideoGroup",
                new { grid = threadId, visibility = 0, clientId = ts.ToString(), ttl = 0, zsource = 704, msgType = 5, msgInfo, imei = GetImei() });
        else
            return await ApiClient.CallEncryptedPostApiAsync("sendVideo",
                new { toId = threadId, clientId = ts.ToString(), ttl = 0, zsource = 704, msgType = 5, msgInfo, imei = GetImei() });
    }

    public async Task<ZaloApiResponse<JsonElement>> SendVoiceAsync(string threadId, string voiceUrl, int? fileSize = null, string? msg = null, ThreadType threadType = ThreadType.User)
    {
        long ts = GetTimestamp();

        // TS does HEAD request to get content-length for fileSize
        int computedFileSize = fileSize ?? 0;
        if (computedFileSize == 0)
        {
            try
            {
                using HttpRequestMessage headRequest = new(HttpMethod.Head, voiceUrl);
                HttpResponseMessage headResponse = await HttpClient.SendAsync(headRequest);
                if (headResponse.IsSuccessStatusCode && headResponse.Content.Headers.ContentLength.HasValue)
                    computedFileSize = (int)headResponse.Content.Headers.ContentLength.Value;
            }
            catch { /* best-effort */ }
        }

        string msgInfo = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["voiceUrl"] = voiceUrl,
            ["m4aUrl"] = voiceUrl,
            ["fileSize"] = computedFileSize,
        }, _jsonOptions);

        if (threadType == ThreadType.Group)
            return await ApiClient.CallEncryptedPostApiAsync("sendVoiceGroup",
                new { grid = threadId, visibility = 0, clientId = ts.ToString(), ttl = 0, zsource = -1, msgType = 3, msgInfo, imei = GetImei() });
        else
            return await ApiClient.CallEncryptedPostApiAsync("sendVoice",
                new { toId = threadId, clientId = ts.ToString(), ttl = 0, zsource = -1, msgType = 3, msgInfo, imei = GetImei() });
    }

    public async Task<ZaloApiResponse<JsonElement>> SendCardAsync(string threadId, long userId, string? msg = null, ThreadType threadType = ThreadType.User)
    {
        long ts = GetTimestamp();
        string clientId = ts.ToString();
        // TS: calls api.getQR(userId) to get QR code URL
        ZaloApiResponse<JsonElement> qrResult = await GetQrAsync(userId.ToString());
        string qrCodeUrl = "";
        if (qrResult.IsSuccess && qrResult.Data.ValueKind == JsonValueKind.Object)
        {
            qrCodeUrl = TryGetString(qrResult.Data, userId.ToString()) ?? "";
        }
        string msgInfo = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["contactUid"] = userId.ToString(),
            ["qrCodeUrl"] = qrCodeUrl,
        }, _jsonOptions);
        if (threadType == ThreadType.Group)
            return await ApiClient.CallEncryptedPostApiAsync("sendCardGroup",
                new { ttl = 0, msgType = 6, clientId, msgInfo, visibility = 0, grid = threadId });
        else
            return await ApiClient.CallEncryptedPostApiAsync("sendCard",
                new { ttl = 0, msgType = 6, clientId, msgInfo, toId = threadId, imei = GetImei() });
    }
    public Task<ZaloApiResponse<JsonElement>> SendBankCardAsync(string threadId, object cardData, ThreadType threadType = ThreadType.User) => ApiClient.CallEncryptedPostApiAsync("sendBankCard", new { threadId, cardData, threadType });

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

        long ts = GetTimestamp();
        string clientId = ts.ToString();

        object decorLog;
        object msgInfo;

        if (referenceMsgId.HasValue && referenceTs.HasValue)
        {
            string refId = referenceMsgId.Value.ToString();
            long refTs = referenceTs.Value;
            int fwLvl = referenceFwLvl ?? 2;

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

        bool isGroup = threadType == ThreadType.Group;

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
        string fileService = GetFileServiceUrl();
        string endpoint = isGroup ? $"{fileService}/api/group/mforward" : $"{fileService}/api/message/mforward";
        string url = ZaloUtils.MakeUrl(endpoint, null, Context.ApiVersion, Context.ApiType);

        return SendEncryptedPostToUrlAsync(url, paramsObj);
    }

    public Task<ZaloApiResponse<JsonElement>> DeleteMessageAsync(string messageId, string ownerId, bool onlyMe = false, ThreadType threadType = ThreadType.User)
    {
        bool isSelf = Context.Uid.ToString() == ownerId;
        bool isGroup = threadType == ThreadType.Group;

        // Validation: can't delete own message for everyone - use Undo instead
        if (isSelf && !onlyMe)
            throw new InvalidOperationException("To delete your message for everyone, use undo api instead");

        // Validation: can't delete messages for everyone in private chat
        if (!isGroup && !onlyMe)
            throw new InvalidOperationException("Can't delete message for everyone in a private chat");

        long ts = GetTimestamp();
        string endpoint = isGroup ? "deleteMessageGroup" : "deleteMessage";
        if (isGroup)
            return ApiClient.CallEncryptedPostApiAsync(endpoint,
                new { grid = "", cliMsgId = ts, msgs = new[] { new { cliMsgId = "0", globalMsgId = messageId, ownerId, destId = "" } }, onlyMe = onlyMe ? 1 : 0 });
        else
            return ApiClient.CallEncryptedPostApiAsync(endpoint,
                new { toid = "", cliMsgId = ts, msgs = new[] { new { cliMsgId = "0", globalMsgId = messageId, ownerId, destId = "" } }, onlyMe = onlyMe ? 1 : 0, imei = GetImei() });
    }

    public Task<ZaloApiResponse<JsonElement>> UndoAsync(string messageId, string ownerId, ThreadType threadType = ThreadType.User)
    {
        long ts = GetTimestamp();
        if (threadType == ThreadType.Group)
            return ApiClient.CallEncryptedPostApiAsync("undo",
                new { msgId = messageId, clientId = ts, cliMsgId = "0", uidFrom = ownerId, idTo = "" });
        else
            return ApiClient.CallEncryptedPostApiAsync("undo",
                new { msgId = messageId, clientId = ts, cliMsgId = "0", uidFrom = ownerId, idTo = "" });
    }

    public Task<ZaloApiResponse<JsonElement>> SendTypingEventAsync(string threadId, ThreadType threadType = ThreadType.User)
    {
        long ts = GetTimestamp();
        if (threadType == ThreadType.Group)
            return ApiClient.CallEncryptedPostApiAsync("sendTypingEventGroup",
                new { msgType = 1, clientId = ts, uid = Context.Uid.ToString(), threadId, isGroup = 1 });
        else
            return ApiClient.CallEncryptedPostApiAsync("sendTypingEvent",
                new { msgType = 1, clientId = ts, uid = Context.Uid.ToString(), threadId, isGroup = 0 });
    }

    public Task<ZaloApiResponse<JsonElement>> SendSeenEventAsync(string threadId, long messageId, ThreadType threadType = ThreadType.User)
    {
        long ts = GetTimestamp();
        if (threadType == ThreadType.Group)
            return ApiClient.CallEncryptedPostApiAsync("sendSeenEventGroup",
                new { messageId, clientId = ts, grid = threadId, visibility = 0 });
        else
            return ApiClient.CallEncryptedPostApiAsync("sendSeenEvent",
                new { messageId, clientId = ts, toid = threadId, imei = GetImei() });
    }

    public Task<ZaloApiResponse<JsonElement>> SendDeliveredEventAsync(string threadId, long messageId, ThreadType threadType = ThreadType.User)
    {
        long ts = GetTimestamp();
        if (threadType == ThreadType.Group)
            return ApiClient.CallEncryptedPostApiAsync("sendDeliveredEventGroup",
                new { messageId, clientId = ts, grid = threadId, visibility = 0 });
        else
            return ApiClient.CallEncryptedPostApiAsync("sendDeliveredEvent",
                new { messageId, clientId = ts, toid = threadId, imei = GetImei() });
    }

    public Task<ZaloApiResponse<JsonElement>> AddReactionAsync(string messageId, string reactionIcon, ThreadType threadType = ThreadType.User)
    {
        long ts = GetTimestamp();
        return ApiClient.CallEncryptedPostApiAsync("addReaction",
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
        long ts = GetTimestamp();
        bool isGroupMessage = threadType == ThreadType.Group;
        string fileService = GetFileServiceUrl();

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

        string url = ZaloUtils.MakeUrl($"{fileService}/api/{(isGroupMessage ? "group" : "message")}/{urlType}", null, Context.ApiVersion, Context.ApiType);
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

        bool isGroupMessage = type == ThreadType.Group;
        string fileService = GetFileServiceUrl();
        string urlPrefix = $"{fileService}/api/{(isGroupMessage ? "group" : "message")}/";
        string typeParam = isGroupMessage ? "11" : "2";

        int chunkSize = GetShareFileSetting("chunk_size_file", 491520);
        int maxFile = GetShareFileSetting("max_file", 10);
        int maxSizeMb = GetShareFileSetting("max_size_share_file_v3", 50);

        if (sources.Length > maxFile)
            throw new InvalidOperationException($"Exceed maximum file of {maxFile}");

        List<(string filePath, byte[] fileBuffer, string fileName, string extFile, string fileType, long totalSize, int width, int height, int totalChunk, long clientId, object[] chunkContents)> attachmentsData = new();
        long baseClientId = GetTimestamp();

        for (int srcIdx = 0; srcIdx < sources.Length; srcIdx++)
        {
            object source = sources[srcIdx];
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

            string extFile = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();

            // Validate extension
            List<string> restrictedExt = GetRestrictedExtensions();
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
                        if (Context.Options.ImageMetadataGetter != null && isFilePath)
                        {
                            ImageMetadata? meta = await Context.Options.ImageMetadataGetter(filePath);
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

            int totalChunk = (int)Math.Ceiling((double)totalSize / chunkSize);
            long clientId = baseClientId + srcIdx;

            // Build chunk contents (for building multipart form data)
            object[] chunks = new object[totalChunk];
            for (int i = 0; i < totalChunk; i++)
            {
                int start = i * chunkSize;
                int length = (int)Math.Min(chunkSize, totalSize - start);
                byte[] chunkData = new byte[length];
                Array.Copy(fileBuffer, start, chunkData, 0, length);
                chunks[i] = chunkData;
            }

            attachmentsData.Add((filePath, fileBuffer, fileName, extFile, fileType, totalSize, width, height, totalChunk, clientId, chunks));
        }

        List<UploadAttachmentResult> results = new();
        List<Task> requests = new();

        foreach ((string filePath, byte[] fileBuffer, string fileName, string extFile, string fileType, long totalSize, int width, int height, int totalChunk, long clientId, object[] chunkContents) data in attachmentsData)
        {
            string urlType = data.fileType == "image" ? "photo_original/upload" : "asyncfile/upload";

            for (int chunkIdx = 0; chunkIdx < data.totalChunk; chunkIdx++)
            {
                int chunkId = chunkIdx + 1;
                Dictionary<string, object?> paramsObj = new()
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

                string? encryptedParams = EncodeAes(JsonSerializer.Serialize(paramsObj, _jsonOptions));
                if (encryptedParams == null)
                    throw new InvalidOperationException("Failed to encrypt message");

                string uploadUrl = $"{urlPrefix}{urlType}";
                uploadUrl = ZaloUtils.MakeUrl(uploadUrl, new Dictionary<string, string> { ["type"] = typeParam, ["params"] = encryptedParams });

                byte[] chunkContent = data.chunkContents[chunkIdx] as byte[] ?? Array.Empty<byte>();
                MultipartFormDataContent content = new();
                content.Add(new ByteArrayContent(chunkContent), "chunkContent", data.fileName);

                int chunkIdxCapture = chunkIdx;
                (string filePath, byte[] fileBuffer, string fileName, string extFile, string fileType, long totalSize, int width, int height, int totalChunk, long clientId, object[] chunkContents) dataCapture = data;

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
            HttpRequestMessage request = new(HttpMethod.Post, uploadUrl);
            request.Headers.Add("User-Agent", Context.UserAgent);
            if (!string.IsNullOrEmpty(Context.Imei))
                request.Headers.Add("x-zalo-imei", Context.Imei);
            request.Content = content;

            HttpResponseMessage response = await HttpClient.SendAsync(request);
            string responseString = await response.Content.ReadAsStringAsync();

            using JsonDocument doc = JsonDocument.Parse(responseString);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("error_code", out JsonElement ecEl) || ecEl.GetInt32() != 0)
                return; // error — skip

            if (!root.TryGetProperty("data", out JsonElement dataEl))
                return;

            string? rawData = dataEl.GetString();
            if (string.IsNullOrEmpty(rawData))
                return;

            string? decrypted = AesHelper.DecryptAesCbc(Context.SecretKey, rawData);
            if (decrypted == null)
                return;

            using JsonDocument innerDoc = JsonDocument.Parse(decrypted);
            JsonElement innerRoot = innerDoc.RootElement;

            if (innerRoot.TryGetProperty("error_code", out JsonElement iEc) && iEc.GetInt32() != 0)
                return;

            JsonElement innerData = innerRoot.TryGetProperty("data", out JsonElement dd) ? dd : innerRoot;

            // Only process response for the FIRST chunk (chunkIndex == 0) or handle per-chunk responses
            if (chunkIndex == 0)
            {
                if (data.fileType == "image")
                {
                    UploadAttachmentResult result = new()
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
                    string? fileId = TryGetString(innerData, "fileId");
                    if (!string.IsNullOrEmpty(fileId))
                    {
                        TaskCompletionSource<UploadAttachmentResult> tcs = new();
                        string fileType = data.fileType;
                        string fileName = data.fileName;
                        long totalSize = data.totalSize;

                        UploadCallback callback = null!;
                        callback = (wsData) =>
                        {
                            lock (results)
                            {
                                UploadAttachmentResult result = new()
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

                        Context.UploadCallbacks[fileId] = callback;

                        // Timeout after 30 seconds if WebSocket never confirms
                        _ = Task.Delay(30000).ContinueWith(_ =>
                        {
                            if (Context.UploadCallbacks.Remove(fileId))
                            {
                                lock (results)
                                {
                                    UploadAttachmentResult fallbackResult = new()
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
        string json = JsonSerializer.Serialize(data, _jsonOptions);
        string? encrypted = AesHelper.EncryptAesCbc(Context.SecretKey, json);
        if (encrypted == null)
            return new ZaloApiResponse<JsonElement> { Error = "Failed to encrypt" };

        HttpRequestMessage request = new(HttpMethod.Post, url);
        request.Headers.Add("User-Agent", Context.UserAgent);
        if (!string.IsNullOrEmpty(Context.Imei))
            request.Headers.Add("x-zalo-imei", Context.Imei);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["params"] = encrypted });

        HttpResponseMessage response = await HttpClient.SendAsync(request);
        string responseString = await response.Content.ReadAsStringAsync();

        using JsonDocument doc = JsonDocument.Parse(responseString);
        JsonElement root = doc.RootElement;
        if (!root.TryGetProperty("error_code", out JsonElement ecEl) || ecEl.GetInt32() != 0)
        {
            string errMsg = root.TryGetProperty("error_message", out JsonElement emEl) ? emEl.GetString() ?? "Unknown" : "Unknown";
            int errCode = root.TryGetProperty("error_code", out JsonElement ecEl2) ? ecEl2.GetInt32() : -1;
            return new ZaloApiResponse<JsonElement> { Error = errMsg, ErrorCode = errCode };
        }

        if (!root.TryGetProperty("data", out JsonElement dataEl))
            return new ZaloApiResponse<JsonElement> { Error = "No data" };

        string? rawData = dataEl.GetString();
        if (string.IsNullOrEmpty(rawData))
            return new ZaloApiResponse<JsonElement> { Data = JsonDocument.Parse("{}").RootElement.Clone() };

        string? decrypted = AesHelper.DecryptAesCbc(Context.SecretKey, rawData);
        if (decrypted == null)
            return new ZaloApiResponse<JsonElement> { Error = "Failed to decrypt" };

        using JsonDocument innerDoc = JsonDocument.Parse(decrypted);
        JsonElement innerRoot = innerDoc.RootElement;
        if (innerRoot.TryGetProperty("error_code", out JsonElement iEc) && iEc.GetInt32() != 0)
        {
            string iMsg = innerRoot.TryGetProperty("error_message", out JsonElement iEm) ? iEm.GetString() ?? "Unknown" : "Unknown";
            return new ZaloApiResponse<JsonElement> { Error = iMsg, ErrorCode = iEc.GetInt32() };
        }

        JsonElement respData = innerRoot.TryGetProperty("data", out JsonElement iData) ? iData.Clone() : innerRoot.Clone();
        return new ZaloApiResponse<JsonElement> { Data = respData };
    }

    private string? EncodeAes(string json)
    {
        return AesHelper.EncryptAesCbc(Context.SecretKey, json);
    }

    private string GetFileServiceUrl()
    {
        if (Context.ZpwServiceMapV3.TryGetValue("file", out string[]? urls) && urls.Length > 0)
            return urls[0].TrimEnd('/');
        return "https://files.chat.zalo.me";
    }

    private int GetShareFileSetting(string key, int defaultVal)
    {
        if (Context.Settings.TryGetValue("sharefile", out object? obj) && obj is Dictionary<string, object> sf)
        {
            if (sf.TryGetValue(key, out object? val))
            {
                try { return Convert.ToInt32(val); } catch { }
            }
        }
        return defaultVal;
    }

    private static int TryGetInt(JsonElement el, string key, int defaultVal = 0)
    {
        return el.TryGetProperty(key, out JsonElement v) ? v.GetInt32() : defaultVal;
    }

    private static long TryGetLong(JsonElement el, string key, long defaultVal = 0)
    {
        return el.TryGetProperty(key, out JsonElement v) ? v.GetInt64() : defaultVal;
    }

    private List<string> GetRestrictedExtensions()
    {
        if (Context.Settings.TryGetValue("sharefile", out object? obj) && obj is Dictionary<string, object> sf)
        {
            if (sf.TryGetValue("restricted_ext_file", out object? val) && val is List<object> extList)
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
    public Task<ZaloApiResponse<JsonElement>> CreateGroupAsync(string name, List<long> memberIds) => ApiClient.CallPostApiAsync("createGroup", new { name, memberIds });
    public Task<ZaloApiResponse<JsonElement>> GetAllGroupsAsync() => ApiClient.CallGetApiAsync("getAllGroups");
    public Task<ZaloApiResponse<JsonElement>> GetGroupInfoAsync(string groupId) => ApiClient.CallGetApiAsync("getGroupInfo", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> GetGroupMembersInfoAsync(string groupId) => ApiClient.CallGetApiAsync("getGroupMembersInfo", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> GetGroupChatHistoryAsync(string groupId) => ApiClient.CallGetApiAsync("getGroupChatHistory", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> AddUserToGroupAsync(string groupId, long userId) => ApiClient.CallPostApiAsync("addUserToGroup", new { groupId, userId });
    public Task<ZaloApiResponse<JsonElement>> RemoveUserFromGroupAsync(string groupId, long userId) => ApiClient.CallPostApiAsync("removeUserFromGroup", new { groupId, userId });
    public Task<ZaloApiResponse<JsonElement>> LeaveGroupAsync(string groupId) => ApiClient.CallPostApiAsync("leaveGroup", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> ChangeGroupNameAsync(string groupId, string name) => ApiClient.CallPostApiAsync("changeGroupName", new { groupId, name });
    public Task<ZaloApiResponse<JsonElement>> ChangeGroupAvatarAsync(string groupId, string imagePath) => ApiClient.CallPostApiAsync("changeGroupAvatar", new { groupId, imagePath });
    public Task<ZaloApiResponse<JsonElement>> ChangeGroupOwnerAsync(string groupId, long newOwnerId) => ApiClient.CallPostApiAsync("changeGroupOwner", new { groupId, newOwnerId });
    public Task<ZaloApiResponse<JsonElement>> AddGroupDeputyAsync(string groupId, long userId) => ApiClient.CallPostApiAsync("addGroupDeputy", new { groupId, userId });
    public Task<ZaloApiResponse<JsonElement>> RemoveGroupDeputyAsync(string groupId, long userId) => ApiClient.CallPostApiAsync("removeGroupDeputy", new { groupId, userId });
    public Task<ZaloApiResponse<JsonElement>> AddGroupBlockedMemberAsync(string groupId, long userId) => ApiClient.CallPostApiAsync("addGroupBlockedMember", new { groupId, userId });
    public Task<ZaloApiResponse<JsonElement>> RemoveGroupBlockedMemberAsync(string groupId, long userId) => ApiClient.CallPostApiAsync("removeGroupBlockedMember", new { groupId, userId });
    public Task<ZaloApiResponse<JsonElement>> GetGroupBlockedMemberAsync(string groupId) => ApiClient.CallGetApiAsync("getGroupBlockedMember", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> DisperseGroupAsync(string groupId) => ApiClient.CallPostApiAsync("disperseGroup", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> EnableGroupLinkAsync(string groupId) => ApiClient.CallPostApiAsync("enableGroupLink", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> DisableGroupLinkAsync(string groupId) => ApiClient.CallPostApiAsync("disableGroupLink", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> GetGroupLinkInfoAsync(string groupId) => ApiClient.CallGetApiAsync("getGroupLinkInfo", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> GetGroupLinkDetailAsync(string groupCode) => ApiClient.CallGetApiAsync("getGroupLinkDetail", new { groupCode });
    public Task<ZaloApiResponse<JsonElement>> JoinGroupLinkAsync(string groupCode) => ApiClient.CallPostApiAsync("joinGroupLink", new { groupCode });
    public Task<ZaloApiResponse<JsonElement>> UpdateGroupSettingsAsync(string groupId, object settings) => ApiClient.CallPostApiAsync("updateGroupSettings", new { groupId, settings });
    public Task<ZaloApiResponse<JsonElement>> GetPendingGroupMembersAsync(string groupId) => ApiClient.CallGetApiAsync("getPendingGroupMembers", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> ReviewPendingMemberRequestAsync(string groupId, long userId, bool approve) => ApiClient.CallPostApiAsync("reviewPendingMemberRequest", new { groupId, userId, approve });
    public Task<ZaloApiResponse<JsonElement>> GetGroupInviteBoxInfoAsync(string code) => ApiClient.CallGetApiAsync("getGroupInviteBoxInfo", new { code });
    public Task<ZaloApiResponse<JsonElement>> GetGroupInviteBoxListAsync() => ApiClient.CallGetApiAsync("getGroupInviteBoxList");
    public Task<ZaloApiResponse<JsonElement>> JoinGroupInviteBoxAsync(string code) => ApiClient.CallPostApiAsync("joinGroupInviteBox", new { code });
    public Task<ZaloApiResponse<JsonElement>> DeleteGroupInviteBoxAsync(string code) => ApiClient.CallPostApiAsync("deleteGroupInviteBox", new { code });
    public Task<ZaloApiResponse<JsonElement>> UpgradeGroupToCommunityAsync(string groupId) => ApiClient.CallPostApiAsync("upgradeGroupToCommunity", new { groupId });

    // ─── Conversation APIs ───────────────────────────────────────────────
    public async Task<ZaloApiResponse<JsonElement>> GetConversationAsync()
    {
        try
        {
            if (_conversationCache != null && (DateTime.UtcNow - _conversationCacheTime).TotalSeconds < 60)
                return _conversationCache;

            List<JsonElement> convList = new();
            Dictionary<string, JsonElement> profiles = new();
            Dictionary<string, JsonElement> groupInfoDict = new();

            ZaloApiResponse<List<Models.ApiModels.getAllFriendsModel.ResponseModel>?> friendsResult = await GetAllFriendsAsync();
            if (friendsResult.IsSuccess)
            {
                foreach (Models.ApiModels.getAllFriendsModel.ResponseModel friend in friendsResult.Data)
                {
                    convList.Add(JsonSerializer.SerializeToElement(friend, _jsonOptions));
                    profiles[friend.userId!] = JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["displayName"] = friend.displayName ?? friend.userId }, _jsonOptions);
                }
            }

            ZaloApiResponse<JsonElement> groupsResult = await GetAllGroupsAsync();
            if (groupsResult.IsSuccess && groupsResult.Data.ValueKind == JsonValueKind.Object)
            {
                JsonElement gData = groupsResult.Data;
                if (gData.TryGetProperty("gridVerMap", out JsonElement gridMap) && gridMap.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty grp in gridMap.EnumerateObject())
                    {
                        string gid = grp.Name;
                        if (string.IsNullOrEmpty(gid)) continue;
                        convList.Add(JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["id"] = gid, ["type"] = 1, ["name"] = $"Group {gid}", ["lastMsg"] = "", ["lastTime"] = 0L, ["memberCount"] = 0 }, _jsonOptions));
                    }
                }
                else if (gData.TryGetProperty("data", out JsonElement gList) && gList.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement grp in gList.EnumerateArray())
                    {
                        string? gid = grp.TryGetProperty("groupId", out JsonElement gidEl) ? gidEl.GetString() : null;
                        if (string.IsNullOrEmpty(gid)) continue;
                        convList.Add(JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["id"] = gid, ["type"] = 1, ["name"] = $"Group {gid}", ["lastMsg"] = "", ["lastTime"] = 0L, ["memberCount"] = 0 }, _jsonOptions));
                    }
                }
            }

            Dictionary<string, object?> resultDict = new()
            {
                ["data"] = new Dictionary<string, object?> { ["conversations"] = convList, ["profiles"] = profiles, ["groupInfo"] = groupInfoDict }
            };

            ZaloApiResponse<JsonElement> result = new()
            { Data = JsonSerializer.SerializeToElement(resultDict, _jsonOptions), Error = null };
            _conversationCache = result;
            _conversationCacheTime = DateTime.UtcNow;
            //string r = result.Data.ToString();

            return result;
        }
        catch (Exception ex) { return new ZaloApiResponse<JsonElement> { Data = default, Error = ex.Message }; }
    }

    /// <summary>
    /// Get the real conversation list from Zalo's getContext API.
    /// Returns structured data with conversations, profiles, and groupInfo.
    /// Equivalent to zca-js's getContext().
    /// </summary>
    public Task<ZaloApiResponse<JsonElement>> GetContextAsync() => ApiClient.CallGetApiAsync("getContext");

    public Task<ZaloApiResponse<JsonElement>> GetArchivedChatListAsync() => ApiClient.CallGetApiAsync("getArchivedChatList");
    public Task<ZaloApiResponse<JsonElement>> UpdateArchivedChatListAsync(string threadId, bool archive, ThreadType threadType = ThreadType.User) => ApiClient.CallPostApiAsync("updateArchivedChatList", new { threadId, archive, threadType });
    public Task<ZaloApiResponse<JsonElement>> GetHiddenConversationsAsync() => ApiClient.CallGetApiAsync("getHiddenConversations");
    public Task<ZaloApiResponse<JsonElement>> SetHiddenConversationsAsync(List<string> threadIds) => ApiClient.CallPostApiAsync("setHiddenConversations", new { threadIds });
    public Task<ZaloApiResponse<JsonElement>> GetPinConversationsAsync() => ApiClient.CallGetApiAsync("getPinConversations");
    public Task<ZaloApiResponse<JsonElement>> SetPinnedConversationsAsync(List<string> threadIds) => ApiClient.CallPostApiAsync("setPinnedConversations", new { threadIds });
    public Task<ZaloApiResponse<JsonElement>> ResetHiddenConversPinAsync() => ApiClient.CallPostApiAsync("resetHiddenConversPin");
    public Task<ZaloApiResponse<JsonElement>> UpdateHiddenConversPinAsync(string threadId, bool hidden, bool pinned) => ApiClient.CallPostApiAsync("updateHiddenConversPin", new { threadId, hidden, pinned });
    public Task<ZaloApiResponse<JsonElement>> DeleteChatAsync(string threadId) => ApiClient.CallEncryptedPostApiAsync("deleteChat", new { threadId, clientId = GetTimestamp() });
    public Task<ZaloApiResponse<JsonElement>> AddUnreadMarkAsync(string threadId) => ApiClient.CallPostApiAsync("addUnreadMark", new { threadId });
    public Task<ZaloApiResponse<JsonElement>> RemoveUnreadMarkAsync(string threadId) => ApiClient.CallPostApiAsync("removeUnreadMark", new { threadId });
    public Task<ZaloApiResponse<JsonElement>> GetUnreadMarkAsync() => ApiClient.CallGetApiAsync("getUnreadMark");
    public Task<ZaloApiResponse<JsonElement>> GetAutoDeleteChatAsync(string threadId) => ApiClient.CallGetApiAsync("getAutoDeleteChat", new { threadId });
    public Task<ZaloApiResponse<JsonElement>> UpdateAutoDeleteChatAsync(string threadId, int duration) => ApiClient.CallPostApiAsync("updateAutoDeleteChat", new { threadId, duration });

    // ─── Sticker APIs (encrypted GET) ────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> GetStickersAsync(string keyword) => ApiClient.CallEncryptedGetApiAsync("getStickers", new { keyword, gif = 1, guggy = 0, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> GetStickersDetailAsync(int stickerId) => ApiClient.CallEncryptedGetApiAsync("getStickersDetail", new { sid = stickerId });
    public Task<ZaloApiResponse<JsonElement>> GetStickerCategoryDetailAsync(int categoryId) => ApiClient.CallEncryptedGetApiAsync("getStickerCategoryDetail", new { cid = categoryId });
    public Task<ZaloApiResponse<JsonElement>> SearchStickerAsync(string keyword) => ApiClient.CallEncryptedGetApiAsync("searchSticker", new { keyword, limit = 50, srcType = 0, imei = GetImei() });

    // ─── Poll APIs ───────────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> CreatePollAsync(string groupId, string question, List<string> options) => ApiClient.CallPostApiAsync("createPoll", new { groupId, question, options });
    public Task<ZaloApiResponse<JsonElement>> GetPollDetailAsync(string pollId) => ApiClient.CallGetApiAsync("getPollDetail", new { pollId });
    public Task<ZaloApiResponse<JsonElement>> AddPollOptionsAsync(string pollId, List<string> options) => ApiClient.CallPostApiAsync("addPollOptions", new { pollId, options });
    public Task<ZaloApiResponse<JsonElement>> VotePollAsync(string pollId, List<int> optionIds) => ApiClient.CallPostApiAsync("votePoll", new { pollId, optionIds });
    public Task<ZaloApiResponse<JsonElement>> LockPollAsync(string pollId) => ApiClient.CallPostApiAsync("lockPoll", new { pollId });
    public Task<ZaloApiResponse<JsonElement>> SharePollAsync(string pollId, string threadId, ThreadType threadType = ThreadType.User) => ApiClient.CallPostApiAsync("sharePoll", new { pollId, threadId, threadType });

    // ─── Reminder APIs ───────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> CreateReminderAsync(string groupId, string message, long remindTime) => ApiClient.CallPostApiAsync("createReminder", new { groupId, message, remindTime });
    public Task<ZaloApiResponse<JsonElement>> EditReminderAsync(string reminderId, string message, long remindTime) => ApiClient.CallPostApiAsync("editReminder", new { reminderId, message, remindTime });
    public Task<ZaloApiResponse<JsonElement>> RemoveReminderAsync(string reminderId) => ApiClient.CallPostApiAsync("removeReminder", new { reminderId });
    public Task<ZaloApiResponse<JsonElement>> GetReminderAsync(string reminderId) => ApiClient.CallGetApiAsync("getReminder", new { reminderId });
    public Task<ZaloApiResponse<JsonElement>> GetListReminderAsync(string groupId) => ApiClient.CallGetApiAsync("getListReminder", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> GetReminderResponsesAsync(string reminderId) => ApiClient.CallGetApiAsync("getReminderResponses", new { reminderId });

    // ─── Catalog APIs ────────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> CreateCatalogAsync(string name) => ApiClient.CallPostApiAsync("createCatalog", new { name });
    public Task<ZaloApiResponse<JsonElement>> UpdateCatalogAsync(string catalogId, string name) => ApiClient.CallPostApiAsync("updateCatalog", new { catalogId, name });
    public Task<ZaloApiResponse<JsonElement>> DeleteCatalogAsync(string catalogId) => ApiClient.CallPostApiAsync("deleteCatalog", new { catalogId });
    public Task<ZaloApiResponse<JsonElement>> GetCatalogListAsync() => ApiClient.CallGetApiAsync("getCatalogList");
    public Task<ZaloApiResponse<JsonElement>> CreateProductCatalogAsync(string catalogId, object product) => ApiClient.CallPostApiAsync("createProductCatalog", new { catalogId, product });
    public Task<ZaloApiResponse<JsonElement>> UpdateProductCatalogAsync(string productId, object product) => ApiClient.CallPostApiAsync("updateProductCatalog", new { productId, product });
    public Task<ZaloApiResponse<JsonElement>> DeleteProductCatalogAsync(string productId) => ApiClient.CallPostApiAsync("deleteProductCatalog", new { productId });
    public Task<ZaloApiResponse<JsonElement>> GetProductCatalogListAsync(string catalogId) => ApiClient.CallGetApiAsync("getProductCatalogList", new { catalogId });
    public Task<ZaloApiResponse<JsonElement>> UploadProductPhotoAsync(string productId, string imagePath) => ApiClient.CallPostApiAsync("uploadProductPhoto", new { productId, imagePath });

    // ─── Auto Reply APIs ────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> CreateAutoReplyAsync(object autoReplyData) => ApiClient.CallPostApiAsync("createAutoReply", autoReplyData);
    public Task<ZaloApiResponse<JsonElement>> UpdateAutoReplyAsync(string autoReplyId, object autoReplyData) => ApiClient.CallPostApiAsync("updateAutoReply", new { autoReplyId, autoReplyData });
    public Task<ZaloApiResponse<JsonElement>> DeleteAutoReplyAsync(string autoReplyId) => ApiClient.CallPostApiAsync("deleteAutoReply", new { autoReplyId });
    public Task<ZaloApiResponse<JsonElement>> GetAutoReplyListAsync() => ApiClient.CallGetApiAsync("getAutoReplyList");

    // ─── Quick Message APIs ─────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> AddQuickMessageAsync(string message) => ApiClient.CallPostApiAsync("addQuickMessage", new { message });
    public Task<ZaloApiResponse<JsonElement>> UpdateQuickMessageAsync(string quickMessageId, string message) => ApiClient.CallPostApiAsync("updateQuickMessage", new { quickMessageId, message });
    public Task<ZaloApiResponse<JsonElement>> RemoveQuickMessageAsync(string quickMessageId) => ApiClient.CallPostApiAsync("removeQuickMessage", new { quickMessageId });
    public Task<ZaloApiResponse<JsonElement>> GetQuickMessageListAsync() => ApiClient.CallGetApiAsync("getQuickMessageList");

    // ─── Board/Note APIs ────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> GetListBoardAsync(string groupId) => ApiClient.CallGetApiAsync("getListBoard", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> CreateNoteAsync(string groupId, string content) => ApiClient.CallPostApiAsync("createNote", new { groupId, content });
    public Task<ZaloApiResponse<JsonElement>> EditNoteAsync(string noteId, string content) => ApiClient.CallPostApiAsync("editNote", new { noteId, content });

    // ─── Label APIs ─────────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> GetLabelsAsync() => ApiClient.CallGetApiAsync("getLabels");
    public Task<ZaloApiResponse<JsonElement>> UpdateLabelsAsync(List<object> labels) => ApiClient.CallPostApiAsync("updateLabels", new { labels });

    // ─── Settings APIs ───────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> GetSettingsAsync() => ApiClient.CallGetApiAsync("getSettings");
    public Task<ZaloApiResponse<JsonElement>> UpdateSettingsAsync(object settings) => ApiClient.CallPostApiAsync("updateSettings", settings);
    public Task<ZaloApiResponse<JsonElement>> UpdateLangAsync(string language) => ApiClient.CallPostApiAsync("updateLang", new { language });
    public Task<ZaloApiResponse<JsonElement>> SetMuteAsync(string threadId, int muteDuration, ThreadType threadType = ThreadType.User) => ApiClient.CallPostApiAsync("setMute", new { threadId, muteDuration, threadType });
    public Task<ZaloApiResponse<JsonElement>> GetMuteAsync(string threadId, ThreadType threadType = ThreadType.User) => ApiClient.CallGetApiAsync("getMute", new { threadId, threadType });
    public Task<ZaloApiResponse<JsonElement>> UpdateActiveStatusAsync(bool isActive) => ApiClient.CallPostApiAsync("updateActiveStatus", new { isActive });
    public Task<ZaloApiResponse<JsonElement>> KeepAliveAsync() => ApiClient.CallGetApiAsync("keepAlive");
    public Task<ZaloApiResponse<JsonElement>> LastOnlineAsync(long userId) => ApiClient.CallGetApiAsync("lastOnline", new { userId });
    public Task<ZaloApiResponse<JsonElement>> GetQrAsync(string userId) => ApiClient.CallEncryptedPostApiAsync("getQR", new { fids = new[] { userId } });
    public Task<ZaloApiResponse<JsonElement>> GetCookieAsync() => ApiClient.CallGetApiAsync("getCookie");
    public Task<ZaloApiResponse<JsonElement>> ParseLinkAsync(string url) => ApiClient.CallEncryptedGetApiAsync("parseLink", new { link = url, version = 1, imei = GetImei() });

    // ─── Report API (encrypted POST for both user/group) ─────────────────
    public Task<ZaloApiResponse<JsonElement>> SendReportAsync(string threadId, int reason, string? content = null, ThreadType threadType = ThreadType.User)
    {
        if (threadType == ThreadType.Group)
            return ApiClient.CallEncryptedPostApiAsync("sendReportGroup",
                new { uidTo = threadId, type = 14, reason, content = content ?? "", imei = GetImei() });
        else
            return ApiClient.CallEncryptedPostApiAsync("sendReport",
                new { idTo = threadId, objId = "person.profile", reason = reason.ToString(), content });
    }

    public Task<ZaloApiResponse<JsonElement>> GetBizAccountAsync() => ApiClient.CallGetApiAsync("getBizAccount");
    public Task<ZaloApiResponse<JsonElement>> CustomApiCallAsync(string method, string endpoint, object? data = null, bool isGet = true)
        => ApiClient.CallCustomApiAsync(method, endpoint, data, isGet);
}