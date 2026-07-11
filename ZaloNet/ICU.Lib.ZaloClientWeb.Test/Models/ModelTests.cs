using ICU.Lib.ZaloClientWeb.Models;
using ICU.Lib.ZaloClientWeb.Models.Types;

namespace ICU.Lib.ZaloClientWeb.Test.Models;

public class ModelTests
{
    // ============ Message ============
    [Fact]
    public void UserMessageInfo_Sets_ThreadId_And_IsSelf()
    {
        var data = new MessageData { UidFrom = "0", IdTo = "456", MsgId = "msg1", DName = "Test" };
        var msg = new UserMessageInfo("123", data);
        Assert.Equal("456", msg.ThreadId); // threadId = idTo when uidFrom == "0"
        Assert.True(msg.IsSelf); // uidFrom == "0" means self
        Assert.Equal(ThreadType.User, msg.Type);
    }

    [Fact]
    public void UserMessageInfo_IsSelf_True_When_UidFrom_Zero()
    {
        var data = new MessageData { UidFrom = "0", IdTo = "456", MsgId = "msg2" };
        var msg = new UserMessageInfo("current-user", data);
        Assert.Equal("456", msg.ThreadId);
        Assert.True(msg.IsSelf);
    }

    [Fact]
    public void GroupMessageInfo_Uses_IdTo_As_ThreadId()
    {
        var data = new MessageData { UidFrom = "123", IdTo = "group456", MsgId = "msg3" };
        var msg = new GroupMessageInfo("123", data);
        Assert.Equal("group456", msg.ThreadId);
        Assert.Equal(ThreadType.Group, msg.Type);
    }

    [Fact]
    public void Quote_OwnerId_Is_String_After_Construction()
    {
        var data = new MessageData
        {
            UidFrom = "123", IdTo = "456", MsgId = "msg4",
            Quote = new Quote { OwnerId = "789" }
        };
        var msg = new UserMessageInfo("current-user", data);
        Assert.NotNull(msg.Data.Quote);
        Assert.Equal("789", msg.Data.Quote.OwnerId);
    }

    [Fact]
    public void Mention_Properties_Work()
    {
        var mention = new Mention { Uid = "123", Pos = 0, Len = 5, Type = 0 };
        Assert.Equal("123", mention.Uid);
        Assert.Equal(0, mention.Pos);
        Assert.Equal(5, mention.Len);
        Assert.Equal(0, mention.Type);
    }

    // ============ Reaction ============
    [Fact]
    public void Reaction_Constants_Are_Defined()
    {
        Assert.Equal("/-heart", Reactions.Heart);
        Assert.Equal("/-strong", Reactions.Like);
        Assert.Equal(":>", Reactions.Haha);
        Assert.Equal(55, typeof(Reactions).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Length);
    }

    [Fact]
    public void Reaction_Constructor_Sets_IsSelf()
    {
        var data = new ReactionData { UidFrom = "0", IdTo = "456" };
        var reaction = new Reaction("current-user", data, false);
        Assert.True(reaction.IsSelf);
        Assert.False(reaction.IsGroup);
    }

    [Fact]
    public void Reaction_Constructor_Sets_Group()
    {
        var data = new ReactionData { UidFrom = "123", IdTo = "group789" };
        var reaction = new Reaction("current-user", data, true);
        Assert.True(reaction.IsGroup);
        Assert.Equal("group789", reaction.ThreadId);
    }

    // ============ Typing ============
    [Fact]
    public void UserTypingEvent_Uses_Uid_As_ThreadId()
    {
        var data = new TypingData { Uid = "user123", Ts = "1000" };
        var typing = new UserTypingEvent(data);
        Assert.Equal("user123", typing.ThreadId);
        Assert.Equal(ThreadType.User, typing.Type);
        Assert.False(typing.IsSelf);
    }

    [Fact]
    public void GroupTypingEvent_Uses_Gid_As_ThreadId()
    {
        var data = new GroupTypingData { Gid = "group456" };
        var typing = new GroupTypingEvent(data);
        Assert.Equal("group456", typing.ThreadId);
        Assert.Equal(ThreadType.Group, typing.Type);
    }

    // ============ Undo ============
    [Fact]
    public void UndoEvent_Parses_Json()
    {
        var json = JsonSerializer.Deserialize<JsonElement>(
            "{\"actionId\":\"act1\",\"msgId\":\"msg1\",\"uidFrom\":\"0\",\"idTo\":\"456\",\"ts\":\"1000\"}");
        var undo = new UndoEvent("current-user", json, false);
        Assert.Equal("act1", undo.ActionId);
        Assert.Equal("msg1", undo.MsgId);
        Assert.True(undo.IsSelf);
    }

    // ============ GroupEvent ============
    [Fact]
    public void GroupEvent_Initialize_Base_Event()
    {
        var json = JsonSerializer.Deserialize<JsonElement>(
            "{\"groupId\":\"g123\",\"groupName\":\"Test Group\",\"sourceId\":\"user1\",\"time\":\"1000\"}");
        var evt = GroupEvent.Initialize("user1", json, GroupEventType.Join, "join");
        Assert.Equal(GroupEventType.Join, evt.Type);
        Assert.IsType<GroupEventBaseData>(evt.Data);
        var data = (GroupEventBaseData)evt.Data;
        Assert.Equal("g123", data.GroupId);
        Assert.Equal("Test Group", data.GroupName);
    }

    [Fact]
    public void GroupEvent_Initialize_JoinRequest()
    {
        var json = JsonSerializer.Deserialize<JsonElement>(
            "{\"groupId\":\"g123\",\"uids\":[\"u1\",\"u2\"],\"totalPending\":2,\"time\":\"1000\"}");
        var evt = GroupEvent.Initialize("u1", json, GroupEventType.JoinRequest, "join_request");
        Assert.IsType<GroupEventJoinRequestData>(evt.Data);
        var data = (GroupEventJoinRequestData)evt.Data;
        Assert.Equal(2, data.Uids?.Length);
        Assert.Equal(2, data.TotalPending);
    }

    // ============ FriendEvent ============
    [Fact]
    public void FriendEvent_Initialize_Request()
    {
        var json = JsonSerializer.Deserialize<JsonElement>(
            "{\"toUid\":\"u2\",\"fromUid\":\"u1\",\"src\":1,\"message\":\"Hello\"}");
        var evt = FriendEvent.Initialize("u1", json, FriendEventType.Request);
        Assert.IsType<FriendEventRequestData>(evt.Data);
        var data = (FriendEventRequestData)evt.Data;
        Assert.Equal("u2", data.ToUid);
        Assert.Equal("Hello", data.Message);
        Assert.True(evt.IsSelf); // fromUid == "u1"
    }

    [Fact]
    public void FriendEvent_Initialize_Add()
    {
        var json = JsonSerializer.Deserialize<JsonElement>("\"user123\"");
        var evt = FriendEvent.Initialize("current-user", json, FriendEventType.Add);
        Assert.IsType<FriendEventUserData>(evt.Data);
        var data = (FriendEventUserData)evt.Data;
        Assert.Equal("user123", data.Uid);
        Assert.False(evt.IsSelf); // Add type is not self
    }

    [Fact]
    public void FriendEvent_Initialize_Reject()
    {
        var json = JsonSerializer.Deserialize<JsonElement>("{\"toUid\":\"u2\",\"fromUid\":\"u1\"}");
        var evt = FriendEvent.Initialize("u1", json, FriendEventType.RejectRequest);
        Assert.IsType<FriendEventRejectUndoData>(evt.Data);
        var data = (FriendEventRejectUndoData)evt.Data;
        Assert.Equal("u2", data.ToUid);
    }

    // ============ Credentials ============
    [Fact]
    public void CookieItem_Properties_Work()
    {
        var cookie = new CookieItem
        {
            Name = "zpsid",
            Value = "abc123",
            Domain = ".chat.zalo.me",
            Path = "/",
            Secure = true,
            HttpOnly = true
        };
        Assert.Equal("zpsid", cookie.Name);
        Assert.Equal("abc123", cookie.Value);
        Assert.True(cookie.Secure);
    }

    // ============ LoginInfo ============
    [Fact]
    public void LoginInfo_Defaults()
    {
        var info = new LoginInfo();
        Assert.Equal(0L, info.Uid);
        Assert.Null(info.ZpwEnk);
        Assert.NotNull(info.ZpwServiceMapV3);
    }

    // ============ ZBusiness ============
    [Fact]
    public void BusinessCategoryNames_Contains_All_Categories()
    {
        Assert.Equal(15, BusinessCategoryNames.Names.Count);
        Assert.Equal("Bất động sản", BusinessCategoryNames.Names[BusinessCategory.RealEstate]);
    }

    // ============ GroupInfo ============
    [Fact]
    public void GroupSetting_Defaults()
    {
        var setting = new GroupSetting();
        Assert.Equal(0, setting.BlockName);
        Assert.Equal(0, setting.JoinAppr);
    }

    [Fact]
    public void GroupTopicType_Values()
    {
        Assert.Equal(0, (int)GroupTopicType.Note);
        Assert.Equal(2, (int)GroupTopicType.Message);
        Assert.Equal(3, (int)GroupTopicType.Poll);
    }

    // ============ Exceptions ============
    [Fact]
    public void ZaloApiException_Has_ErrorCode()
    {
        var ex = new ICU.Lib.ZaloClientWeb.Exceptions.ZaloApiException("test");
        Assert.Equal("test", ex.Message);
        Assert.Null(ex.ErrorCode);
    }

    [Fact]
    public void ZaloApiException_With_ErrorCode()
    {
        var ex = new ICU.Lib.ZaloClientWeb.Exceptions.ZaloApiException("error", -201);
        Assert.Equal(-201, ex.ErrorCode);
    }

    // ============ ZaloOptions ============
    [Fact]
    public void ZaloOptions_Defaults()
    {
        var opts = new ZaloOptions();
        Assert.True(opts.Logging);
        Assert.False(opts.SelfListen);
        Assert.True(opts.CheckUpdate);
        Assert.Equal(30, opts.ApiType);
        Assert.Equal(671, opts.ApiVersion);
    }

    // ============ Types ============
    [Fact]
    public void GroupEventType_Values_Match_Act_Strings()
    {
        Assert.Equal(1, (int)GroupEventType.JoinRequest);
        Assert.Equal(22, (int)GroupEventType.RemindTopic);
        Assert.Equal(0, (int)GroupEventType.Unknown);
    }

    [Fact]
    public void FriendEventType_Values_Match()
    {
        Assert.Equal(1, (int)FriendEventType.Add);
        Assert.Equal(12, (int)FriendEventType.PinCreate);
    }

    [Fact]
    public void DestType_Values()
    {
        Assert.Equal(1, (int)ICU.Lib.ZaloClientWeb.Models.Types.DestType.Group);
        Assert.Equal(3, (int)ICU.Lib.ZaloClientWeb.Models.Types.DestType.User);
        Assert.Equal(5, (int)ICU.Lib.ZaloClientWeb.Models.Types.DestType.Page);
    }

    [Fact]
    public void CloseReason_Values()
    {
        Assert.Equal(1000, (int)CloseReason.ManualClosure);
        Assert.Equal(3000, (int)CloseReason.DuplicateConnection);
    }
}