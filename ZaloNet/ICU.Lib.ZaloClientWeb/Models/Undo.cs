using System.Text.Json;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Represents an undo (message recall) event.
/// Equivalent to Undo class in zca-js.
/// </summary>
public class UndoEvent
{
    public string ActionId { get; set; } = string.Empty;
    public string MsgId { get; set; } = string.Empty;
    public string CliMsgId { get; set; } = string.Empty;
    public string UidFrom { get; set; } = string.Empty;
    public string IdTo { get; set; } = string.Empty;
    public long Ts { get; set; }
    public string ThreadId { get; }
    public bool IsSelf { get; }
    public bool IsGroup { get; }

    public UndoEvent(string uid, JsonElement msg, bool isGroup)
    {
        IsGroup = isGroup;
        IsSelf = msg.TryGetProperty("uidFrom", out var uidFrom) && uidFrom.GetString() == "0";

        ActionId = msg.TryGetProperty("actionId", out var actionId) ? actionId.GetString() ?? "" : "";
        MsgId = msg.TryGetProperty("msgId", out var msgId) ? msgId.GetString() ?? "" : "";
        CliMsgId = msg.TryGetProperty("cliMsgId", out var cliMsgId) ? cliMsgId.GetString() ?? "" : "";
        UidFrom = uidFrom.ValueKind == JsonValueKind.String ? uidFrom.GetString() ?? "" : "";
        IdTo = msg.TryGetProperty("idTo", out var idTo) ? idTo.GetString() ?? "" : "";
        Ts = msg.TryGetProperty("ts", out var ts) && ts.ValueKind == JsonValueKind.String
            ? long.TryParse(ts.GetString(), out var t) ? t : 0
            : 0;

        ThreadId = isGroup || UidFrom == "0" ? IdTo : UidFrom;

        if (IdTo == "0") IdTo = uid;
        if (UidFrom == "0") UidFrom = uid;
    }
}