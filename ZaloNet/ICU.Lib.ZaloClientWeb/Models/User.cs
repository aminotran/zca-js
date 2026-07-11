using ICU.Lib.ZaloClientWeb.Models.Types;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Full user information returned by Zalo API.
/// Equivalent to User type in zca-js.
/// </summary>
public class UserInfo
{
    /// <summary>Unique user ID.</summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>Zalo username.</summary>
    public string Username { get; set; } = string.Empty;
    /// <summary>Display name visible to others.</summary>
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>Zalo account display name.</summary>
    public string ZaloName { get; set; } = string.Empty;
    /// <summary>Avatar image URL.</summary>
    public string Avatar { get; set; } = string.Empty;
    /// <summary>Background avatar image URL.</summary>
    public string BgAvatar { get; set; } = string.Empty;
    /// <summary>Cover photo URL.</summary>
    public string Cover { get; set; } = string.Empty;
    /// <summary>Gender: 0 = Male, 1 = Female.</summary>
    public Gender Gender { get; set; }
    /// <summary>Date of birth (timestamp in milliseconds).</summary>
    public long Dob { get; set; }
    /// <summary>String-formatted date of birth.</summary>
    public string Sdob { get; set; } = string.Empty;
    /// <summary>User status message/text.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Phone number.</summary>
    public string PhoneNumber { get; set; } = string.Empty;
    /// <summary>Is friend with current user? 0 = no, 1 = yes.</summary>
    public int IsFr { get; set; }
    /// <summary>Is blocked? 0 = no, 1 = yes.</summary>
    public int IsBlocked { get; set; }
    /// <summary>Last online/action time (timestamp in milliseconds).</summary>
    public long LastActionTime { get; set; }
    /// <summary>Last profile update time (timestamp in milliseconds).</summary>
    public long LastUpdateTime { get; set; }
    /// <summary>Is active/online? 0 = offline, 1 = online.</summary>
    public int IsActive { get; set; }
    /// <summary>Is active on PC? 0 = no, 1 = yes.</summary>
    public int IsActivePC { get; set; }
    /// <summary>Is active on Web? 0 = no, 1 = yes.</summary>
    public int IsActiveWeb { get; set; }
    /// <summary>Is a valid user account? 0 = invalid, 1 = valid.</summary>
    public int IsValid { get; set; }
    /// <summary>User key/hash for encryption.</summary>
    public string UserKey { get; set; } = string.Empty;
    /// <summary>Account status code (0=normal, other=restricted/banned).</summary>
    public int AccountStatus { get; set; }
    /// <summary>Global ID for cross-platform identification.</summary>
    public string GlobalId { get; set; } = string.Empty;
    /// <summary>Account creation time (timestamp in milliseconds).</summary>
    public long CreatedTs { get; set; }
    /// <summary>User mode: 0 = personal, 1 = business.</summary>
    public int UserMode { get; set; }
    /// <summary>Zalo Business package info (null for personal accounts).</summary>
    public ZBusinessPackage? BizPkg { get; set; }
}

/// <summary>
/// Basic user information (minimal profile).
/// Equivalent to UserBasic type in zca-js.
/// </summary>
public class UserBasicInfo
{
    /// <summary>User ID.</summary>
    public string Uid { get; set; } = string.Empty;
    /// <summary>Avatar image URL.</summary>
    public string Avatar { get; set; } = string.Empty;
    /// <summary>Cover photo URL.</summary>
    public string Cover { get; set; } = string.Empty;
    /// <summary>User status message.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Gender.</summary>
    public Gender Gender { get; set; }
    /// <summary>Date of birth (timestamp in milliseconds).</summary>
    public long Dob { get; set; }
    /// <summary>String-formatted date of birth.</summary>
    public string Sdob { get; set; } = string.Empty;
    /// <summary>Global ID.</summary>
    public string GlobalId { get; set; } = string.Empty;
    /// <summary>Zalo display name.</summary>
    public string ZaloName { get; set; } = string.Empty;
    /// <summary>Display name visible to others.</summary>
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>Business package info (null for personal accounts).</summary>
    public ZBusinessPackage? BizPkg { get; set; }
}

/// <summary>
/// User privacy and notification settings.
/// Equivalent to UserSetting type in zca-js.
/// </summary>
public class UserSetting
{
    /// <summary>Allow adding friends via contact? 0 = no, 1 = yes.</summary>
    public int AddFriendViaContact { get; set; }
    /// <summary>Show in friend recommendations? 0 = no, 1 = yes.</summary>
    public int DisplayOnRecommendFriend { get; set; }
    /// <summary>Allow adding friends via group? 0 = no, 1 = yes.</summary>
    public int AddFriendViaGroup { get; set; }
    /// <summary>Allow adding friends via QR code? 0 = no, 1 = yes.</summary>
    public int AddFriendViaQr { get; set; }
    /// <summary>Enable quick messages? 0 = disabled, 1 = enabled.</summary>
    public int QuickMessageStatus { get; set; }
    /// <summary>Show online status to others?</summary>
    public bool ShowOnlineStatus { get; set; }
    /// <summary>Accept calls from strangers? 0 = no, 1 = yes.</summary>
    public int AcceptStrangerCall { get; set; }
    /// <summary>Auto-archive chat status.</summary>
    public int ArchivedChatStatus { get; set; }
    /// <summary>Receive messages from non-friends? 0 = no, 1 = yes.</summary>
    public int ReceiveMessage { get; set; }
    /// <summary>Allow adding via phone number? 0 = no, 1 = yes.</summary>
    public int AddFriendViaPhone { get; set; }
    /// <summary>Show "seen" status to others? 0 = no, 1 = yes.</summary>
    public int DisplaySeenStatus { get; set; }
    /// <summary>Allow others to see birthday? 0 = no, 1 = yes.</summary>
    public int ViewBirthday { get; set; }
    /// <summary>2FA status: 0 = disabled, 1 = enabled.</summary>
    public int Setting2FAStatus { get; set; }
}