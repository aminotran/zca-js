using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ICU.Lib.ZaloClientWeb.Auth;
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
    internal ZaloContext Context => _context;
    internal HttpClient HttpClient => _httpClient;

    internal WebSocket.ZaloListener? _listener;

    private ZaloApiResponse<JsonElement>? _conversationCache;
    private DateTime _conversationCacheTime;

    public ZaloApi(ZaloContext context, HttpClient httpClient)
    {
        _context = context;
        _httpClient = httpClient;
    }

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
    public Task<ZaloApiResponse<JsonElement>> GetUserInfoAsync(long userId) => ApiMethods.CallGetApiAsync(_context, _httpClient, "getUserInfo", new { userId });
    public Task<ZaloApiResponse<JsonElement>> FindUserAsync(string phoneNumber) => ApiMethods.CallGetApiAsync(_context, _httpClient, "findUser", new { phoneNumber });
    public Task<ZaloApiResponse<JsonElement>> FindUserByUsernameAsync(string username) => ApiMethods.CallGetApiAsync(_context, _httpClient, "findUserByUsername", new { username });
    public Task<ZaloApiResponse<JsonElement>> GetAccountInfoAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "fetchAccountInfo");
    public long GetOwnId() => _context.Uid;
    public Task<ZaloApiResponse<long>> GetOwnIdAsync() => Task.FromResult(new ZaloApiResponse<long> { Data = _context.Uid });
    public Task<ZaloApiResponse<JsonElement>> UpdateProfileAsync(object profileData) => ApiMethods.CallPostApiAsync(_context, _httpClient, "updateProfile", profileData);
    public Task<ZaloApiResponse<JsonElement>> UpdateProfileBioAsync(string bio) => ApiMethods.CallPostApiAsync(_context, _httpClient, "updateProfileBio", new { bio });
    public Task<ZaloApiResponse<JsonElement>> ChangeAccountAvatarAsync(string imagePath) => ApiMethods.CallPostApiAsync(_context, _httpClient, "changeAccountAvatar", new { imagePath });
    public Task<ZaloApiResponse<JsonElement>> GetAvatarListAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getAvatarList");
    public Task<ZaloApiResponse<JsonElement>> GetFullAvatarAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getFullAvatar");
    public Task<ZaloApiResponse<JsonElement>> DeleteAvatarAsync(long avatarId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "deleteAvatar", new { avatarId });
    public Task<ZaloApiResponse<JsonElement>> ReuseAvatarAsync(long avatarId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "reuseAvatar", new { avatarId });
    public Task<ZaloApiResponse<JsonElement>> GetAvatarUrlProfileAsync(long userId) => ApiMethods.CallGetApiAsync(_context, _httpClient, "getAvatarUrlProfile", new { userId });

    // ─── Friend APIs (all encrypted POST) ─────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> GetAllFriendsAsync() => ApiMethods.CallEncryptedGetApiAsync(_context, _httpClient, "getAllFriends", new { incInvalid = 1, page = 1, count = 20000, avatar_size = 120, actiontime = 0, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> GetFriendRequestStatusAsync(long friendId) => ApiMethods.CallEncryptedGetApiAsync(_context, _httpClient, "getFriendRequestStatus", new { fid = friendId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> SendFriendRequestAsync(long userId, string? message = null) => ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendFriendRequest", new { userId, imei = GetImei(), msg = message ?? "" });
    public Task<ZaloApiResponse<JsonElement>> AcceptFriendRequestAsync(long userId) => ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "acceptFriendRequest", new { fid = userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> RejectFriendRequestAsync(long userId) => ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "rejectFriendRequest", new { fid = userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> RemoveFriendAsync(long userId) => ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "removeFriend", new { fid = userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> UndoFriendRequestAsync(long userId) => ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "undoFriendRequest", new { fid = userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> BlockUserAsync(long userId) => ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "blockUser", new { fid = userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> UnblockUserAsync(long userId) => ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "unblockUser", new { fid = userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> BlockViewFeedAsync(long userId) => ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "blockViewFeed", new { fid = userId, imei = GetImei(), blockType = 1 });
    public Task<ZaloApiResponse<JsonElement>> GetFriendBoardListAsync(long userId) => ApiMethods.CallGetApiAsync(_context, _httpClient, "getFriendBoardList", new { userId });
    public Task<ZaloApiResponse<JsonElement>> GetFriendOnlinesAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getFriendOnlines");
    public Task<ZaloApiResponse<JsonElement>> GetFriendRecommendationsAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getFriendRecommendations");
    public Task<ZaloApiResponse<JsonElement>> ChangeFriendAliasAsync(long userId, string alias) => ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "changeFriendAlias", new { userId, alias, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> RemoveFriendAliasAsync(long userId) => ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "removeFriendAlias", new { userId, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> GetSentFriendRequestAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getSentFriendRequest");
    public Task<ZaloApiResponse<JsonElement>> GetCloseFriendsAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getCloseFriends");
    public Task<ZaloApiResponse<JsonElement>> GetAliasListAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getAliasList");
    public Task<ZaloApiResponse<JsonElement>> GetRelatedFriendGroupAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getRelatedFriendGroup");
    public Task<ZaloApiResponse<JsonElement>> GetMultiUsersByPhonesAsync(List<string> phones) => ApiMethods.CallPostApiAsync(_context, _httpClient, "getMultiUsersByPhones", new { phones });
    public Task<ZaloApiResponse<JsonElement>> InviteUserToGroupsAsync(long userId, List<string> groupIds) => ApiMethods.CallPostApiAsync(_context, _httpClient, "inviteUserToGroups", new { userId, groupIds });

    // ─── Message APIs (all encrypted POST) ────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> SendMessageAsync(string threadId, string message, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        if (threadType == ThreadType.Group)
            return ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendMessageGroup",
                new { message, clientId = ts, ttl = 0, grid = threadId, visibility = 0 });
        else
            return ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendMessage",
                new { message, clientId = ts, ttl = 0, toid = threadId, imei = GetImei() });
    }

    public Task<ZaloApiResponse<JsonElement>> SendStickerAsync(string threadId, int stickerId, int stickerCategoryId, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        if (threadType == ThreadType.Group)
            return ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendStickerGroup",
                new { stickerId, cateId = stickerCategoryId, clientId = ts, ttl = 0, grid = threadId, visibility = 0 });
        else
            return ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendSticker",
                new { stickerId, cateId = stickerCategoryId, clientId = ts, ttl = 0, toid = threadId, imei = GetImei() });
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
            var data = parseResult.Data;
            href = TryGetString(data, "href") ?? link;
            src = TryGetString(data, "src") ?? "";
            title = TryGetString(data, "title") ?? "";
            desc = TryGetString(data, "desc") ?? "";
            thumb = TryGetString(data, "thumb") ?? "";
            media = TryGetString(data, "media") ?? "[]";
        }

        var finalMsg = !string.IsNullOrEmpty(msg) ? (msg!.Contains(link) ? msg : msg + " " + link) : link;
        if (threadType == ThreadType.Group)
            return await ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendLinkGroup",
                new { msg = finalMsg, href, src, title, desc, thumb, type = 2, media, ttl = 0, clientId = ts, grid = threadId, imei = GetImei() });
        else
            return await ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendLink",
                new { msg = finalMsg, href, src, title, desc, thumb, type = 2, media, ttl = 0, clientId = ts, toId = threadId });
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
        });

        if (threadType == ThreadType.Group)
            return await ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendVideoGroup",
                new { grid = threadId, visibility = 0, clientId = ts.ToString(), ttl = 0, zsource = 704, msgType = 5, msgInfo, imei = GetImei() });
        else
            return await ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendVideo",
                new { toId = threadId, clientId = ts.ToString(), ttl = 0, zsource = 704, msgType = 5, msgInfo, imei = GetImei() });
    }

    public async Task<ZaloApiResponse<JsonElement>> SendVoiceAsync(string threadId, string voiceUrl, int duration = 0, string? msg = null, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        var msgInfo = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["voiceUrl"] = voiceUrl,
            ["duration"] = duration,
            ["title"] = msg ?? ""
        });

        if (threadType == ThreadType.Group)
            return await ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendVoiceGroup",
                new { grid = threadId, visibility = 0, clientId = ts.ToString(), ttl = 0, zsource = 704, msgType = 6, msgInfo, imei = GetImei() });
        else
            return await ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendVoice",
                new { toId = threadId, clientId = ts.ToString(), ttl = 0, zsource = 704, msgType = 6, msgInfo, imei = GetImei() });
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
        });
        if (threadType == ThreadType.Group)
            return await ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendCardGroup",
                new { ttl = 0, msgType = 6, clientId, msgInfo, visibility = 0, grid = threadId });
        else
            return await ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendCard",
                new { ttl = 0, msgType = 6, clientId, msgInfo, toId = threadId, imei = GetImei() });
    }
    public Task<ZaloApiResponse<JsonElement>> SendBankCardAsync(string threadId, object cardData, ThreadType threadType = ThreadType.User) => ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendBankCard", new { threadId, cardData, threadType });

    public Task<ZaloApiResponse<JsonElement>> ForwardMessageAsync(string threadId, long messageId, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        if (threadType == ThreadType.Group)
            return ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "forwardMessage",
                new { msgId = messageId, clientId = ts, grid = threadId, visibility = 0 });
        else
            return ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "forwardMessage",
                new { msgId = messageId, clientId = ts, toid = threadId, imei = GetImei() });
    }

    public Task<ZaloApiResponse<JsonElement>> DeleteMessageAsync(string messageId, string ownerId, bool onlyMe = false, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        var endpoint = threadType == ThreadType.Group ? "deleteMessageGroup" : "deleteMessage";
        if (threadType == ThreadType.Group)
            return ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, endpoint,
                new { grid = "", cliMsgId = ts, msgs = new[] { new { cliMsgId = "0", globalMsgId = messageId, ownerId, destId = "" } }, onlyMe = onlyMe ? 1 : 0 });
        else
            return ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, endpoint,
                new { toid = "", cliMsgId = ts, msgs = new[] { new { cliMsgId = "0", globalMsgId = messageId, ownerId, destId = "" } }, onlyMe = onlyMe ? 1 : 0, imei = GetImei() });
    }

    public Task<ZaloApiResponse<JsonElement>> UndoAsync(string messageId, string ownerId, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        if (threadType == ThreadType.Group)
            return ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "undo",
                new { msgId = messageId, clientId = ts, cliMsgId = "0", uidFrom = ownerId, idTo = "" });
        else
            return ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "undo",
                new { msgId = messageId, clientId = ts, cliMsgId = "0", uidFrom = ownerId, idTo = "" });
    }

    public Task<ZaloApiResponse<JsonElement>> SendTypingEventAsync(string threadId, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        if (threadType == ThreadType.Group)
            return ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendTypingEventGroup",
                new { msgType = 1, clientId = ts, uid = _context.Uid.ToString(), threadId, isGroup = 1 });
        else
            return ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendTypingEvent",
                new { msgType = 1, clientId = ts, uid = _context.Uid.ToString(), threadId, isGroup = 0 });
    }

    public Task<ZaloApiResponse<JsonElement>> SendSeenEventAsync(string threadId, long messageId, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        if (threadType == ThreadType.Group)
            return ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendSeenEventGroup",
                new { messageId, clientId = ts, grid = threadId, visibility = 0 });
        else
            return ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendSeenEvent",
                new { messageId, clientId = ts, toid = threadId, imei = GetImei() });
    }

    public Task<ZaloApiResponse<JsonElement>> SendDeliveredEventAsync(string threadId, long messageId, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        if (threadType == ThreadType.Group)
            return ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendDeliveredEventGroup",
                new { messageId, clientId = ts, grid = threadId, visibility = 0 });
        else
            return ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendDeliveredEvent",
                new { messageId, clientId = ts, toid = threadId, imei = GetImei() });
    }

    public Task<ZaloApiResponse<JsonElement>> AddReactionAsync(string messageId, string reactionIcon, ThreadType threadType = ThreadType.User)
    {
        var ts = GetTimestamp();
        return ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "addReaction",
            new { react_list = new[] { new { msgId = messageId, reactIcon = reactionIcon, reactType = 0 } }, clientId = ts, imei = GetImei() });
    }

    public Task<ZaloApiResponse<JsonElement>> UploadAttachmentAsync(string filePath) => Task.FromResult(new ZaloApiResponse<JsonElement> { Error = "Not implemented: file upload required" });

    // ─── Group APIs ──────────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> CreateGroupAsync(string name, List<long> memberIds) => ApiMethods.CallPostApiAsync(_context, _httpClient, "createGroup", new { name, memberIds });
    public Task<ZaloApiResponse<JsonElement>> GetAllGroupsAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getAllGroups");
    public Task<ZaloApiResponse<JsonElement>> GetGroupInfoAsync(string groupId) => ApiMethods.CallGetApiAsync(_context, _httpClient, "getGroupInfo", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> GetGroupMembersInfoAsync(string groupId) => ApiMethods.CallGetApiAsync(_context, _httpClient, "getGroupMembersInfo", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> GetGroupChatHistoryAsync(string groupId) => ApiMethods.CallGetApiAsync(_context, _httpClient, "getGroupChatHistory", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> AddUserToGroupAsync(string groupId, long userId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "addUserToGroup", new { groupId, userId });
    public Task<ZaloApiResponse<JsonElement>> RemoveUserFromGroupAsync(string groupId, long userId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "removeUserFromGroup", new { groupId, userId });
    public Task<ZaloApiResponse<JsonElement>> LeaveGroupAsync(string groupId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "leaveGroup", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> ChangeGroupNameAsync(string groupId, string name) => ApiMethods.CallPostApiAsync(_context, _httpClient, "changeGroupName", new { groupId, name });
    public Task<ZaloApiResponse<JsonElement>> ChangeGroupAvatarAsync(string groupId, string imagePath) => ApiMethods.CallPostApiAsync(_context, _httpClient, "changeGroupAvatar", new { groupId, imagePath });
    public Task<ZaloApiResponse<JsonElement>> ChangeGroupOwnerAsync(string groupId, long newOwnerId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "changeGroupOwner", new { groupId, newOwnerId });
    public Task<ZaloApiResponse<JsonElement>> AddGroupDeputyAsync(string groupId, long userId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "addGroupDeputy", new { groupId, userId });
    public Task<ZaloApiResponse<JsonElement>> RemoveGroupDeputyAsync(string groupId, long userId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "removeGroupDeputy", new { groupId, userId });
    public Task<ZaloApiResponse<JsonElement>> AddGroupBlockedMemberAsync(string groupId, long userId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "addGroupBlockedMember", new { groupId, userId });
    public Task<ZaloApiResponse<JsonElement>> RemoveGroupBlockedMemberAsync(string groupId, long userId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "removeGroupBlockedMember", new { groupId, userId });
    public Task<ZaloApiResponse<JsonElement>> GetGroupBlockedMemberAsync(string groupId) => ApiMethods.CallGetApiAsync(_context, _httpClient, "getGroupBlockedMember", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> DisperseGroupAsync(string groupId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "disperseGroup", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> EnableGroupLinkAsync(string groupId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "enableGroupLink", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> DisableGroupLinkAsync(string groupId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "disableGroupLink", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> GetGroupLinkInfoAsync(string groupId) => ApiMethods.CallGetApiAsync(_context, _httpClient, "getGroupLinkInfo", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> GetGroupLinkDetailAsync(string groupCode) => ApiMethods.CallGetApiAsync(_context, _httpClient, "getGroupLinkDetail", new { groupCode });
    public Task<ZaloApiResponse<JsonElement>> JoinGroupLinkAsync(string groupCode) => ApiMethods.CallPostApiAsync(_context, _httpClient, "joinGroupLink", new { groupCode });
    public Task<ZaloApiResponse<JsonElement>> UpdateGroupSettingsAsync(string groupId, object settings) => ApiMethods.CallPostApiAsync(_context, _httpClient, "updateGroupSettings", new { groupId, settings });
    public Task<ZaloApiResponse<JsonElement>> GetPendingGroupMembersAsync(string groupId) => ApiMethods.CallGetApiAsync(_context, _httpClient, "getPendingGroupMembers", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> ReviewPendingMemberRequestAsync(string groupId, long userId, bool approve) => ApiMethods.CallPostApiAsync(_context, _httpClient, "reviewPendingMemberRequest", new { groupId, userId, approve });
    public Task<ZaloApiResponse<JsonElement>> GetGroupInviteBoxInfoAsync(string code) => ApiMethods.CallGetApiAsync(_context, _httpClient, "getGroupInviteBoxInfo", new { code });
    public Task<ZaloApiResponse<JsonElement>> GetGroupInviteBoxListAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getGroupInviteBoxList");
    public Task<ZaloApiResponse<JsonElement>> JoinGroupInviteBoxAsync(string code) => ApiMethods.CallPostApiAsync(_context, _httpClient, "joinGroupInviteBox", new { code });
    public Task<ZaloApiResponse<JsonElement>> DeleteGroupInviteBoxAsync(string code) => ApiMethods.CallPostApiAsync(_context, _httpClient, "deleteGroupInviteBox", new { code });
    public Task<ZaloApiResponse<JsonElement>> UpgradeGroupToCommunityAsync(string groupId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "upgradeGroupToCommunity", new { groupId });

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

                    convList.Add(JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["id"] = userId, ["type"] = 0, ["name"] = displayName ?? userId, ["lastMsg"] = "", ["lastTime"] = 0L }));
                    profiles[userId!] = JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["displayName"] = displayName ?? userId });
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
                        convList.Add(JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["id"] = gid, ["type"] = 1, ["name"] = $"Group {gid}", ["lastMsg"] = "", ["lastTime"] = 0L, ["memberCount"] = 0 }));
                    }
                }
                else if (gData.TryGetProperty("data", out var gList) && gList.ValueKind == JsonValueKind.Array)
                {
                    foreach (var grp in gList.EnumerateArray())
                    {
                        var gid = grp.TryGetProperty("groupId", out var gidEl) ? gidEl.GetString() : null;
                        if (string.IsNullOrEmpty(gid)) continue;
                        convList.Add(JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["id"] = gid, ["type"] = 1, ["name"] = $"Group {gid}", ["lastMsg"] = "", ["lastTime"] = 0L, ["memberCount"] = 0 }));
                    }
                }
            }

            var resultDict = new Dictionary<string, object?>
            {
                ["data"] = new Dictionary<string, object?> { ["conversations"] = convList, ["profiles"] = profiles, ["groupInfo"] = groupInfoDict }
            };

            var result = new ZaloApiResponse<JsonElement> { Data = JsonSerializer.SerializeToElement(resultDict), Error = null };
            _conversationCache = result;
            _conversationCacheTime = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex) { return new ZaloApiResponse<JsonElement> { Data = default, Error = ex.Message }; }
    }

    public Task<ZaloApiResponse<JsonElement>> GetArchivedChatListAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getArchivedChatList");
    public Task<ZaloApiResponse<JsonElement>> UpdateArchivedChatListAsync(string threadId, bool archive, ThreadType threadType = ThreadType.User) => ApiMethods.CallPostApiAsync(_context, _httpClient, "updateArchivedChatList", new { threadId, archive, threadType });
    public Task<ZaloApiResponse<JsonElement>> GetHiddenConversationsAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getHiddenConversations");
    public Task<ZaloApiResponse<JsonElement>> SetHiddenConversationsAsync(List<string> threadIds) => ApiMethods.CallPostApiAsync(_context, _httpClient, "setHiddenConversations", new { threadIds });
    public Task<ZaloApiResponse<JsonElement>> GetPinConversationsAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getPinConversations");
    public Task<ZaloApiResponse<JsonElement>> SetPinnedConversationsAsync(List<string> threadIds) => ApiMethods.CallPostApiAsync(_context, _httpClient, "setPinnedConversations", new { threadIds });
    public Task<ZaloApiResponse<JsonElement>> ResetHiddenConversPinAsync() => ApiMethods.CallPostApiAsync(_context, _httpClient, "resetHiddenConversPin");
    public Task<ZaloApiResponse<JsonElement>> UpdateHiddenConversPinAsync(string threadId, bool hidden, bool pinned) => ApiMethods.CallPostApiAsync(_context, _httpClient, "updateHiddenConversPin", new { threadId, hidden, pinned });
    public Task<ZaloApiResponse<JsonElement>> DeleteChatAsync(string threadId) => ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "deleteChat", new { threadId, clientId = GetTimestamp() });
    public Task<ZaloApiResponse<JsonElement>> AddUnreadMarkAsync(string threadId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "addUnreadMark", new { threadId });
    public Task<ZaloApiResponse<JsonElement>> RemoveUnreadMarkAsync(string threadId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "removeUnreadMark", new { threadId });
    public Task<ZaloApiResponse<JsonElement>> GetUnreadMarkAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getUnreadMark");
    public Task<ZaloApiResponse<JsonElement>> GetAutoDeleteChatAsync(string threadId) => ApiMethods.CallGetApiAsync(_context, _httpClient, "getAutoDeleteChat", new { threadId });
    public Task<ZaloApiResponse<JsonElement>> UpdateAutoDeleteChatAsync(string threadId, int duration) => ApiMethods.CallPostApiAsync(_context, _httpClient, "updateAutoDeleteChat", new { threadId, duration });

    // ─── Sticker APIs (encrypted GET) ────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> GetStickersAsync(string keyword) => ApiMethods.CallEncryptedGetApiAsync(_context, _httpClient, "getStickers", new { keyword, gif = 1, guggy = 0, imei = GetImei() });
    public Task<ZaloApiResponse<JsonElement>> GetStickersDetailAsync(int stickerId) => ApiMethods.CallEncryptedGetApiAsync(_context, _httpClient, "getStickersDetail", new { sid = stickerId });
    public Task<ZaloApiResponse<JsonElement>> GetStickerCategoryDetailAsync(int categoryId) => ApiMethods.CallEncryptedGetApiAsync(_context, _httpClient, "getStickerCategoryDetail", new { cid = categoryId });
    public Task<ZaloApiResponse<JsonElement>> SearchStickerAsync(string keyword) => ApiMethods.CallEncryptedGetApiAsync(_context, _httpClient, "searchSticker", new { keyword, limit = 50, srcType = 0, imei = GetImei() });

    // ─── Poll APIs ───────────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> CreatePollAsync(string groupId, string question, List<string> options) => ApiMethods.CallPostApiAsync(_context, _httpClient, "createPoll", new { groupId, question, options });
    public Task<ZaloApiResponse<JsonElement>> GetPollDetailAsync(string pollId) => ApiMethods.CallGetApiAsync(_context, _httpClient, "getPollDetail", new { pollId });
    public Task<ZaloApiResponse<JsonElement>> AddPollOptionsAsync(string pollId, List<string> options) => ApiMethods.CallPostApiAsync(_context, _httpClient, "addPollOptions", new { pollId, options });
    public Task<ZaloApiResponse<JsonElement>> VotePollAsync(string pollId, List<int> optionIds) => ApiMethods.CallPostApiAsync(_context, _httpClient, "votePoll", new { pollId, optionIds });
    public Task<ZaloApiResponse<JsonElement>> LockPollAsync(string pollId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "lockPoll", new { pollId });
    public Task<ZaloApiResponse<JsonElement>> SharePollAsync(string pollId, string threadId, ThreadType threadType = ThreadType.User) => ApiMethods.CallPostApiAsync(_context, _httpClient, "sharePoll", new { pollId, threadId, threadType });

    // ─── Reminder APIs ───────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> CreateReminderAsync(string groupId, string message, long remindTime) => ApiMethods.CallPostApiAsync(_context, _httpClient, "createReminder", new { groupId, message, remindTime });
    public Task<ZaloApiResponse<JsonElement>> EditReminderAsync(string reminderId, string message, long remindTime) => ApiMethods.CallPostApiAsync(_context, _httpClient, "editReminder", new { reminderId, message, remindTime });
    public Task<ZaloApiResponse<JsonElement>> RemoveReminderAsync(string reminderId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "removeReminder", new { reminderId });
    public Task<ZaloApiResponse<JsonElement>> GetReminderAsync(string reminderId) => ApiMethods.CallGetApiAsync(_context, _httpClient, "getReminder", new { reminderId });
    public Task<ZaloApiResponse<JsonElement>> GetListReminderAsync(string groupId) => ApiMethods.CallGetApiAsync(_context, _httpClient, "getListReminder", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> GetReminderResponsesAsync(string reminderId) => ApiMethods.CallGetApiAsync(_context, _httpClient, "getReminderResponses", new { reminderId });

    // ─── Catalog APIs ────────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> CreateCatalogAsync(string name) => ApiMethods.CallPostApiAsync(_context, _httpClient, "createCatalog", new { name });
    public Task<ZaloApiResponse<JsonElement>> UpdateCatalogAsync(string catalogId, string name) => ApiMethods.CallPostApiAsync(_context, _httpClient, "updateCatalog", new { catalogId, name });
    public Task<ZaloApiResponse<JsonElement>> DeleteCatalogAsync(string catalogId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "deleteCatalog", new { catalogId });
    public Task<ZaloApiResponse<JsonElement>> GetCatalogListAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getCatalogList");
    public Task<ZaloApiResponse<JsonElement>> CreateProductCatalogAsync(string catalogId, object product) => ApiMethods.CallPostApiAsync(_context, _httpClient, "createProductCatalog", new { catalogId, product });
    public Task<ZaloApiResponse<JsonElement>> UpdateProductCatalogAsync(string productId, object product) => ApiMethods.CallPostApiAsync(_context, _httpClient, "updateProductCatalog", new { productId, product });
    public Task<ZaloApiResponse<JsonElement>> DeleteProductCatalogAsync(string productId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "deleteProductCatalog", new { productId });
    public Task<ZaloApiResponse<JsonElement>> GetProductCatalogListAsync(string catalogId) => ApiMethods.CallGetApiAsync(_context, _httpClient, "getProductCatalogList", new { catalogId });
    public Task<ZaloApiResponse<JsonElement>> UploadProductPhotoAsync(string productId, string imagePath) => ApiMethods.CallPostApiAsync(_context, _httpClient, "uploadProductPhoto", new { productId, imagePath });

    // ─── Auto Reply APIs ────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> CreateAutoReplyAsync(object autoReplyData) => ApiMethods.CallPostApiAsync(_context, _httpClient, "createAutoReply", autoReplyData);
    public Task<ZaloApiResponse<JsonElement>> UpdateAutoReplyAsync(string autoReplyId, object autoReplyData) => ApiMethods.CallPostApiAsync(_context, _httpClient, "updateAutoReply", new { autoReplyId, autoReplyData });
    public Task<ZaloApiResponse<JsonElement>> DeleteAutoReplyAsync(string autoReplyId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "deleteAutoReply", new { autoReplyId });
    public Task<ZaloApiResponse<JsonElement>> GetAutoReplyListAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getAutoReplyList");

    // ─── Quick Message APIs ─────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> AddQuickMessageAsync(string message) => ApiMethods.CallPostApiAsync(_context, _httpClient, "addQuickMessage", new { message });
    public Task<ZaloApiResponse<JsonElement>> UpdateQuickMessageAsync(string quickMessageId, string message) => ApiMethods.CallPostApiAsync(_context, _httpClient, "updateQuickMessage", new { quickMessageId, message });
    public Task<ZaloApiResponse<JsonElement>> RemoveQuickMessageAsync(string quickMessageId) => ApiMethods.CallPostApiAsync(_context, _httpClient, "removeQuickMessage", new { quickMessageId });
    public Task<ZaloApiResponse<JsonElement>> GetQuickMessageListAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getQuickMessageList");

    // ─── Board/Note APIs ────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> GetListBoardAsync(string groupId) => ApiMethods.CallGetApiAsync(_context, _httpClient, "getListBoard", new { groupId });
    public Task<ZaloApiResponse<JsonElement>> CreateNoteAsync(string groupId, string content) => ApiMethods.CallPostApiAsync(_context, _httpClient, "createNote", new { groupId, content });
    public Task<ZaloApiResponse<JsonElement>> EditNoteAsync(string noteId, string content) => ApiMethods.CallPostApiAsync(_context, _httpClient, "editNote", new { noteId, content });

    // ─── Label APIs ─────────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> GetLabelsAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getLabels");
    public Task<ZaloApiResponse<JsonElement>> UpdateLabelsAsync(List<object> labels) => ApiMethods.CallPostApiAsync(_context, _httpClient, "updateLabels", new { labels });

    // ─── Settings APIs ───────────────────────────────────────────────────
    public Task<ZaloApiResponse<JsonElement>> GetSettingsAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getSettings");
    public Task<ZaloApiResponse<JsonElement>> UpdateSettingsAsync(object settings) => ApiMethods.CallPostApiAsync(_context, _httpClient, "updateSettings", settings);
    public Task<ZaloApiResponse<JsonElement>> UpdateLangAsync(string language) => ApiMethods.CallPostApiAsync(_context, _httpClient, "updateLang", new { language });
    public Task<ZaloApiResponse<JsonElement>> SetMuteAsync(string threadId, int muteDuration, ThreadType threadType = ThreadType.User) => ApiMethods.CallPostApiAsync(_context, _httpClient, "setMute", new { threadId, muteDuration, threadType });
    public Task<ZaloApiResponse<JsonElement>> GetMuteAsync(string threadId, ThreadType threadType = ThreadType.User) => ApiMethods.CallGetApiAsync(_context, _httpClient, "getMute", new { threadId, threadType });
    public Task<ZaloApiResponse<JsonElement>> UpdateActiveStatusAsync(bool isActive) => ApiMethods.CallPostApiAsync(_context, _httpClient, "updateActiveStatus", new { isActive });
    public Task<ZaloApiResponse<JsonElement>> KeepAliveAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "keepAlive");
    public Task<ZaloApiResponse<JsonElement>> LastOnlineAsync(long userId) => ApiMethods.CallGetApiAsync(_context, _httpClient, "lastOnline", new { userId });
    public Task<ZaloApiResponse<JsonElement>> GetQrAsync(string userId) => ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "getQR", new { fids = new[] { userId } });
    public Task<ZaloApiResponse<JsonElement>> GetCookieAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getCookie");
    public Task<ZaloApiResponse<JsonElement>> ParseLinkAsync(string url) => ApiMethods.CallGetApiAsync(_context, _httpClient, "parseLink", new { url });

    // ─── Report API (encrypted POST for both user/group) ─────────────────
    public Task<ZaloApiResponse<JsonElement>> SendReportAsync(string threadId, int reason, string? content = null, ThreadType threadType = ThreadType.User)
    {
        if (threadType == ThreadType.Group)
            return ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendReportGroup",
                new { uidTo = threadId, type = 14, reason, content = content ?? "", imei = GetImei() });
        else
            return ApiMethods.CallEncryptedPostApiAsync(_context, _httpClient, "sendReport",
                new { idTo = threadId, objId = "person.profile", reason = reason.ToString(), content });
    }

    public Task<ZaloApiResponse<JsonElement>> GetBizAccountAsync() => ApiMethods.CallGetApiAsync(_context, _httpClient, "getBizAccount");
    public Task<ZaloApiResponse<JsonElement>> CustomApiCallAsync(string method, string endpoint, object? data = null, bool isGet = true)
        => ApiMethods.CallCustomApiAsync(_context, _httpClient, method, endpoint, data, isGet);
}