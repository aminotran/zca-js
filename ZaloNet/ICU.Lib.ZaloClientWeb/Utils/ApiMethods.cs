using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ICU.Lib.ZaloClientWeb.Crypto;
using ICU.Lib.ZaloClientWeb.Models;

namespace ICU.Lib.ZaloClientWeb.Utils;

public static class ApiMethods
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Maps endpoint name to service key (used to resolve the base host via ZpwServiceMapV3).
    /// </summary>
    private static readonly Dictionary<string, string> EndpointToServiceMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fetchAccountInfo"] = "profile", ["updateProfile"] = "profile",
        ["updateProfileBio"] = "profile", ["changeAccountAvatar"] = "file",
        ["getAvatarList"] = "profile", ["getFullAvatar"] = "profile",
        ["deleteAvatar"] = "profile", ["reuseAvatar"] = "profile",
        ["getAvatarUrlProfile"] = "profile", ["findUser"] = "friend",
        ["findUserByUsername"] = "friend", ["getUserInfo"] = "profile",
        ["getAllFriends"] = "profile", ["getSettings"] = "profile",
        ["updateSettings"] = "profile", ["updateLang"] = "profile",
        ["updateActiveStatus"] = "profile", ["getQR"] = "friend",
        ["getCookie"] = "profile", ["getBizAccount"] = "profile",
        ["sendReport"] = "profile", ["lastOnline"] = "profile",
        ["getMute"] = "profile", ["setMute"] = "profile",
        ["getCloseFriends"] = "profile",

        ["acceptFriendRequest"] = "friend", ["rejectFriendRequest"] = "friend",
        ["removeFriend"] = "friend", ["undoFriendRequest"] = "friend",
        ["blockUser"] = "friend", ["unblockUser"] = "friend",
        ["blockViewFeed"] = "friend", ["getFriendOnlines"] = "profile",
        ["getFriendRecommendations"] = "friend", ["changeFriendAlias"] = "alias",
        ["removeFriendAlias"] = "alias", ["getSentFriendRequest"] = "friend",
        ["getRelatedFriendGroup"] = "friend", ["getMultiUsersByPhones"] = "friend",
        ["sendMessageGroup"] = "group", ["inviteUserToGroups"] = "group", ["getAliasList"] = "alias",
        ["sendFriendRequest"] = "friend", ["getFriendRequestStatus"] = "friend",
        ["getFriendBoardList"] = "friend_board",

        ["getContext"] = "conversation", ["getArchivedChatList"] = "label",
        ["updateArchivedChatList"] = "label", ["getHiddenConversations"] = "conversation",
        ["setHiddenConversations"] = "conversation", ["getPinConversations"] = "conversation",
        ["setPinnedConversations"] = "conversation", ["resetHiddenConversPin"] = "conversation",
        ["updateHiddenConversPin"] = "conversation", ["deleteChat"] = "conversation",
        ["addUnreadMark"] = "conversation", ["removeUnreadMark"] = "conversation",
        ["getUnreadMark"] = "conversation", ["getAutoDeleteChat"] = "conversation",
        ["updateAutoDeleteChat"] = "conversation", ["setMuteConversation"] = "conversation",
        ["getMuteConversation"] = "conversation",

        ["createGroup"] = "group", ["getAllGroups"] = "group_poll",
        ["getGroupInfo"] = "group", ["getGroupMembersInfo"] = "profile",
        ["getGroupChatHistory"] = "group", ["addUserToGroup"] = "group",
        ["removeUserFromGroup"] = "group", ["leaveGroup"] = "group",
        ["changeGroupName"] = "group", ["changeGroupAvatar"] = "file",
        ["changeGroupOwner"] = "group", ["addGroupDeputy"] = "group",
        ["removeGroupDeputy"] = "group", ["addGroupBlockedMember"] = "group",
        ["removeGroupBlockedMember"] = "group", ["getGroupBlockedMember"] = "group",
        ["disperseGroup"] = "group", ["enableGroupLink"] = "group",
        ["disableGroupLink"] = "group", ["getGroupLinkInfo"] = "group",
        ["getGroupLinkDetail"] = "group", ["joinGroupLink"] = "group",
        ["updateGroupSettings"] = "group", ["getPendingGroupMembers"] = "group",
        ["reviewPendingMemberRequest"] = "group", ["getGroupInviteBoxInfo"] = "group",
        ["getGroupInviteBoxList"] = "group", ["joinGroupInviteBox"] = "group",
        ["deleteGroupInviteBox"] = "group", ["upgradeGroupToCommunity"] = "group",
        ["keepAlive"] = "chat",

        ["sendMessage"] = "chat", ["sendSticker"] = "chat",
        ["sendLink"] = "chat", ["sendVideo"] = "chat",
        ["sendVoice"] = "chat", ["sendCard"] = "chat",
        ["sendBankCard"] = "zimsg", ["forwardMessage"] = "chat",
        ["deleteMessage"] = "chat", ["undo"] = "chat",
        ["sendTypingEvent"] = "chat", ["sendSeenEvent"] = "chat",
        ["sendDeliveredEvent"] = "chat", ["parseLink"] = "file",
        ["addReaction"] = "reaction", ["uploadAttachment"] = "file",

        ["getStickers"] = "sticker", ["getStickersDetail"] = "sticker",
        ["getStickerCategoryDetail"] = "sticker", ["searchSticker"] = "sticker",

        ["createPoll"] = "group", ["getPollDetail"] = "group",
        ["addPollOptions"] = "group", ["votePoll"] = "group",
        ["lockPoll"] = "group", ["sharePoll"] = "group",

        ["createReminder"] = "group", ["editReminder"] = "group",
        ["removeReminder"] = "group", ["getReminder"] = "group_board",
        ["getListReminder"] = "group", ["getReminderResponses"] = "group_board",

        ["createCatalog"] = "catalog", ["updateCatalog"] = "catalog",
        ["deleteCatalog"] = "catalog", ["getCatalogList"] = "catalog",
        ["createProductCatalog"] = "catalog", ["updateProductCatalog"] = "catalog",
        ["deleteProductCatalog"] = "catalog", ["getProductCatalogList"] = "catalog",
        ["uploadProductPhoto"] = "file",

        ["createAutoReply"] = "auto_reply", ["updateAutoReply"] = "auto_reply",
        ["deleteAutoReply"] = "auto_reply", ["getAutoReplyList"] = "auto_reply",

        ["addQuickMessage"] = "quick_message", ["updateQuickMessage"] = "quick_message",
        ["removeQuickMessage"] = "quick_message", ["getQuickMessageList"] = "quick_message",

        ["getListBoard"] = "group_board", ["createNote"] = "group_board",
        ["editNote"] = "group_board",

        ["getLabels"] = "label", ["updateLabels"] = "label",
    };

    /// <summary>
    /// Maps endpoint name to the actual API path suffix (used after the base host from ZpwServiceMapV3).
    /// These are the REAL paths from zca-js TypeScript source, NOT generic "api/{endpointName}".
    /// </summary>
    private static readonly Dictionary<string, string> EndpointToPathMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Profile service
        ["fetchAccountInfo"] = "/api/social/profile/me-v2",
        ["getAllFriends"] = "/api/social/friend/getfriends",
        ["getUserInfo"] = "/api/social/friend/getprofiles/v2",
        ["updateProfile"] = "/api/social/profile/update",
        ["updateProfileBio"] = "/api/social/profile/status",
        ["getAvatarList"] = "/api/social/avatar-list",
        ["getFullAvatar"] = "/api/social/profile/avatar",
        ["deleteAvatar"] = "/api/social/del-avatars",
        ["reuseAvatar"] = "/api/social/reuse-avatar",
        ["getAvatarUrlProfile"] = "/api/social/profile/avatar-url",
        ["getBizAccount"] = "/api/social/friend/get-bizacc",
        ["getCloseFriends"] = "/api/social/friend/getclosedfriends",
        ["getFriendOnlines"] = "/api/social/friend/onlines",
        ["lastOnline"] = "/api/social/profile/lastOnline",
        ["getMute"] = "/api/social/profile/getmute",
        ["setMute"] = "/api/social/profile/setmute",
        ["getSettings"] = "/api/social/profile/getsetting",
        ["updateSettings"] = "/api/social/profile/setSetting",
        ["updateLang"] = "/api/social/profile/updatelang",
        ["updateActiveStatus"] = "/api/social/profile/activeTime",
        ["getCookie"] = "/api/social/profile/getCookie",
        ["sendReport"] = "/api/social/profile/report",

        // Friend service
        ["acceptFriendRequest"] = "/api/friend/accept",
        ["rejectFriendRequest"] = "/api/friend/reject",
        ["removeFriend"] = "/api/friend/remove",
        ["undoFriendRequest"] = "/api/friend/undo",
        ["blockUser"] = "/api/friend/block",
        ["unblockUser"] = "/api/friend/unblock",
        ["blockViewFeed"] = "/api/friend/feed/block",
        ["findUser"] = "/api/friend/profile/get",
        ["findUserByUsername"] = "/api/friend/search/by-user-name",
        ["sendFriendRequest"] = "/api/friend/sendreq",
        ["getFriendRequestStatus"] = "/api/friend/reqstatus",
        ["getFriendRecommendations"] = "/api/friend/recommendsv2/list",
        ["getSentFriendRequest"] = "/api/friend/requested/list",
        ["getRelatedFriendGroup"] = "/api/friend/group/related",
        ["getMultiUsersByPhones"] = "/api/friend/profile/multiget",
        ["getQR"] = "/api/friend/mget-qr",

        // Friend Board service
        ["getFriendBoardList"] = "/api/friendboard/list",

        // Alias service
        ["changeFriendAlias"] = "/api/alias/update",
        ["removeFriendAlias"] = "/api/alias/remove",
        ["getAliasList"] = "/api/alias/list",

        // Conversation service
        ["getContext"] = "/api/conv/get_lsv3",
        ["getHiddenConversations"] = "/api/hiddenconvers/get-all",
        ["setHiddenConversations"] = "/api/hiddenconvers/add-remove",
        ["getPinConversations"] = "/api/pinconvers/list",
        ["setPinnedConversations"] = "/api/pinconvers/updatev2",
        ["resetHiddenConversPin"] = "/api/hiddenconvers/reset",
        ["updateHiddenConversPin"] = "/api/hiddenconvers/update-pin",
        ["deleteChat"] = "/api/conv/delchat",
        ["addUnreadMark"] = "/api/conv/addUnreadMark",
        ["removeUnreadMark"] = "/api/conv/removeUnreadMark",
        ["getUnreadMark"] = "/api/conv/getUnreadMark",
        ["getAutoDeleteChat"] = "/api/conv/autodelete/getConvers",
        ["updateAutoDeleteChat"] = "/api/conv/autodelete/updateConvers",
        ["setMuteConversation"] = "/api/conv/setchatmute",
        ["getMuteConversation"] = "/api/conv/getchatmute",

        // Label service for archived chats + labels
        ["getArchivedChatList"] = "/api/archivedchat/list",
        ["updateArchivedChatList"] = "/api/archivedchat/update",
        ["getLabels"] = "/api/convlabel/get",
        ["updateLabels"] = "/api/convlabel/update",

        // Group service
        ["createGroup"] = "/api/group/create/v2",
        ["getGroupInfo"] = "/api/group/getmg-v2",
        ["getGroupMembersInfo"] = "/api/social/group/members",
        ["getGroupChatHistory"] = "/api/group/history",
        ["addUserToGroup"] = "/api/group/invite/v2",
        ["removeUserFromGroup"] = "/api/group/kickout",
        ["leaveGroup"] = "/api/group/leave",
        ["changeGroupName"] = "/api/group/updateinfo",
        ["changeGroupOwner"] = "/api/group/change-owner",
        ["addGroupDeputy"] = "/api/group/admins/add",
        ["removeGroupDeputy"] = "/api/group/admins/remove",
        ["addGroupBlockedMember"] = "/api/group/blockedmems/add",
        ["removeGroupBlockedMember"] = "/api/group/blockedmems/remove",
        ["getGroupBlockedMember"] = "/api/group/blockedmems/list",
        ["disperseGroup"] = "/api/group/disperse",
        ["enableGroupLink"] = "/api/group/link/new",
        ["disableGroupLink"] = "/api/group/link/disable",
        ["getGroupLinkInfo"] = "/api/group/link/ginfo",
        ["getGroupLinkDetail"] = "/api/group/link/detail",
        ["joinGroupLink"] = "/api/group/link/join",
        ["updateGroupSettings"] = "/api/group/setting/update",
        ["getPendingGroupMembers"] = "/api/group/pending-mems/list",
        ["reviewPendingMemberRequest"] = "/api/group/pending-mems/review",
        ["getGroupInviteBoxInfo"] = "/api/group/inv-box/inv-info",
        ["getGroupInviteBoxList"] = "/api/group/inv-box/list",
        ["joinGroupInviteBox"] = "/api/group/inv-box/join",
        ["deleteGroupInviteBox"] = "/api/group/inv-box/mdel-inv",
        ["upgradeGroupToCommunity"] = "/api/group/upgrade/community",

        // Group Poll service
        ["getAllGroups"] = "/api/group/getlg/v4",

        // Chat service
        ["sendMessage"] = "/api/message/sms",
        ["sendSticker"] = "/api/message/sticker",
        ["sendLink"] = "/api/message/sendlink",
        ["forwardMessage"] = "/api/message/forward",
        ["deleteMessage"] = "/api/message/delete",
        ["undo"] = "/api/message/undo",
        ["sendTypingEvent"] = "/api/message/typing",
        ["sendSeenEvent"] = "/api/message/seensent",
        ["sendDeliveredEvent"] = "/api/message/deliveredsent",

        // Group chat service (for group messages)
        ["sendMessageGroup"] = "/api/group/sendmsg",
        ["sendStickerGroup"] = "/api/group/sticker",

        // zimsg service
        ["sendBankCard"] = "/api/transfer/card",

        // File service
        ["changeAccountAvatar"] = "/api/profile/upavatar",
        ["changeGroupAvatar"] = "/api/group/upavatar",
        ["uploadAttachment"] = "/api/msgfile/upload",
        ["uploadProductPhoto"] = "/api/product/upload/photo",
        ["parseLink"] = "/api/message/parselink",

        // Reaction service
        ["addReaction"] = "/api/reaction/add",

        // Sticker service
        ["getStickers"] = "/api/message/sticker/suggest/stickers",
        ["getStickersDetail"] = "/api/message/sticker/sticker_detail",
        ["getStickerCategoryDetail"] = "/api/message/sticker/category/sticker_detail",
        ["searchSticker"] = "/api/message/sticker/search",

        // Poll service (group)
        ["createPoll"] = "/api/poll/create",
        ["getPollDetail"] = "/api/poll/detail",
        ["addPollOptions"] = "/api/poll/option/add",
        ["votePoll"] = "/api/poll/vote",
        ["lockPoll"] = "/api/poll/end",
        ["sharePoll"] = "/api/poll/share",

        // Reminder service
        ["createReminder"] = "/api/group/scheduler/add",
        ["editReminder"] = "/api/group/scheduler/update",
        ["removeReminder"] = "/api/group/scheduler/remove",
        ["getListReminder"] = "/api/group/scheduler/list",

        // Group Board service
        ["getReminder"] = "/api/board/topic/getReminder",
        ["getReminderResponses"] = "/api/board/topic/listResponseEvent",
        ["getListBoard"] = "/api/board/list",
        ["createNote"] = "/api/board/topic/createv2",
        ["editNote"] = "/api/board/topic/updatev2",

        // Catalog service
        ["createCatalog"] = "/api/prodcatalog/catalog/create",
        ["updateCatalog"] = "/api/prodcatalog/catalog/update",
        ["deleteCatalog"] = "/api/prodcatalog/catalog/delete",
        ["getCatalogList"] = "/api/prodcatalog/catalog/list",
        ["createProductCatalog"] = "/api/prodcatalog/product/create",
        ["updateProductCatalog"] = "/api/prodcatalog/product/update",
        ["deleteProductCatalog"] = "/api/prodcatalog/product/mdelete",
        ["getProductCatalogList"] = "/api/prodcatalog/product/list",

        // Auto Reply service
        ["createAutoReply"] = "/api/autoreply/create",
        ["updateAutoReply"] = "/api/autoreply/update",
        ["deleteAutoReply"] = "/api/autoreply/delete",
        ["getAutoReplyList"] = "/api/autoreply/list",

        // Quick Message service
        ["addQuickMessage"] = "/api/quickmessage/create",
        ["updateQuickMessage"] = "/api/quickmessage/update",
        ["removeQuickMessage"] = "/api/quickmessage/delete",
        ["getQuickMessageList"] = "/api/quickmessage/list",

        // Keep alive (special: no /api/ prefix)
        ["keepAlive"] = "/keepalive",
    };

    /// <summary>
    /// Resolves the full API URL for an endpoint using Zalo's service map and the real path mapping.
    /// </summary>
    private static string ResolveBaseUrl(ZaloContext ctx, string endpoint)
    {
        if (endpoint.StartsWith("http")) return endpoint;

        // Get the path from the real path mapping
        string apiPath;
        if (!EndpointToPathMap.TryGetValue(endpoint, out apiPath))
        {
            // Fallback to generic pattern if not in map
            apiPath = $"/api/{endpoint}";
        }

        // Get the base host from the service map
        if (EndpointToServiceMap.TryGetValue(endpoint, out var serviceKey))
        {
            if (ctx.ZpwServiceMapV3.TryGetValue(serviceKey, out var urls) && urls.Length > 0)
                return $"{urls[0].TrimEnd('/')}{apiPath}";
        }

        // Fallback to wpa.chat.zalo.me
        return $"https://wpa.chat.zalo.me{apiPath}";
    }

    private static string BuildApiUrl(ZaloContext ctx, string endpoint, object? parameters = null, bool flattenToQuery = false)
    {
        var baseUrl = ResolveBaseUrl(ctx, endpoint);
        var extraParams = new Dictionary<string, string>();
        if (parameters != null && flattenToQuery)
        {
            var dict = ObjectToDictionary(parameters);
            foreach (var kvp in dict)
                if (kvp.Value != null)
                    extraParams[kvp.Key] = kvp.Value?.ToString() ?? "";
        }
        return ZaloUtils.MakeUrl(baseUrl, extraParams.Count > 0 ? extraParams : null, ctx.ApiVersion, ctx.ApiType);
    }

    public static async Task<ZaloApiResponse<JsonElement>> CallGetApiAsync(ZaloContext ctx, HttpClient httpClient, string endpoint, object? parameters = null)
    {
        var url = BuildApiUrl(ctx, endpoint, parameters, true);
        return await SendApiRequestAsync(ctx, httpClient, url, HttpMethod.Get, endpoint);
    }

    /// <summary>
    /// Makes a GET API request with AES-encrypted params as query parameter.
    /// Matches TypeScript pattern: utils.request(utils.makeURL(serviceURL, { params: encryptedParams }), { method: "GET" })
    /// </summary>
    public static async Task<ZaloApiResponse<JsonElement>> CallEncryptedGetApiAsync(ZaloContext ctx, HttpClient httpClient, string endpoint, object? parameters = null)
    {
        var baseUrl = BuildApiUrl(ctx, endpoint);
        // Serialize params to JSON and encrypt with AES
        string encryptedParams;
        if (parameters != null)
        {
            var json = JsonSerializer.Serialize(parameters, _jsonOptions);
            encryptedParams = AesHelper.EncryptAesCbc(ctx.SecretKey, json) ?? json;
        }
        else
        {
            encryptedParams = AesHelper.EncryptAesCbc(ctx.SecretKey, "{}") ?? "{}";
        }
        var url = ZaloUtils.MakeUrl(baseUrl, new Dictionary<string, string> { ["params"] = encryptedParams }, ctx.ApiVersion, ctx.ApiType);
        return await SendApiRequestAsync(ctx, httpClient, url, HttpMethod.Get, endpoint);
    }

    public static async Task<ZaloApiResponse<JsonElement>> CallPostApiAsync(ZaloContext ctx, HttpClient httpClient, string endpoint, object? data = null)
    {
        var url = BuildApiUrl(ctx, endpoint);
        return await SendApiRequestAsync(ctx, httpClient, url, HttpMethod.Post, endpoint, data);
    }

    /// <summary>
    /// Makes a POST API request with AES-encrypted params sent as form field "params".
    /// Matches TypeScript pattern: utils.request(serviceURL, { method: "POST", body: new URLSearchParams({ params: encryptedParams }) })
    /// </summary>
    public static async Task<ZaloApiResponse<JsonElement>> CallEncryptedPostApiAsync(ZaloContext ctx, HttpClient httpClient, string endpoint, object? data = null)
    {
        var url = BuildApiUrl(ctx, endpoint);
        return await SendApiEncryptedRequestAsync(ctx, httpClient, url, HttpMethod.Post, endpoint, data);
    }

    public static async Task<ZaloApiResponse<JsonElement>> CallCustomApiAsync(ZaloContext ctx, HttpClient httpClient, string method, string endpoint, object? data = null, bool isGet = true)
    {
        var url = BuildApiUrl(ctx, endpoint);
        return await SendApiRequestAsync(ctx, httpClient, url, isGet ? HttpMethod.Get : HttpMethod.Post, endpoint, data);
    }

    /// <summary>
    /// Builds a URL for sending a group message (uses group service URL instead of chat service).
    /// </summary>
    public static string BuildGroupMessageUrl(ZaloContext ctx, string groupEndpoint)
    {
        if (ctx.ZpwServiceMapV3.TryGetValue("group", out var urls) && urls.Length > 0)
        {
            string path;
            if (!EndpointToPathMap.TryGetValue(groupEndpoint, out path))
                path = $"/api/{groupEndpoint}";
            return ZaloUtils.MakeUrl($"{urls[0].TrimEnd('/')}{path}", null, ctx.ApiVersion, ctx.ApiType);
        }
        return BuildApiUrl(ctx, groupEndpoint);
    }

    /// <summary>
    /// Sends a POST request with AES-encrypted params as form field "params".
    /// Matches TS: utils.request(url, { method: "POST", body: new URLSearchParams({ params: encryptedParams }) })
    /// </summary>
    private static async Task<ZaloApiResponse<JsonElement>> SendApiEncryptedRequestAsync(ZaloContext ctx, HttpClient httpClient, string url, HttpMethod method, string? endpoint = null, object? data = null)
    {
        try
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Add("User-Agent", ctx.UserAgent);
            if (!string.IsNullOrEmpty(ctx.Imei))
                request.Headers.Add("x-zalo-imei", ctx.Imei);

            var cookieHeader = GetCookieHeaderForUrl(ctx.CookieContainer, url);
            if (!string.IsNullOrEmpty(cookieHeader))
                request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);

            if (data != null)
            {
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                var encrypted = !string.IsNullOrEmpty(ctx.SecretKey)
                    ? AesHelper.EncryptAesCbc(ctx.SecretKey, json) ?? json
                    : json;
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["params"] = encrypted });
            }

            var response = await httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            if (ctx.Options.Logging && endpoint != null)
            {
                var preview = responseString.Length > 200 ? responseString[..200] + "..." : responseString;
                ctx.Options.ApiLogCallback?.Invoke($"[API] {method} {url} → HTTP {(int)response.StatusCode} | {preview}");
            }

            if (!response.IsSuccessStatusCode)
                return new ZaloApiResponse<JsonElement> { Error = $"HTTP {(int)response.StatusCode}: {method} {url}" };

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
            {
                using var e = JsonDocument.Parse("{}");
                return new ZaloApiResponse<JsonElement> { Data = e.RootElement.Clone() };
            }

            string decrypted;
            if (!string.IsNullOrEmpty(ctx.SecretKey))
            {
                decrypted = AesHelper.DecryptAesCbc(ctx.SecretKey, rawData);
                if (decrypted == null) return new ZaloApiResponse<JsonElement> { Error = "Failed to decrypt" };
            }
            else decrypted = rawData;

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
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (ctx.Options.ApiLogCallback != null)
                ctx.Options.ApiLogCallback($"[API-ERROR] {method} {endpoint ?? url}: {msg}");
            return new ZaloApiResponse<JsonElement> { Error = $"Request failed: {msg}" };
        }
    }

    private static async Task<ZaloApiResponse<JsonElement>> SendApiRequestAsync(ZaloContext ctx, HttpClient httpClient, string url, HttpMethod method, string? endpoint = null, object? data = null)
    {
        try
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Add("User-Agent", ctx.UserAgent);
            if (!string.IsNullOrEmpty(ctx.Imei))
                request.Headers.Add("x-zalo-imei", ctx.Imei);

            // Force-add cookies as header to ensure they're sent to ALL subdomains
            var cookieHeader = GetCookieHeaderForUrl(ctx.CookieContainer, url);
            if (!string.IsNullOrEmpty(cookieHeader))
                request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);

            if (data != null && method == HttpMethod.Post)
            {
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                var formData = new Dictionary<string, string> { ["data"] = json };
                if (!string.IsNullOrEmpty(ctx.SecretKey))
                {
                    var encrypted = AesHelper.EncryptAesCbc(ctx.SecretKey, json);
                    if (encrypted != null) formData["data"] = encrypted;
                }
                request.Content = new FormUrlEncodedContent(formData);
            }

            var response = await httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            // Always log API calls when logging is enabled
            if (ctx.Options.Logging && endpoint != null)
            {
                var preview = responseString.Length > 200 ? responseString[..200] + "..." : responseString;
                ctx.Options.ApiLogCallback?.Invoke($"[API] {method} {url} → HTTP {(int)response.StatusCode} | {preview}");
            }

            if (!response.IsSuccessStatusCode)
                return new ZaloApiResponse<JsonElement> { Error = $"HTTP {(int)response.StatusCode}: {method} {url}" };

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
            {
                using var e = JsonDocument.Parse("{}");
                return new ZaloApiResponse<JsonElement> { Data = e.RootElement.Clone() };
            }

            string decrypted;
            if (!string.IsNullOrEmpty(ctx.SecretKey))
            {
                decrypted = AesHelper.DecryptAesCbc(ctx.SecretKey, rawData);
                if (decrypted == null) return new ZaloApiResponse<JsonElement> { Error = "Failed to decrypt" };
            }
            else decrypted = rawData;

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
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (ctx.Options.ApiLogCallback != null)
                ctx.Options.ApiLogCallback($"[API-ERROR] {method} {endpoint ?? url}: {msg}");
            return new ZaloApiResponse<JsonElement> { Error = $"Request failed: {msg}" };
        }
    }

    /// <summary>
    /// Extracts cookies from a CookieContainer for the given URL.
    /// </summary>
    private static string GetCookieHeaderForUrl(CookieContainer container, string url)
    {
        try
        {
            var uri = new Uri(url);
            var cookies = container.GetCookies(uri);
            var sb = new StringBuilder();
            foreach (Cookie cookie in cookies)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append($"{cookie.Name}={cookie.Value}");
            }
            return sb.ToString();
        }
        catch
        {
            return "";
        }
    }

    private static Dictionary<string, object?> ObjectToDictionary(object obj)
    {
        return obj.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => char.ToLowerInvariant(p.Name[0]) + p.Name[1..], p => p.GetValue(obj));
    }
}