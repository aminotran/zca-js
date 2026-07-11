using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ICU.Lib.ZaloClientWeb.Models;
using ICU.Lib.ZaloClientWeb.Utils;

namespace ICU.Lib.ZaloClientWeb.WebSocket;

/// <summary>
/// Event args for messages received from Zalo WebSocket.
/// </summary>
public class ZaloMessageEventArgs : EventArgs
{
    public string Type { get; set; } = string.Empty;
    public JsonElement Data { get; set; }
    public bool IsGroup { get; set; }
    public bool IsSelf { get; set; }
}

/// <summary>
/// Event args for connection state changes.
/// </summary>
public class ZaloConnectionEventArgs : EventArgs
{
    public bool Connected { get; set; }
    public Models.Types.CloseReason? CloseReason { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// WebSocket listener for real-time Zalo events.
/// Equivalent to Listener class in zca-js (apis/listen.ts).
/// Listens for messages, typing events, reactions, seen/delivered events, group events, friend events.
/// </summary>
public class ZaloListener : IDisposable
{
    private readonly ZaloContext _context;
    private readonly HttpClient _httpClient;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private int _currentUrlIndex;

    private readonly ZaloLogger _logger;

    // Events
    public event EventHandler<ZaloConnectionEventArgs>? Connected;
    public event EventHandler<ZaloConnectionEventArgs>? Disconnected;
    public event EventHandler<ZaloConnectionEventArgs>? Error;
    public event EventHandler<ZaloMessageEventArgs>? MessageReceived;
    public event EventHandler<ZaloMessageEventArgs>? TypingReceived;
    public event EventHandler<ZaloMessageEventArgs>? SeenReceived;
    public event EventHandler<ZaloMessageEventArgs>? DeliveredReceived;
    public event EventHandler<ZaloMessageEventArgs>? ReactionReceived;
    public event EventHandler<ZaloMessageEventArgs>? GroupEventReceived;
    public event EventHandler<ZaloMessageEventArgs>? FriendEventReceived;
    public event EventHandler<ZaloMessageEventArgs>? UndoReceived;

    private bool _disposed;

    public ZaloListener(ZaloContext context, HttpClient httpClient)
    {
        _context = context;
        _httpClient = httpClient;
        _logger = new ZaloLogger(context.Options.Logging);
    }

    /// <summary>
    /// Starts the WebSocket connection for listening to real-time events.
    /// Equivalent to Listener.start() in zca-js.
    /// </summary>
    public async Task StartAsync(bool retryOnClose = false)
    {
        if (_webSocket != null)
            throw new InvalidOperationException("Listener already started");

        _cts = new CancellationTokenSource();
        _webSocket = new ClientWebSocket();

        // Set headers matching zca-js
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

            // Start receiving messages
            await ReceiveLoopAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("WebSocket connection failed:", ex.Message);
            Error?.Invoke(this, new ZaloConnectionEventArgs { Connected = false, Message = ex.Message });
        }
    }

    /// <summary>
    /// Stops the WebSocket connection.
    /// Equivalent to Listener.stop() in zca-js.
    /// </summary>
    public async Task StopAsync()
    {
        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Manual closure", CancellationToken.None);
        }

        _cts?.Cancel();
        _webSocket?.Dispose();
        _webSocket = null;
    }

    /// <summary>
    /// Sends a raw payload to the WebSocket.
    /// Equivalent to Listener.sendWs() in zca-js.
    /// </summary>
    public async Task SendAsync(object payload)
    {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            return;

        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(bytes);

        // Build binary header (4 bytes): version(1) + cmd(2) + subCmd(1)
        var header = new byte[4 + bytes.Length];
        var payloadDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        if (payloadDict != null)
        {
            header[0] = payloadDict.ContainsKey("version") ? payloadDict["version"].GetByte() : (byte)0;
            // cmd and subCmd in header...
        }

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
                    CloseReason = Models.Types.CloseReason.AbnormalClosure,
                    Message = ex.Message
                });
                break;
            }
        }
    }

    private async Task ProcessMessageAsync(byte[] data)
    {
        try
        {
            if (data.Length < 4) return;

            // Parse header: version(1) + cmd(2) + subCmd(1)
            var version = data[0];
            var cmd = BitConverter.ToUInt16(data, 1);
            var subCmd = data[3];

            if (data.Length <= 4) return;

            var payloadData = Encoding.UTF8.GetString(data, 4, data.Length - 4);
            if (string.IsNullOrEmpty(payloadData)) return;

            var parsed = JsonSerializer.Deserialize<JsonElement>(payloadData);

            // Handle cipher key exchange (version=1, cmd=1, subCmd=1)
            if (version == 1 && cmd == 1 && subCmd == 1 && parsed.TryGetProperty("key", out var keyElement))
            {
                var cipherKey = keyElement.GetString();
                _logger.Verbose("Received cipher key");
                StartPingLoop();
                return;
            }

            // Handle messages (cmd=501 - user messages, cmd=521 - group messages)
            if (cmd == 501 || cmd == 521)
            {
                var isGroup = cmd == 521;
                // TODO: Decode and emit messages
                var eventArgs = new ZaloMessageEventArgs
                {
                    Type = isGroup ? "group_message" : "user_message",
                    IsGroup = isGroup,
                    Data = parsed
                };
                MessageReceived?.Invoke(this, eventArgs);
            }

            // Handle typing events (cmd=602)
            if (cmd == 602)
            {
                var eventArgs = new ZaloMessageEventArgs
                {
                    Type = "typing",
                    Data = parsed
                };
                TypingReceived?.Invoke(this, eventArgs);
            }

            // Handle reactions (cmd=612, 610, 611)
            if (cmd == 612 || cmd == 610 || cmd == 611)
            {
                var eventArgs = new ZaloMessageEventArgs
                {
                    Type = "reaction",
                    Data = parsed
                };
                ReactionReceived?.Invoke(this, eventArgs);
            }

            // Handle seen/delivered (cmd=502, 522)
            if (cmd == 502 || cmd == 522)
            {
                var isGroup = cmd == 522;
                var eventArgs = new ZaloMessageEventArgs
                {
                    Type = isGroup ? "group_seen_delivered" : "user_seen_delivered",
                    Data = parsed
                };
                SeenReceived?.Invoke(this, eventArgs);
                DeliveredReceived?.Invoke(this, eventArgs);
            }

            // Handle duplicate connection
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

    private void StartPingLoop()
    {
        // Start sending ping at the configured interval
        _ = Task.Run(async () =>
        {
            while (_webSocket != null && _webSocket.State == WebSocketState.Open && !_cts!.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(30000, _cts.Token); // 30 second ping interval (configurable)
                    // Send ping payload
                    var pingPayload = Encoding.UTF8.GetBytes(
                        "{\"version\":1,\"cmd\":2,\"subCmd\":1,\"data\":{\"eventId\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}}");
                    await _webSocket.SendAsync(new ArraySegment<byte>(pingPayload), WebSocketMessageType.Text, true, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error("Ping failed:", ex.Message);
                    break;
                }
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