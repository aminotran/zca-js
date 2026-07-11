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

/// <summary>
/// Event args for connection state changes on the Zalo WebSocket.
/// </summary>
public class ZaloConnectionEventArgs : EventArgs
{
    public bool Connected { get; set; }
    public CloseReason? CloseReason { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Event args for a new message (user or group) received in real-time.
/// <c>Message</c> will be either <see cref="UserMessageInfo"/> or <see cref="GroupMessageInfo"/>.
/// </summary>
public class ZaloMessageEventArgs : EventArgs
{
    /// <summary>"user_message" or "group_message"</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>The parsed message object (UserMessageInfo or GroupMessageInfo).</summary>
    public object? Message { get; set; }
    /// <summary>True if this is a group message.</summary>
    public bool IsGroup { get; set; }
    /// <summary>True if the message was sent by the current logged-in user.</summary>
    public bool IsSelf { get; set; }
}

/// <summary>
/// Event args for a typing indicator event.
/// </summary>
public class ZaloTypingEventArgs : EventArgs
{
    /// <summary>"typing" or "gtyping"</summary>
    public string Act { get; set; } = string.Empty;
    /// <summary>Thread ID where the typing is occurring.</summary>
    public string ThreadId { get; set; } = string.Empty;
    /// <summary>User ID who is typing.</summary>
    public string Uid { get; set; } = string.Empty;
    /// <summary>Timestamp of the event.</summary>
    public long Ts { get; set; }
    /// <summary>Is this a group typing event?</summary>
    public bool IsGroup { get; set; }
}

/// <summary>
/// Event args for a reaction event (add/remove reaction).
/// </summary>
public class ZaloReactionEventArgs : EventArgs
{
    /// <summary>"reaction", "old_reactions"</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>List of reaction data received.</summary>
    public List<Reaction> Reactions { get; set; } = new();
}

/// <summary>
/// Event args for a seen/delivered event.
/// </summary>
public class ZaloSeenDeliveredEventArgs : EventArgs
{
    /// <summary>"seen", "delivered"</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>Thread/conversation ID.</summary>
    public string ThreadId { get; set; } = string.Empty;
    /// <summary>List of user IDs who saw/delivered the message.</summary>
    public List<string> UserIds { get; set; } = new();
}

/// <summary>
/// Event args for an undo (message recall) event.
/// </summary>
public class ZaloUndoEventArgs : EventArgs
{
    /// <summary>Undo event data.</summary>
    public UndoEvent? Undo { get; set; }
}

/// <summary>
/// WebSocket listener for real-time Zalo events.
/// Equivalent to Listener class in zca-js (apis/listen.ts).
/// </summary>
public class ZaloListener : IDisposable
{
    private readonly ZaloContext _context;
    private readonly HttpClient _httpClient;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private int _currentUrlIndex;
    private string? _cipherKey;

    private readonly ZaloLogger _logger;

    // Events
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
    }

    /// <summary>
    /// Starts the WebSocket connection.
    /// </summary>
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

    /// <summary>
    /// Sends a WebSocket payload with binary header.
    /// </summary>
    public async Task SendAsync(byte version, ushort cmd, byte subCmd, object data)
    {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            return;

        var json = JsonSerializer.Serialize(data);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        // Build binary header (4 bytes): version(1) + cmd(2, LE) + subCmd(1)
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

            var parsed = JsonSerializer.Deserialize<JsonElement>(payloadData);

            // Cipher key exchange (cmd=1, subCmd=1)
            if (version == 1 && cmd == 1 && subCmd == 1 && parsed.TryGetProperty("key", out var keyEl))
            {
                _cipherKey = keyEl.GetString();
                _logger.Verbose("Received cipher key");
                CipherKeyReceived?.Invoke(this, _cipherKey);
                StartPingLoop();
                return;
            }

            // ===== User messages (cmd=501) =====
            if (version == 1 && cmd == 501 && subCmd == 0)
            {
                var decodedNullable = await DecodeEventPayloadAsync(parsed);
                if (decodedNullable == null) return;
                var decoded = decodedNullable.Value;
                if (!decoded.TryGetProperty("msgs", out var msgsEl) || msgsEl.ValueKind != JsonValueKind.Array)
                    return;

                foreach (var msgEl in msgsEl.EnumerateArray())
                {
                    // Check for undo
                    if (msgEl.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Object
                        && content.TryGetProperty("deleteMsg", out _))
                    {
                        var undo = new UndoEvent(_context.Uid.ToString(), msgEl, false);
                        if (!undo.IsSelf || _context.Options.SelfListen)
                            UndoReceived?.Invoke(this, new ZaloUndoEventArgs { Undo = undo });
                        continue;
                    }

                    var msgData = JsonSerializer.Deserialize<MessageData>(msgEl.GetRawText());
                    if (msgData != null)
                    {
                        var msg = new UserMessageInfo(_context.Uid.ToString(), msgData);
                        if (msg.IsSelf && !_context.Options.SelfListen) continue;
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
                var decoded = decodedNullable.Value;
                if (!decoded.TryGetProperty("groupMsgs", out var groupMsgsEl) || groupMsgsEl.ValueKind != JsonValueKind.Array)
                    return;

                foreach (var msgEl in groupMsgsEl.EnumerateArray())
                {
                    if (msgEl.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Object
                        && content.TryGetProperty("deleteMsg", out _))
                    {
                        var undo = new UndoEvent(_context.Uid.ToString(), msgEl, true);
                        if (!undo.IsSelf || _context.Options.SelfListen)
                            UndoReceived?.Invoke(this, new ZaloUndoEventArgs { Undo = undo });
                        continue;
                    }

                    var msgData = JsonSerializer.Deserialize<MessageData>(msgEl.GetRawText());
                    if (msgData != null)
                    {
                        var msg = new GroupMessageInfo(_context.Uid.ToString(), msgData);
                        if (msg.IsSelf && !_context.Options.SelfListen) continue;
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
                var decoded = decodedNullable.Value;
                if (!decoded.TryGetProperty("controls", out var controlsEl) || controlsEl.ValueKind != JsonValueKind.Array)
                    return;

                foreach (var ctrl in controlsEl.EnumerateArray())
                {
                    if (!ctrl.TryGetProperty("content", out var ctrlContent) || ctrlContent.ValueKind != JsonValueKind.Object)
                        continue;

                    // Upload file done
                    if (ctrlContent.TryGetProperty("act_type", out var actType) && actType.GetString() == "file_done")
                    {
                        var fileUrl = ctrlContent.TryGetProperty("data", out var fileData)
                            && fileData.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;
                        var fileId = ctrlContent.TryGetProperty("fileId", out var fidEl) ? fidEl.GetString() : null;
                        // Fire upload callback if any
                        continue;
                    }

                    // Group events
                    if (actType.GetString() == "group")
                    {
                        if (ctrlContent.TryGetProperty("act", out var groupAct))
                        {
                            if (groupAct.GetString() == "join_reject") continue;

                            var dataRaw = ctrlContent.TryGetProperty("data", out var gData)
                                ? gData.ValueKind == JsonValueKind.String
                                    ? JsonDocument.Parse(gData.GetString()!).RootElement
                                    : gData.Clone()
                                : new JsonElement();

                            var groupEvent = GroupEvent.Initialize(
                                _context.Uid.ToString(),
                                dataRaw,
                                (GroupEventType)ZaloUtils.GetGroupEventType(groupAct.GetString() ?? ""),
                                groupAct.GetString() ?? ""
                            );
                            if (groupEvent.IsSelf && !_context.Options.SelfListen) continue;
                            GroupEventReceived?.Invoke(this, groupEvent);
                        }
                        continue;
                    }

                    // Friend events
                    if (actType.GetString() == "fr")
                    {
                        if (!ctrlContent.TryGetProperty("act", out var frAct)) continue;
                        if (frAct.GetString() == "req") continue; // skip duplicate req

                        JsonElement frData;
                        if (ctrlContent.TryGetProperty("data", out var frDataEl))
                        {
                            frData = frDataEl.ValueKind == JsonValueKind.String
                                ? JsonDocument.Parse(frDataEl.GetString()!).RootElement
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
                        FriendEventReceived?.Invoke(this, friendEvent);
                    }
                }
                return;
            }

            // ===== Reactions (cmd=612) =====
            if (version == 1 && cmd == 612 && subCmd == 0)
            {
                var decodedNullable = await DecodeEventPayloadAsync(parsed);
                if (decodedNullable == null) return;
                var decoded = decodedNullable.Value;

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
                    ReactionReceived?.Invoke(this, new ZaloReactionEventArgs { Type = "reaction", Reactions = reactionList });
                return;
            }

            // ===== Old reactions (cmd=610/611) =====
            if (cmd == 610 || cmd == 611)
            {
                var decodedNullable = await DecodeEventPayloadAsync(parsed);
                if (decodedNullable == null) return;
                var decoded = decodedNullable.Value;

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
                var decoded = decodedNullable.Value;

                if (decoded.TryGetProperty("delivereds", out var delEl) && delEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var d in delEl.EnumerateArray())
                    {
                        var delivered = JsonSerializer.Deserialize<UserDeliveredMessage>(d.GetRawText());
                        if (delivered != null)
                            DeliveredReceived?.Invoke(this, new ZaloSeenDeliveredEventArgs
                            { Type = "delivered", ThreadId = delivered.Uid });
                    }
                }
                if (decoded.TryGetProperty("seens", out var seenEl) && seenEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in seenEl.EnumerateArray())
                    {
                        var seen = JsonSerializer.Deserialize<UserSeenMessage>(s.GetRawText());
                        if (seen != null)
                            SeenReceived?.Invoke(this, new ZaloSeenDeliveredEventArgs
                            { Type = "seen", ThreadId = seen.Uid });
                    }
                }
                return;
            }

            // ===== Seen/Delivered group (cmd=522) =====
            if (version == 1 && cmd == 522 && subCmd == 0)
            {
                var decodedNullable = await DecodeEventPayloadAsync(parsed);
                if (decodedNullable == null) return;
                var decoded = decodedNullable.Value;

                if (decoded.TryGetProperty("delivereds", out var gDelEl) && gDelEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var d in gDelEl.EnumerateArray())
                    {
                        var delivered = JsonSerializer.Deserialize<GroupDeliveredMessage>(d.GetRawText());
                        if (delivered != null)
                            DeliveredReceived?.Invoke(this, new ZaloSeenDeliveredEventArgs
                            { Type = "delivered", ThreadId = delivered.GroupId });
                    }
                }
                if (decoded.TryGetProperty("groupSeens", out var gSeenEl) && gSeenEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in gSeenEl.EnumerateArray())
                    {
                        var seen = JsonSerializer.Deserialize<GroupSeenMessage>(s.GetRawText());
                        if (seen != null)
                            SeenReceived?.Invoke(this, new ZaloSeenDeliveredEventArgs
                            { Type = "seen", ThreadId = seen.GroupId });
                    }
                }
                return;
            }

            // ===== Typing events (cmd=602) =====
            if (version == 1 && cmd == 602 && subCmd == 0)
            {
                var decodedNullable = await DecodeEventPayloadAsync(parsed);
                if (decodedNullable == null) return;
                var decoded = decodedNullable.Value;
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
                        }
                        else
                        {
                            args.IsGroup = false;
                            args.ThreadId = dataObj.TryGetProperty("uid", out var uid2) ? uid2.GetString() ?? "" : "";
                            args.Uid = dataObj.TryGetProperty("uid", out var uid3) ? uid3.GetString() ?? "" : "";
                        }
                        TypingReceived?.Invoke(this, args);
                    }
                    catch { }
                }
                return;
            }

            // ===== Old messages (cmd=510, 511) =====
            if (cmd == 510 && subCmd == 1)
            {
                var decodedNullable = await DecodeEventPayloadAsync(parsed);
                if (decodedNullable == null) return;
                var decoded = decodedNullable.Value;
                if (!decoded.TryGetProperty("msgs", out var oldMsgsEl) || oldMsgsEl.ValueKind != JsonValueKind.Array)
                    return;

                foreach (var msgEl in oldMsgsEl.EnumerateArray())
                {
                    var msgData = JsonSerializer.Deserialize<MessageData>(msgEl.GetRawText());
                    if (msgData != null)
                    {
                        var msg = new UserMessageInfo(_context.Uid.ToString(), msgData);
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
                var decoded = decodedNullable.Value;
                if (!decoded.TryGetProperty("groupMsgs", out var oldGrpMsgsEl) || oldGrpMsgsEl.ValueKind != JsonValueKind.Array)
                    return;

                foreach (var msgEl in oldGrpMsgsEl.EnumerateArray())
                {
                    var msgData = JsonSerializer.Deserialize<MessageData>(msgEl.GetRawText());
                    if (msgData != null)
                    {
                        var msg = new GroupMessageInfo(_context.Uid.ToString(), msgData);
                        MessageReceived?.Invoke(this, new ZaloMessageEventArgs
                        { Type = "old_group_message", Message = msg, IsGroup = true, IsSelf = msg.IsSelf });
                    }
                }
                return;
            }

            // Duplicate connection
            if (version == 1 && cmd == 3000 && subCmd == 0)
            {
                _logger.Error("Another connection is opened, closing this one");
                await StopAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to process WebSocket message:", ex.Message);
        }
    }

    /// <summary>
    /// Decodes an encrypted event payload.
    /// Returns the inner "data" field as a JsonElement.
    /// </summary>
    private async Task<JsonElement?> DecodeEventPayloadAsync(JsonElement parsed)
    {
        if (!parsed.TryGetProperty("data", out var dataStrEl) || dataStrEl.ValueKind != JsonValueKind.String)
            return null;
        if (!parsed.TryGetProperty("encrypt", out var encEl) || encEl.ValueKind != JsonValueKind.Number)
            return null;

        var encryptType = encEl.GetInt32();
        var rawData = dataStrEl.GetString() ?? "";

        var result = await ZaloUtils.DecodeEventData<JsonElement>(rawData, encryptType, _cipherKey);
        return result;
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
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { _logger.Error("Ping failed:", ex.Message); break; }
            }
        });
    }

    private string BuildWebSocketUrl()
    {
        var urls = _context.ZpwWsUrls ?? Array.Empty<string>();
        var url = urls.Length > 0 ? urls[_currentUrlIndex % urls.Length] : "wss://wpa.chat.zalo.me:443";
        return ZaloUtils.MakeUrl(url, new Dictionary<string, string> { ["t"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() });
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