using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.WebSockets;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ICU.Lib.ZaloClientWeb.Models;
using ICU.Lib.ZaloClientWeb.Models.Types;
using ICU.Lib.ZaloClientWeb.Utils;

namespace ICU.Lib.ZaloClientWeb.WebSocket;

public class ZaloConnectionEventArgs : EventArgs
{
    public bool Connected { get; set; }
    public CloseReason? CloseReason { get; set; }
    public string? Message { get; set; }
}

public class ZaloMessageEventArgs : EventArgs
{
    public string Type { get; set; } = string.Empty;
    public object? Message { get; set; }
    public bool IsGroup { get; set; }
    public bool IsSelf { get; set; }
}

public class ZaloTypingEventArgs : EventArgs
{
    public string Act { get; set; } = string.Empty;
    public string ThreadId { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public long Ts { get; set; }
    public bool IsGroup { get; set; }
}

public class ZaloReactionEventArgs : EventArgs
{
    public string Type { get; set; } = string.Empty;
    public List<Reaction> Reactions { get; set; } = new();
}

public class ZaloSeenDeliveredEventArgs : EventArgs
{
    public string Type { get; set; } = string.Empty;
    public string ThreadId { get; set; } = string.Empty;
    public List<string> UserIds { get; set; } = new();
}

public class ZaloUndoEventArgs : EventArgs
{
    public UndoEvent? Undo { get; set; }
}

public class ZaloListener : IDisposable
{
    private readonly ZaloContext _context;
    private readonly HttpClient _httpClient;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private int _currentUrlIndex;
    private string? _cipherKey;
    private int _id;
    private bool _selfListen;
    private int _retryCount;
    private int _rotateCount;
    private int _maxRetries;
    private int[] _retryTimes;
    private int[] _rotateErrorCodes;
    private int[] _closeAndRetryCodes;
    private int _pingInterval;

    private readonly ZaloLogger _logger;

    public event EventHandler<ZaloConnectionEventArgs>? Connected;
    public event EventHandler<ZaloConnectionEventArgs>? Disconnected;
    public event EventHandler<ZaloConnectionEventArgs>? Error;
    public event EventHandler<ZaloMessageEventArgs>? MessageReceived;
    public event EventHandler<ZaloTypingEventArgs>? TypingReceived;
    public event EventHandler<ZaloSeenDeliveredEventArgs>? SeenReceived;
    public event EventHandler<ZaloSeenDeliveredEventArgs>? DeliveredReceived;
    public event EventHandler<ZaloReactionEventArgs>? ReactionReceived;
    public event EventHandler<GroupEvent>? GroupEventReceived;
    public event EventHandler<FriendEvent>? FriendEventReceived;
    public event EventHandler<ZaloUndoEventArgs>? UndoReceived;
    public event EventHandler<string>? CipherKeyReceived;

    private bool _disposed;

    public ZaloListener(ZaloContext context, HttpClient httpClient)
    {
        _context = context;
        _httpClient = httpClient;
        _logger = new ZaloLogger(context.Options.Logging);
        _selfListen = context.Options.SelfListen;
        _id = 0;
        _retryCount = 0;
        _rotateCount = 0;
        _maxRetries = 5;
        _retryTimes = new[] { 1000, 2000, 5000, 10000, 30000 };
        _rotateErrorCodes = Array.Empty<int>();
        _closeAndRetryCodes = Array.Empty<int>();
        _pingInterval = 30000;

        // Parse socket settings from context when available
        try
        {
            if (context.Settings.TryGetValue("features", out var featuresObj) && featuresObj is Dictionary<string, object> features)
            {
                if (features.TryGetValue("socket", out var sockObj) && sockObj is Dictionary<string, object> socket)
                {
                    if (socket.TryGetValue("ping_interval", out var pingVal))
                        _pingInterval = Convert.ToInt32(pingVal);

                    if (socket.TryGetValue("retries", out var retriesObj) && retriesObj is Dictionary<string, object> retries)
                    {
                        foreach (var kvp in retries)
                        {
                            if (kvp.Value is Dictionary<string, object> r)
                            {
                                if (r.TryGetValue("max", out var max))
                                    _maxRetries = Convert.ToInt32(max);
                                if (r.TryGetValue("times", out var times))
                                {
                                    if (times is JsonElement je && je.ValueKind == JsonValueKind.Array)
                                        _retryTimes = je.EnumerateArray().Select(x => x.GetInt32()).ToArray();
                                }
                            }
                        }
                    }

                    if (socket.TryGetValue("close_and_retry_codes", out var closeCodes) && closeCodes is JsonElement cje && cje.ValueKind == JsonValueKind.Array)
                        _closeAndRetryCodes = cje.EnumerateArray().Select(x => x.GetInt32()).ToArray();

                    if (socket.TryGetValue("rotate_error_codes", out var rotCodes) && rotCodes is JsonElement rje && rje.ValueKind == JsonValueKind.Array)
                        _rotateErrorCodes = rje.EnumerateArray().Select(x => x.GetInt32()).ToArray();
                }
            }
        }
        catch { /* best-effort */ }
    }

    public async Task StartAsync(bool retryOnClose = false)
    {
        if (_webSocket != null)
            throw new InvalidOperationException("Listener already started");

        _cts = new CancellationTokenSource();
        _webSocket = new ClientWebSocket();

        _webSocket.Options.SetRequestHeader("accept-encoding", "gzip, deflate, br, zstd");
        _webSocket.Options.SetRequestHeader("accept-language", "en-US,en;q=0.9");
        _webSocket.Options.SetRequestHeader("cache-control", "no-cache");
        _webSocket.Options.SetRequestHeader("origin", "https://chat.zalo.me");
        _webSocket.Options.SetRequestHeader("user-agent", _context.UserAgent);
        _webSocket.Options.Cookies = _context.CookieContainer;

        var wsUrl = BuildWebSocketUrl();

        try
        {
            _logger.Info("Connecting to WebSocket:", wsUrl);
            await _webSocket.ConnectAsync(new Uri(wsUrl), _cts.Token);
            Connected?.Invoke(this, new ZaloConnectionEventArgs { Connected = true });
            await ReceiveLoopAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("WebSocket connection failed:", ex.Message);
            Error?.Invoke(this, new ZaloConnectionEventArgs { Connected = false, Message = ex.Message });
        }
    }

    public async Task StopAsync()
    {
        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Manual closure", CancellationToken.None);

        _cts?.Cancel();
        _webSocket?.Dispose();
        _webSocket = null;
    }

    public async Task SendAsync(byte version, ushort cmd, byte subCmd, object data)
    {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            return;

        var json = JsonSerializer.Serialize(data);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        var header = new byte[4 + jsonBytes.Length];
        header[0] = version;
        header[1] = (byte)(cmd & 0xFF);
        header[2] = (byte)((cmd >> 8) & 0xFF);
        header[3] = subCmd;
        Array.Copy(jsonBytes, 0, header, 4, jsonBytes.Length);

        await _webSocket.SendAsync(new ArraySegment<byte>(header), WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[8192];
        var messageBuffer = new List<byte>();

        while (_webSocket != null && _webSocket.State == WebSocketState.Open && !_cts!.IsCancellationRequested)
        {
            try
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                messageBuffer.AddRange(new ArraySegment<byte>(buffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    var data = messageBuffer.ToArray();
                    messageBuffer.Clear();
                    await ProcessMessageAsync(data);
                }
            }
            catch (WebSocketException ex)
            {
                _logger.Error("WebSocket error:", ex.Message);
                Disconnected?.Invoke(this, new ZaloConnectionEventArgs
                {
                    Connected = false,
                    CloseReason = CloseReason.AbnormalClosure,
                    Message = ex.Message
                });
                break;
            }
        }
    }

    private async Task ProcessMessageAsync(byte[] rawData)
    {
        try
        {
            if (rawData.Length < 4) return;

            var version = rawData[0];
            var cmd = BitConverter.ToUInt16(rawData, 1);
            var subCmd = rawData[3];

            if (rawData.Length <= 4) return;

            var payloadData = Encoding.UTF8.GetString(rawData, 4, rawData.Length - 4);
            if (string.IsNullOrEmpty(payloadData)) return;

            // Parse with explicit lifetime management
            JsonElement parsed;
            using (var doc = JsonDocument.Parse(payloadData))
            {
                parsed = doc.RootElement.Clone();
            }

            // Log unknown cmds
            string cmdName;
            switch (cmd)
            {
                case 1: cmdName = "CipherKeyExchange"; break;
                case 2: cmdName = "Ping"; break;
                case 501: cmdName = "UserMsgs"; break;
                case 502: cmdName = "UserSeenDelivered"; break;
                case 510: cmdName = "OldUserMsgs"; break;
                case 511: cmdName = "OldGroupMsgs"; break;
                case 521: cmdName = "GroupMsgs"; break;
                case 522: cmdName = "GroupSeenDelivered"; break;
                case 601: cmdName = "Controls"; break;
                case 602: cmdName = "Typing"; break;
                case 610: cmdName = "OldUserReactions"; break;
                case 611: cmdName = "OldGroupReactions"; break;
                case 612: cmdName = "Reactions"; break;
                case 3000: cmdName = "DuplicateConnection"; break;
                case 621: cmdName = "Unknown:ChatText"; break;
                default: cmdName = $"Unknown:{cmd}"; break;
            }

            if (cmd > 1 && cmd != 2)
            {
                _logger.Verbose($"WS: [{cmdName}] v={version} cmd={cmd} sub={subCmd}");
            }

            // Cipher key exchange (cmd=1)
            if (version == 1 && cmd == 1 && subCmd == 1 && parsed.TryGetProperty("key", out var keyEl))
            {
                _cipherKey = keyEl.GetString();
                _logger.Info($"🔑 Cipher key received, ping loop started");
                CipherKeyReceived?.Invoke(this, _cipherKey);
                StartPingLoop();
                return;
            }

            // ===== User messages (cmd=501) =====
            if (version == 1 && cmd == 501 && subCmd == 0)
            {
                var decodedNullable = await DecodeEventPayloadAsync(parsed);
                if (decodedNullable == null) return;
                var decoded = decodedNullable.Value.Clone();
                if (!decoded.TryGetProperty("msgs", out var msgsEl) || msgsEl.ValueKind != JsonValueKind.Array)
                    return;

                foreach (var msgEl in msgsEl.EnumerateArray())
                {
                    if (msgEl.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Object
                        && content.TryGetProperty("deleteMsg", out _))
                    {
                        var undo = new UndoEvent(_context.Uid.ToString(), msgEl, false);
                        if (!undo.IsSelf || _context.Options.SelfListen)
                        {
                            _logger.Info($"↩ [UNDO] msgId={undo.MsgId}");
                            UndoReceived?.Invoke(this, new ZaloUndoEventArgs { Undo = undo });
                        }
                        continue;
                    }

                    var msgData = JsonSerializer.Deserialize<MessageData>(msgEl.GetRawText());
                    if (msgData != null)
                    {
                        var msg = new UserMessageInfo(_context.Uid.ToString(), msgData);
                        if (msg.IsSelf && !_context.Options.SelfListen) continue;
                        var preview = msg.Data?.Content?.GetString() ?? msg.Data?.Notify ?? "(no text/other)";
                        if (preview.Length > 50) preview = preview[..50] + "...";
                        _logger.Info($"📨 [USER] from={msg.Data?.UidFrom} type={msg.Data?.MsgType} content=\"{preview}\"");
                        MessageReceived?.Invoke(this, new ZaloMessageEventArgs
                        {
                            Type = "user_message",
                            Message = msg,
                            IsGroup = false,
                            IsSelf = msg.IsSelf
                        });
                    }
                }
                return;
            }

            // ===== Group messages (cmd=521) =====
            if (version == 1 && cmd == 521 && subCmd == 0)
            {
                var decodedNullable = await DecodeEventPayloadAsync(parsed);
                if (decodedNullable == null) return;
                var decoded = decodedNullable.Value.Clone();
                if (!decoded.TryGetProperty("groupMsgs", out var groupMsgsEl) || groupMsgsEl.ValueKind != JsonValueKind.Array)
                    return;

                foreach (var msgEl in groupMsgsEl.EnumerateArray())
                {
                    if (msgEl.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Object
                        && content.TryGetProperty("deleteMsg", out _))
                    {
                        var undo = new UndoEvent(_context.Uid.ToString(), msgEl, true);
                        if (!undo.IsSelf || _context.Options.SelfListen)
                        {
                            _logger.Info($"↩ [UNDO] msgId={undo.MsgId}");
                            UndoReceived?.Invoke(this, new ZaloUndoEventArgs { Undo = undo });
                        }
                        continue;
                    }

                    var msgData = JsonSerializer.Deserialize<MessageData>(msgEl.GetRawText());
                    if (msgData != null)
                    {
                        var msg = new GroupMessageInfo(_context.Uid.ToString(), msgData);
                        if (msg.IsSelf && !_context.Options.SelfListen) continue;
                        var preview = msg.Data?.Content?.GetString() ?? msg.Data?.Notify ?? "(no text/other)";
                        if (preview.Length > 50) preview = preview[..50] + "...";
                        _logger.Info($"👥 [GROUP] group={msg.ThreadId} from={msg.Data?.UidFrom} type={msg.Data?.MsgType} content=\"{preview}\"");
                        MessageReceived?.Invoke(this, new ZaloMessageEventArgs
                        {
                            Type = "group_message",
                            Message = msg,
                            IsGroup = true,
                            IsSelf = msg.IsSelf
                        });
                    }
                }
                return;
            }

            // ===== Control events (cmd=601) =====
            if (version == 1 && cmd == 601 && subCmd == 0)
            {
                var decodedNullable = await DecodeEventPayloadAsync(parsed);
                if (decodedNullable == null) return;
                var decoded = decodedNullable.Value.Clone();
                if (!decoded.TryGetProperty("controls", out var controlsEl) || controlsEl.ValueKind != JsonValueKind.Array)
                    return;

                foreach (var ctrl in controlsEl.EnumerateArray())
                {
                    if (!ctrl.TryGetProperty("content", out var ctrlContent) || ctrlContent.ValueKind != JsonValueKind.Object)
                        continue;

                    if (ctrlContent.TryGetProperty("act_type", out var actType))
                    {
                        var actStr = actType.GetString();

                        if (actStr == "file_done")
                        {
                            var fileId = ctrlContent.TryGetProperty("fileId", out var fidEl) ? fidEl.GetString() : null;
                            _logger.Info($"📎 [FILE UPLOAD] id={fileId}");
                            if (fileId != null && _context.UploadCallbacks.TryGetValue(fileId, out var callback))
                            {
                                _context.UploadCallbacks.Remove(fileId);
                                callback(ctrlContent);
                            }
                            continue;
                        }

                        if (actStr == "group")
                        {
                            if (ctrlContent.TryGetProperty("act", out var groupAct))
                            {
                                if (groupAct.GetString() == "join_reject") continue;

                                var dataRaw = ctrlContent.TryGetProperty("data", out var gData)
                                    ? gData.ValueKind == JsonValueKind.String
                                        ? JsonDocument.Parse(gData.GetString()!).RootElement.Clone()
                                        : gData.Clone()
                                    : new JsonElement();

                                var groupEvent = GroupEvent.Initialize(
                                    _context.Uid.ToString(),
                                    dataRaw,
                                    (GroupEventType)ZaloUtils.GetGroupEventType(groupAct.GetString() ?? ""),
                                    groupAct.GetString() ?? ""
                                );
                                if (groupEvent.IsSelf && !_context.Options.SelfListen) continue;
                                _logger.Info($"📋 [GROUP EVENT] {groupAct.GetString()}");
                                GroupEventReceived?.Invoke(this, groupEvent);
                            }
                            continue;
                        }

                        if (actStr == "fr")
                        {
                            if (!ctrlContent.TryGetProperty("act", out var frAct)) continue;
                            if (frAct.GetString() == "req") continue;

                            JsonElement frData;
                            if (ctrlContent.TryGetProperty("data", out var frDataEl))
                            {
                                frData = frDataEl.ValueKind == JsonValueKind.String
                                    ? JsonDocument.Parse(frDataEl.GetString()!).RootElement.Clone()
                                    : frDataEl.Clone();
                            }
                            else
                            {
                                frData = default;
                            }

                            var friendEvent = FriendEvent.Initialize(
                                _context.Uid.ToString(),
                                frData,
                                (FriendEventType)ZaloUtils.GetFriendEventType(frAct.GetString() ?? "")
                            );
                            if (friendEvent.IsSelf && !_context.Options.SelfListen) continue;
                            _logger.Info($"🤝 [FRIEND EVENT] {frAct.GetString()}");
                            FriendEventReceived?.Invoke(this, friendEvent);
                        }
                    }
                }
                return;
            }

            // ===== Reactions (cmd=612) =====
            if (version == 1 && cmd == 612 && subCmd == 0)
            {
                var decodedNullable = await DecodeEventPayloadAsync(parsed);
                if (decodedNullable == null) return;
                var decoded = decodedNullable.Value.Clone();

                var reactionList = new List<Reaction>();

                if (decoded.TryGetProperty("reacts", out var reactsEl) && reactsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var r in reactsEl.EnumerateArray())
                    {
                        var rd = JsonSerializer.Deserialize<ReactionData>(r.GetRawText());
                        if (rd?.Content != null)
                            rd.Content = JsonSerializer.Deserialize<ReactionContent>(r.GetProperty("content").GetRawText());
                        if (rd != null)
                        {
                            var reaction = new Reaction(_context.Uid.ToString(), rd, false);
                            if (!reaction.IsSelf || _context.Options.SelfListen)
                                reactionList.Add(reaction);
                        }
                    }
                }

                if (decoded.TryGetProperty("reactGroups", out var reactGroupsEl) && reactGroupsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var r in reactGroupsEl.EnumerateArray())
                    {
                        var rd = JsonSerializer.Deserialize<ReactionData>(r.GetRawText());
                        if (rd?.Content != null)
                            rd.Content = JsonSerializer.Deserialize<ReactionContent>(r.GetProperty("content").GetRawText());
                        if (rd != null)
                        {
                            var reaction = new Reaction(_context.Uid.ToString(), rd, true);
                            if (!reaction.IsSelf || _context.Options.SelfListen)
                                reactionList.Add(reaction);
                        }
                    }
                }

                if (reactionList.Count > 0)
                {
                    _logger.Info($"❤️ [REACTION] x{reactionList.Count}");
                    ReactionReceived?.Invoke(this, new ZaloReactionEventArgs { Type = "reaction", Reactions = reactionList });
                }
                return;
            }

            // ===== Old reactions (cmd=610/611) =====
            if (cmd == 610 || cmd == 611)
            {
                var decodedNullable = await DecodeEventPayloadAsync(parsed);
                if (decodedNullable == null) return;
                var decoded = decodedNullable.Value.Clone();

                var isGroup = cmd == 611;
                var listKey = isGroup ? "reactGroups" : "reacts";
                var reactionList = new List<Reaction>();

                if (decoded.TryGetProperty(listKey, out var reactsEl) && reactsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var r in reactsEl.EnumerateArray())
                    {
                        var rd = JsonSerializer.Deserialize<ReactionData>(r.GetRawText());
                        if (rd != null)
                            reactionList.Add(new Reaction(_context.Uid.ToString(), rd, isGroup));
                    }
                }

                if (reactionList.Count > 0)
                    ReactionReceived?.Invoke(this, new ZaloReactionEventArgs { Type = "old_reactions", Reactions = reactionList });
                return;
            }

            // ===== Seen/Delivered user (cmd=502) =====
            if (version == 1 && cmd == 502 && subCmd == 0)
            {
                var decodedNullable = await DecodeEventPayloadAsync(parsed);
                if (decodedNullable == null) return;
                var decoded = decodedNullable.Value.Clone();

                if (decoded.TryGetProperty("delivereds", out var delEl) && delEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var d in delEl.EnumerateArray())
                    {
                        var delivered = JsonSerializer.Deserialize<UserDeliveredMessage>(d.GetRawText());
                        if (delivered != null)
                        {
                            _logger.Info($"✅ [DELIVERED] uid={delivered.Uid}");
                            DeliveredReceived?.Invoke(this, new ZaloSeenDeliveredEventArgs { Type = "delivered", ThreadId = delivered.Uid });
                        }
                    }
                }
                if (decoded.TryGetProperty("seens", out var seenEl) && seenEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in seenEl.EnumerateArray())
                    {
                        var seen = JsonSerializer.Deserialize<UserSeenMessage>(s.GetRawText());
                        if (seen != null)
                        {
                            _logger.Info($"👁 [SEEN] uid={seen.Uid}");
                            SeenReceived?.Invoke(this, new ZaloSeenDeliveredEventArgs { Type = "seen", ThreadId = seen.Uid });
                        }
                    }
                }
                return;
            }

            // ===== Seen/Delivered group (cmd=522) =====
            if (version == 1 && cmd == 522 && subCmd == 0)
            {
                var decodedNullable = await DecodeEventPayloadAsync(parsed);
                if (decodedNullable == null) return;
                var decoded = decodedNullable.Value.Clone();

                if (decoded.TryGetProperty("delivereds", out var gDelEl) && gDelEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var d in gDelEl.EnumerateArray())
                    {
                        var delivered = JsonSerializer.Deserialize<GroupDeliveredMessage>(d.GetRawText());
                        if (delivered != null)
                        {
                            _logger.Info($"✅ [DELIVERED] groupId={delivered.GroupId}");
                            DeliveredReceived?.Invoke(this, new ZaloSeenDeliveredEventArgs { Type = "delivered", ThreadId = delivered.GroupId });
                        }
                    }
                }
                if (decoded.TryGetProperty("groupSeens", out var gSeenEl) && gSeenEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in gSeenEl.EnumerateArray())
                    {
                        var seen = JsonSerializer.Deserialize<GroupSeenMessage>(s.GetRawText());
                        if (seen != null)
                        {
                            _logger.Info($"👁 [SEEN] groupId={seen.GroupId}");
                            SeenReceived?.Invoke(this, new ZaloSeenDeliveredEventArgs { Type = "seen", ThreadId = seen.GroupId });
                        }
                    }
                }
                return;
            }

            // ===== Typing events (cmd=602) =====
            if (version == 1 && cmd == 602 && subCmd == 0)
            {
                var decodedNullable = await DecodeEventPayloadAsync(parsed);
                if (decodedNullable == null) return;
                var decoded = decodedNullable.Value.Clone();
                if (!decoded.TryGetProperty("actions", out var actionsEl) || actionsEl.ValueKind != JsonValueKind.Array)
                    return;

                foreach (var action in actionsEl.EnumerateArray())
                {
                    if (!action.TryGetProperty("act_type", out var aType) || aType.GetString() != "typing") continue;
                    if (!action.TryGetProperty("act", out var actEl)) continue;

                    var dataStr = action.TryGetProperty("data", out var dEl) ? dEl.GetString() : null;
                    if (string.IsNullOrEmpty(dataStr)) continue;

                    try
                    {
                        var dataObj = JsonSerializer.Deserialize<JsonElement>(dataStr!);
                        var args = new ZaloTypingEventArgs();
                        if (actEl.GetString() == "gtyping")
                        {
                            args.IsGroup = true;
                            args.ThreadId = dataObj.TryGetProperty("gid", out var gid) ? gid.GetString() ?? "" : "";
                            args.Uid = dataObj.TryGetProperty("uid", out var uid) ? uid.GetString() ?? "" : "";
                            _logger.Verbose($"⌨ [GTYPING] group={args.ThreadId} uid={args.Uid}");
                        }
                        else
                        {
                            args.IsGroup = false;
                            args.ThreadId = dataObj.TryGetProperty("uid", out var uid2) ? uid2.GetString() ?? "" : "";
                            args.Uid = dataObj.TryGetProperty("uid", out var uid3) ? uid3.GetString() ?? "" : "";
                            _logger.Verbose($"⌨ [TYPING] uid={args.Uid}");
                        }
                        TypingReceived?.Invoke(this, args);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn("  Failed to parse typing data:", ex.Message);
                    }
                }
                return;
            }

            // ===== Old messages (cmd=510, 511) =====
            if (cmd == 510 && subCmd == 1)
            {
                var decodedNullable = await DecodeEventPayloadAsync(parsed);
                if (decodedNullable == null) return;
                var decoded = decodedNullable.Value.Clone();
                if (!decoded.TryGetProperty("msgs", out var oldMsgsEl) || oldMsgsEl.ValueKind != JsonValueKind.Array)
                    return;

                foreach (var msgEl in oldMsgsEl.EnumerateArray())
                {
                    var msgData = JsonSerializer.Deserialize<MessageData>(msgEl.GetRawText());
                    if (msgData != null)
                    {
                        var msg = new UserMessageInfo(_context.Uid.ToString(), msgData);
                        _logger.Verbose($"📜 [OLD USER MSG] from={msg.Data?.UidFrom}");
                        MessageReceived?.Invoke(this, new ZaloMessageEventArgs
                        { Type = "old_user_message", Message = msg, IsGroup = false, IsSelf = msg.IsSelf });
                    }
                }
                return;
            }

            if (cmd == 511 && subCmd == 1)
            {
                var decodedNullable = await DecodeEventPayloadAsync(parsed);
                if (decodedNullable == null) return;
                var decoded = decodedNullable.Value.Clone();
                if (!decoded.TryGetProperty("groupMsgs", out var oldGrpMsgsEl) || oldGrpMsgsEl.ValueKind != JsonValueKind.Array)
                    return;

                foreach (var msgEl in oldGrpMsgsEl.EnumerateArray())
                {
                    var msgData = JsonSerializer.Deserialize<MessageData>(msgEl.GetRawText());
                    if (msgData != null)
                    {
                        var msg = new GroupMessageInfo(_context.Uid.ToString(), msgData);
                        _logger.Verbose($"📜 [OLD GROUP MSG] group={msg.ThreadId} from={msg.Data?.UidFrom}");
                        MessageReceived?.Invoke(this, new ZaloMessageEventArgs
                        { Type = "old_group_message", Message = msg, IsGroup = true, IsSelf = msg.IsSelf });
                    }
                }
                return;
            }

            // Duplicate connection
            if (version == 1 && cmd == 3000 && subCmd == 0)
            {
                _logger.Error("⚠ Another connection is opened, closing this one");
                await StopAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to process WebSocket message: {ex.Message}");
        }
    }

    private async Task<JsonElement?> DecodeEventPayloadAsync(JsonElement parsed)
    {
        if (!parsed.TryGetProperty("data", out var dataStrEl) || dataStrEl.ValueKind != JsonValueKind.String)
            return null;
        if (!parsed.TryGetProperty("encrypt", out var encEl) || encEl.ValueKind != JsonValueKind.Number)
            return null;

        var encryptType = encEl.GetInt32();
        var rawData = dataStrEl.GetString() ?? "";

        var decoded = await ZaloUtils.DecodeEventData<JsonElement>(rawData, encryptType, _cipherKey);
        if (decoded.ValueKind != JsonValueKind.Undefined)
            return decoded.Clone();
        return null;
    }

    private void StartPingLoop()
    {
        _ = Task.Run(async () =>
        {
            while (_webSocket != null && _webSocket.State == WebSocketState.Open && !_cts!.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(30000, _cts.Token);
                    var pingData = new { version = 1, cmd = 2, subCmd = 1, data = new { eventId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() } };
                    var json = JsonSerializer.Serialize(pingData);
                    await _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, _cts.Token);
                    _logger.Verbose("🏓 Ping sent");
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { _logger.Error("Ping failed:", ex.Message); break; }
            }
        });
    }

    private string GetCookieString()
    {
        try
        {
            var uri = new Uri("https://chat.zalo.me");
            var cookies = _context.CookieContainer.GetCookies(uri);
            var sb = new StringBuilder();
            foreach (System.Net.Cookie cookie in cookies)
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

    private string BuildWebSocketUrl()
    {
        var urls = _context.ZpwWsUrls ?? Array.Empty<string>();
        var url = urls.Length > 0 ? urls[_currentUrlIndex % urls.Length] : "wss://wpa.chat.zalo.me:443";
        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = "wss://" + url.Substring(8);
        return ZaloUtils.MakeUrl(url, new Dictionary<string, string> { ["t"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() });
    }

    /// <summary>
    /// Sends a WebSocket payload with optional auto-incrementing request ID.
    /// Equivalent to sendWs() in zca-js listen.ts.
    /// </summary>
    /// <param name="version">Protocol version.</param>
    /// <param name="cmd">Command code.</param>
    /// <param name="subCmd">Sub-command code.</param>
    /// <param name="data">Payload data object.</param>
    /// <param name="requireId">If true, adds a "req_id" field to the data.</param>
    public async Task SendWsAsync(byte version, ushort cmd, byte subCmd, Dictionary<string, object> data, bool requireId = true)
    {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            return;

        if (requireId)
            data["req_id"] = $"req_{_id++}";

        var json = JsonSerializer.Serialize(data);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        var header = new byte[4 + jsonBytes.Length];
        header[0] = version;
        header[1] = (byte)(cmd & 0xFF);
        header[2] = (byte)((cmd >> 8) & 0xFF);
        header[3] = subCmd;
        Array.Copy(jsonBytes, 0, header, 4, jsonBytes.Length);

        await _webSocket.SendAsync(new ArraySegment<byte>(header), WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    /// <summary>
    /// Request old messages from a thread.
    /// Equivalent to requestOldMessages() in zca-js.
    /// </summary>
    /// <param name="threadType">ThreadType.User or ThreadType.Group.</param>
    /// <param name="lastMsgId">Optional last message ID for pagination.</param>
    public async Task RequestOldMessages(ThreadType threadType, string? lastMsgId = null)
    {
        var data = new Dictionary<string, object>
        {
            ["first"] = true,
            ["lastId"] = lastMsgId ?? "",
            ["preIds"] = Array.Empty<object>(),
        };

        await SendWsAsync(1, threadType == ThreadType.User ? (ushort)510 : (ushort)511, 1, data);
    }

    /// <summary>
    /// Request old reactions from a thread.
    /// Equivalent to requestOldReactions() in zca-js.
    /// </summary>
    /// <param name="threadType">ThreadType.User or ThreadType.Group.</param>
    /// <param name="lastMsgId">Optional last message ID for pagination.</param>
    public async Task RequestOldReactions(ThreadType threadType, string? lastMsgId = null)
    {
        var data = new Dictionary<string, object>
        {
            ["first"] = true,
            ["lastId"] = lastMsgId ?? "",
            ["preIds"] = Array.Empty<object>(),
        };

        await SendWsAsync(1, threadType == ThreadType.User ? (ushort)610 : (ushort)611, 1, data);
    }

    /// <summary>
    /// Checks if the given close code allows retry with configured delay.
    /// </summary>
    private int? CanRetry(int closeCode)
    {
        if (_closeAndRetryCodes.Length > 0 && !_closeAndRetryCodes.Contains(closeCode))
            return null;
        if (_retryCount >= _maxRetries)
            return null;

        var idx = Math.Min(_retryCount, _retryTimes.Length - 1);
        var retryDelay = _retryTimes[Math.Max(0, idx)];
        _retryCount++;
        _logger.Verbose($"Retry for code {closeCode} in {retryDelay}ms ({_retryCount}/{_maxRetries})");
        return retryDelay;
    }

    /// <summary>
    /// Checks if the given close code should trigger endpoint rotation.
    /// </summary>
    private bool ShouldRotate(int closeCode)
    {
        if (_rotateErrorCodes.Length > 0 && !_rotateErrorCodes.Contains(closeCode))
            return false;
        var urls = _context.ZpwWsUrls ?? Array.Empty<string>();
        if (_rotateCount >= urls.Length - 1)
            return false;
        return true;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _webSocket?.Dispose();
            _disposed = true;
        }
    }
}