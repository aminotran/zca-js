namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Zalo reaction icons — string constants for all supported reaction types.
/// Equivalent to Reactions enum in zca-js.
/// <para>Usage: Pass the icon string to <c>AddReactionAsync(messageId, Reactions.Heart)</c>.</para>
/// </summary>
public static class Reactions
{
    /// <summary>❤️ Red heart.</summary>
    public const string Heart = "/-heart";
    /// <summary>👍 Thumbs up / like.</summary>
    public const string Like = "/-strong";
    /// <summary>😂 Laughing / haha.</summary>
    public const string Haha = ":>";
    /// <summary>😮 Wow / surprised.</summary>
    public const string Wow = ":o";
    /// <summary>😢 Crying / sad cry.</summary>
    public const string Cry = ":-((";
    /// <summary>😠 Angry.</summary>
    public const string Angry = ":-h";
    /// <summary>😘 Kiss / blowing a kiss.</summary>
    public const string Kiss = ":-*";
    /// <summary>😂 Tears of joy / laughing to tears.</summary>
    public const string TearsOfJoy = ":')";
    /// <summary>💩 Shit / poo emoji.</summary>
    public const string Shit = "/-shit";
    /// <summary>🌹 Rose / flower.</summary>
    public const string Rose = "/-rose";
    /// <summary>💔 Broken heart.</summary>
    public const string BrokenHeart = "/-break";
    /// <summary>👎 Dislike / thumbs down.</summary>
    public const string Dislike = "/-weak";
    /// <summary>❤️ Love / heart eyes.</summary>
    public const string Love = ";xx";
    /// <summary>😕 Confused / unsure.</summary>
    public const string Confused = ";-/";
    /// <summary>😉 Wink.</summary>
    public const string Wink = ";-)";
    /// <summary>😶 Fade / disappearing.</summary>
    public const string Fade = "/-fade";
    /// <summary>☀️ Sun.</summary>
    public const string Sun = "/-li";
    /// <summary>🎂 Birthday / cake.</summary>
    public const string Birthday = "/-bd";
    /// <summary>💣 Bomb.</summary>
    public const string Bomb = "/-bome";
    /// <summary>👌 OK / hand gesture.</summary>
    public const string Ok = "/-ok";
    /// <summary>✌️ Peace / victory sign.</summary>
    public const string Peace = "/-v";
    /// <summary>🙏 Thanks / prayer hands.</summary>
    public const string Thanks = "/-thanks";
    /// <summary>👊 Punch / fist bump.</summary>
    public const string Punch = "/-punch";
    /// <summary>📤 Share.</summary>
    public const string Share = "/-share";
    /// <summary>🙏 Pray / person praying.</summary>
    public const string Pray = "_()_";
    /// <summary>🚫 No / forbidden.</summary>
    public const string No = "/-no";
    /// <summary>👎 Bad / thumbs down.</summary>
    public const string Bad = "/-bad";
    /// <summary>💜 Love you / purple heart.</summary>
    public const string LoveYou = "/-loveu";
    /// <summary>😞 Sad / disappointed.</summary>
    public const string Sad = "--b";
    /// <summary>😭 Very sad / crying loudly.</summary>
    public const string VerySad = ":((";
    /// <summary>😎 Cool / sunglasses.</summary>
    public const string Cool = "x-)";
    /// <summary>🤓 Nerd / geek.</summary>
    public const string Nerd = "8-)";
    /// <summary>😁 Big smile / grinning.</summary>
    public const string BigSmile = ";-d";
    /// <summary>😎 Sunglasses / chill.</summary>
    public const string Sunglasses = "b-)";
    /// <summary>😐 Neutral / straight face.</summary>
    public const string Neutral = ":--|";
    /// <summary>😞 Sad face / pouting.</summary>
    public const string SadFace = "p-(";
    /// <summary>👋 Bye / waving.</summary>
    public const string Bye = ":-bye";
    /// <summary>😴 Sleepy / tired.</summary>
    public const string Sleepy = "|-)";
    /// <summary>🫢 Wipe / wiping sweat.</summary>
    public const string Wipe = ":wipe";
    /// <summary>⛏️ Dig / digging.</summary>
    public const string Dig = ":-dig";
    /// <summary>😧 Anguish / distressed.</summary>
    public const string Anguish = "&-(";
    /// <summary>👏 Handclap / applauding.</summary>
    public const string Handclap = ":handclap";
    /// <summary>😤 Angry face / frustrated.</summary>
    public const string AngryFace = ">-|";
    /// <summary>💺 F-chair / flipping chair (meme).</summary>
    public const string FChair = ":-f";
    /// <summary>💺 L-chair / chair (meme variant).</summary>
    public const string LChair = ":-l";
    /// <summary>💺 R-chair / chair (meme variant).</summary>
    public const string RChair = ":-r";
    /// <summary>🤫 Silent / shush.</summary>
    public const string Silent = ";-x";
    /// <summary>😲 Surprise / shocked.</summary>
    public const string Surprise = ":-o";
    /// <summary>😳 Embarrassed / blushing.</summary>
    public const string Embarrassed = ";-s";
    /// <summary>😨 Afraid / scared.</summary>
    public const string Afraid = ";-a";
    /// <summary>😞 Sad variant.</summary>
    public const string Sad2 = ":-<";
    /// <summary>😂 Big laugh / ROFL.</summary>
    public const string BigLaugh = ":))";
    /// <summary>🤑 Rich / money mouth.</summary>
    public const string Rich = "$-)";
    /// <summary>🍺 Beer / cheers.</summary>
    public const string Beer = "/-beer";
    /// <summary>(none) Remove reaction / no reaction.</summary>
    public const string None = "";
}

/// <summary>
/// Reaction data from Zalo event.
/// Equivalent to TReaction in zca-js.
/// </summary>
public class ReactionData
{
    /// <summary>Unique action ID for this reaction event.</summary>
    public string ActionId { get; set; } = string.Empty;
    /// <summary>Server message ID that the reaction applies to.</summary>
    public string MsgId { get; set; } = string.Empty;
    /// <summary>Client message ID.</summary>
    public string CliMsgId { get; set; } = string.Empty;
    /// <summary>Type of the message being reacted to.</summary>
    public string MsgType { get; set; } = string.Empty;
    /// <summary>Sender UID of the reaction. "0" if current user.</summary>
    public string UidFrom { get; set; } = string.Empty;
    /// <summary>Recipient UID or Group ID.</summary>
    public string IdTo { get; set; } = string.Empty;
    /// <summary>Optional display name of the reactor.</summary>
    public string? DName { get; set; }
    /// <summary>Reaction content including icon and message references.</summary>
    public ReactionContent? Content { get; set; }
    /// <summary>Timestamp of the reaction in milliseconds.</summary>
    public string Ts { get; set; } = string.Empty;
    /// <summary>Time-to-live in seconds.</summary>
    public int Ttl { get; set; }
}

/// <summary>
/// Reaction content details — which messages were reacted to and with what icon.
/// </summary>
public class ReactionContent
{
    /// <summary>Array of message references this reaction applies to.</summary>
    public ReactionMsgItem[]? RMsg { get; set; }
    /// <summary>Reaction icon string (see <see cref="Reactions"/> constants).</summary>
    public string RIcon { get; set; } = string.Empty;
    /// <summary>Reaction type: 0 = add reaction, 1 = remove reaction.</summary>
    public int RType { get; set; }
    /// <summary>Source: 0 = from user, 1 = from system.</summary>
    public int Source { get; set; }
}

/// <summary>
/// Individual message reference within a reaction event.
/// </summary>
public class ReactionMsgItem
{
    /// <summary>Global (server-side) message ID.</summary>
    public string GMsgID { get; set; } = string.Empty;
    /// <summary>Client message ID.</summary>
    public string CMsgID { get; set; } = string.Empty;
    /// <summary>Message type: 1=text, 31=voice, 32=photo, etc.</summary>
    public int MsgType { get; set; }
}

/// <summary>
/// Represents a reaction event (add/remove reaction on a message).
/// Equivalent to Reaction class in zca-js.
/// </summary>
public class Reaction
{
    /// <summary>Raw reaction data from Zalo API.</summary>
    public ReactionData Data { get; }
    /// <summary>Thread/conversation ID where the reaction occurred.</summary>
    public string ThreadId { get; }
    /// <summary>True if the reaction was made by the current logged-in user.</summary>
    public bool IsSelf { get; }
    /// <summary>True if the reaction is in a group chat.</summary>
    public bool IsGroup { get; }

    public Reaction(string uid, ReactionData data, bool isGroup)
    {
        Data = data;
        ThreadId = isGroup || data.UidFrom == "0" ? data.IdTo : data.UidFrom;
        IsSelf = data.UidFrom == "0";
        IsGroup = isGroup;
        if (data.IdTo == "0") data.IdTo = uid;
        if (data.UidFrom == "0") data.UidFrom = uid;
    }
}