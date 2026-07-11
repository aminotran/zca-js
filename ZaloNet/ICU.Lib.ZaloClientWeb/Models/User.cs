using System.Collections.Generic;
using ICU.Lib.ZaloClientWeb.Models.Types;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Full user information returned by Zalo API.
/// Equivalent to User type in zca-js (src/models/User.ts).
/// </summary>
public class UserInfo
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ZaloName { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;
    public string BgAvatar { get; set; } = string.Empty;
    public string Cover { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public long Dob { get; set; }
    public string Sdob { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public int IsFr { get; set; }
    public int IsBlocked { get; set; }
    public long LastActionTime { get; set; }
    public long LastUpdateTime { get; set; }
    public int IsActive { get; set; }
    public int IsActivePC { get; set; }
    public int IsActiveWeb { get; set; }
    public int IsValid { get; set; }
    public string UserKey { get; set; } = string.Empty;
    public int AccountStatus { get; set; }
    public string GlobalId { get; set; } = string.Empty;
    public long CreatedTs { get; set; }
    public int UserMode { get; set; }
    public ZBusinessPackage? BizPkg { get; set; }
}

/// <summary>
/// Basic user information (minimal profile).
/// Equivalent to UserBasic type in zca-js.
/// </summary>
public class UserBasicInfo
{
    public string Uid { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;
    public string Cover { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public long Dob { get; set; }
    public string Sdob { get; set; } = string.Empty;
    public string GlobalId { get; set; } = string.Empty;
    public string ZaloName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ZBusinessPackage? BizPkg { get; set; }
}

/// <summary>
/// User privacy and notification settings.
/// Equivalent to UserSetting type in zca-js.
/// </summary>
public class UserSetting
{
    public int AddFriendViaContact { get; set; }
    public int DisplayOnRecommendFriend { get; set; }
    public int AddFriendViaGroup { get; set; }
    public int AddFriendViaQr { get; set; }
    public int QuickMessageStatus { get; set; }
    public bool ShowOnlineStatus { get; set; }
    public int AcceptStrangerCall { get; set; }
    public int ArchivedChatStatus { get; set; }
    public int ReceiveMessage { get; set; }
    public int AddFriendViaPhone { get; set; }
    public int DisplaySeenStatus { get; set; }
    public int ViewBirthday { get; set; }
    public int Setting2FAStatus { get; set; }
}