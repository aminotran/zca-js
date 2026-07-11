namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Zalo reaction icons.
/// Equivalent to Reactions enum in zca-js.
/// </summary>
public static class Reactions
{
    public const string Heart = "/-heart";
    public const string Like = "/-strong";
    public const string Haha = ":>";
    public const string Wow = ":o";
    public const string Cry = ":-((";
    public const string Angry = ":-h";
    public const string Kiss = ":-*";
    public const string TearsOfJoy = ":')";
    public const string Shit = "/-shit";
    public const string Rose = "/-rose";
    public const string BrokenHeart = "/-break";
    public const string Dislike = "/-weak";
    public const string Love = ";xx";
    public const string Confused = ";-/";
    public const string Wink = ";-)";
    public const string Fade = "/-fade";
    public const string Sun = "/-li";
    public const string Birthday = "/-bd";
    public const string Bomb = "/-bome";
    public const string Ok = "/-ok";
    public const string Peace = "/-v";
    public const string Thanks = "/-thanks";
    public const string Punch = "/-punch";
    public const string Share = "/-share";
    public const string Pray = "_()_";
    public const string No = "/-no";
    public const string Bad = "/-bad";
    public const string LoveYou = "/-loveu";
    public const string Sad = "--b";
    public const string VerySad = ":((";
    public const string Cool = "x-)";
    public const string Nerd = "8-)";
    public const string BigSmile = ";-d";
    public const string Sunglasses = "b-)";
    public const string Neutral = ":--|";
    public const string SadFace = "p-(";
    public const string Bye = ":-bye";
    public const string Sleepy = "|-)";
    public const string Wipe = ":wipe";
    public const string Dig = ":-dig";
    public const string Anguish = "&-(";
    public const string Handclap = ":handclap";
    public const string AngryFace = ">-|";
    public const string FChair = ":-f";
    public const string LChair = ":-l";
    public const string RChair = ":-r";
    public const string Silent = ";-x";
    public const string Surprise = ":-o";
    public const string Embarrassed = ";-s";
    public const string Afraid = ";-a";
    public const string Sad2 = ":-<";
    public const string BigLaugh = ":))";
    public const string Rich = "$-)";
    public const string Beer = "/-beer";
    public const string None = "";
}

/// <summary>
/// Reaction data from Zalo.
/// Equivalent to TReaction in zca-js.
/// </summary>
public class ReactionData
{
    public string ActionId { get; set; } = string.Empty;
    public string MsgId { get; set; } = string.Empty;
    public string CliMsgId { get; set; } = string.Empty;
    public string MsgType { get; set; } = string.Empty;
    public string UidFrom { get; set; } = string.Empty;
    public string IdTo { get; set; } = string.Empty;
    public string? DName { get; set; }
    public ReactionContent? Content { get; set; }
    public string Ts { get; set; } = string.Empty;
    public int Ttl { get; set; }
}

/// <summary>
/// Reaction content details.
/// </summary>
public class ReactionContent
{
    public ReactionMsgItem[]? RMsg { get; set; }
    public string RIcon { get; set; } = string.Empty;
    public int RType { get; set; }
    public int Source { get; set; }
}

/// <summary>
/// Individual message reference within a reaction.
/// </summary>
public class ReactionMsgItem
{
    public string GMsgID { get; set; } = string.Empty;
    public string CMsgID { get; set; } = string.Empty;
    public int MsgType { get; set; }
}

/// <summary>
/// Represents a reaction on a message.
/// Equivalent to Reaction class in zca-js.
/// </summary>
public class Reaction
{
    public ReactionData Data { get; }
    public string ThreadId { get; }
    public bool IsSelf { get; }
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