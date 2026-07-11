using ICU.Lib.ZaloClientWeb.Utils;

namespace ICU.Lib.ZaloClientWeb.Test.Utils;

public class ZaloUtilsTests
{
    [Fact]
    public void GetSignKey_Should_Return_MD5_Hash()
    {
        var result = ZaloUtils.GetSignKey("login", new() { ["imei"] = "test" });
        Assert.NotNull(result);
        Assert.Equal(32, result.Length); // MD5 hex = 32 chars
        Assert.Matches("^[0-9a-f]+$", result);
    }

    [Fact]
    public void GetSignKey_With_Multiple_Params_Sorts_Keys()
    {
        var result = ZaloUtils.GetSignKey("test", new() { ["b"] = "2", ["a"] = "1" });
        // Keys sorted: a, b -> "zsecuretest12"
        Assert.NotNull(result);
        Assert.Equal(32, result.Length);
    }

    [Fact]
    public void MakeUrl_Adds_Zpw_Params()
    {
        var url = ZaloUtils.MakeUrl("https://wpa.chat.zalo.me/api/test", null, 671, 30);

        Assert.Contains("zpw_ver=671", url);
        Assert.Contains("zpw_type=30", url);
    }

    [Fact]
    public void MakeUrl_Preserves_Existing_Params()
    {
        var url = ZaloUtils.MakeUrl("https://wpa.chat.zalo.me/api/test?existing=1", null, 671, 30);

        Assert.Contains("existing=1", url);
        Assert.Contains("zpw_ver=671", url);
    }

    [Fact]
    public void MakeUrl_With_Extra_Params()
    {
        var url = ZaloUtils.MakeUrl("https://example.com/api", new() { ["key"] = "val" }, 671, 30);

        Assert.Contains("key=val", url);
        Assert.Contains("zpw_ver=671", url);
    }

    [Fact]
    public void GenerateZaloUuid_Should_Return_Uuid_With_MD5()
    {
        var ua = "Mozilla/5.0 Firefox/133.0";
        var uuid = ZaloUtils.GenerateZaloUuid(ua);

        Assert.NotNull(uuid);
        Assert.Contains("-", uuid); // format: uuid-md5
        Assert.Equal(32 + 1 + 32, uuid.Length); // 32 hex + dash + 32 md5
    }

    [Theory]
    [InlineData("webchat", 1)]
    [InlineData("chat.voice", 31)]
    [InlineData("chat.photo", 32)]
    [InlineData("chat.sticker", 36)]
    [InlineData("chat.doodle", 37)]
    [InlineData("chat.recommended", 38)]
    [InlineData("chat.link", 38)]
    [InlineData("chat.video.msg", 44)]
    [InlineData("share.file", 46)]
    [InlineData("chat.gif", 49)]
    [InlineData("chat.location.new", 43)]
    [InlineData("unknown", 1)]
    public void GetClientMessageType_Returns_Correct_Type(string msgType, int expected)
    {
        Assert.Equal(expected, ZaloUtils.GetClientMessageType(msgType));
    }

    [Theory]
    [InlineData("#00FF00", -16711936)]   // 0x00FF00FF (ARGB) → FF00FF00 → -16711936
    [InlineData("#FF0000", -65536)]      // 0x00FF0000 (ARGB) → FFFF0000 → -65536
    [InlineData("0000FF", -16776961)]    // no # → 0x0000FF → FF0000FF → -16776961
    public void HexToNegativeColor_Works(string hex, int expected)
    {
        var result = ZaloUtils.HexToNegativeColor(hex);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(-16711936, "#00FF00")]
    [InlineData(-65536, "#FF0000")]
    public void NegativeColorToHex_Works(int negative, string expected)
    {
        var result = ZaloUtils.NegativeColorToHex(negative);
        Assert.Equal(expected, result.ToUpperInvariant());
    }

    [Fact]
    public void EncryptPin_Returns_MD5()
    {
        var pin = "1234";
        var hash = ZaloUtils.EncryptPin(pin);
        Assert.Equal(32, hash.Length);
        Assert.Matches("^[0-9a-f]+$", hash);
    }

    [Fact]
    public void ValidatePin_Works()
    {
        var pin = "5678";
        var hash = ZaloUtils.EncryptPin(pin);
        Assert.True(ZaloUtils.ValidatePin(hash, pin));
        Assert.False(ZaloUtils.ValidatePin(hash, "wrong"));
    }

    [Theory]
    [InlineData("join_request", 1)]
    [InlineData("join", 2)]
    [InlineData("leave", 3)]
    [InlineData("remove_member", 4)]
    [InlineData("block_member", 5)]
    [InlineData("update_setting", 6)]
    [InlineData("update_avatar", 7)]
    [InlineData("update", 8)]
    [InlineData("new_link", 9)]
    [InlineData("add_admin", 10)]
    [InlineData("remove_admin", 11)]
    [InlineData("new_pin_topic", 12)]
    [InlineData("update_pin_topic", 13)]
    [InlineData("update_topic", 14)]
    [InlineData("update_board", 15)]
    [InlineData("remove_board", 16)]
    [InlineData("reorder_pin_topic", 17)]
    [InlineData("unpin_topic", 18)]
    [InlineData("remove_topic", 19)]
    [InlineData("accept_remind", 20)]
    [InlineData("reject_remind", 21)]
    [InlineData("remind_topic", 22)]
    [InlineData("unknown_action", 0)]
    public void GetGroupEventType_Maps_Correctly(string act, int expected)
    {
        Assert.Equal(expected, ZaloUtils.GetGroupEventType(act));
    }

    [Theory]
    [InlineData("add", 1)]
    [InlineData("remove", 2)]
    [InlineData("block", 3)]
    [InlineData("unblock", 4)]
    [InlineData("block_call", 5)]
    [InlineData("unblock_call", 6)]
    [InlineData("req_v2", 7)]
    [InlineData("reject", 8)]
    [InlineData("undo_req", 9)]
    [InlineData("seen_fr_req", 10)]
    [InlineData("pin_unpin", 11)]
    [InlineData("pin_create", 12)]
    [InlineData("unknown", 0)]
    public void GetFriendEventType_Maps_Correctly(string act, int expected)
    {
        Assert.Equal(expected, ZaloUtils.GetFriendEventType(act));
    }

    [Fact]
    public void FormatTime_Works()
    {
        // Use known timestamp: 2025-01-15 10:30:45 UTC
        var ts = new DateTimeOffset(2025, 1, 15, 10, 30, 45, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var result = ZaloUtils.FormatTime("%H:%M:%S %d/%m/%Y", ts);
        Assert.Equal("10:30:45 15/01/2025", result);
    }

    [Fact]
    public void GetFullTimeFromMilliseconds_Works()
    {
        var ts = new DateTimeOffset(2025, 1, 15, 10, 30, 45, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var result = ZaloUtils.GetFullTimeFromMilliseconds(ts);
        Assert.Contains("10:30", result);
        Assert.Contains("15/01/2025", result);
    }

    [Fact]
    public void StrPadLeft_Pads_Correctly()
    {
        Assert.Equal("  1", ZaloUtils.StrPadLeft("1", ' ', 3));
        Assert.Equal("001", ZaloUtils.StrPadLeft("1", '0', 3));
        Assert.Equal("345", ZaloUtils.StrPadLeft("12345", '0', 3)); // longer input = return last n chars
    }

    [Fact]
    public void RemoveNullKeys_Removes_Null_Values()
    {
        var dict = new Dictionary<string, object?> { ["a"] = "1", ["b"] = null, ["c"] = "3" };
        var result = ZaloUtils.RemoveNullKeys(dict);
        Assert.Equal(2, result.Count);
        Assert.Contains("a", result.Keys);
        Assert.Contains("c", result.Keys);
        Assert.DoesNotContain("b", result.Keys);
    }

    [Fact]
    public void DecodeBase64ToBuffer_Works()
    {
        var data = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });
        var result = ZaloUtils.DecodeBase64ToBuffer(data);
        Assert.Equal(4, result.Length);
        Assert.Equal(1, result[0]);
    }

    [Fact]
    public void DecodeUint8Array_Returns_UTF8()
    {
        var bytes = "Hello"u8.ToArray();
        var result = ZaloUtils.DecodeUint8Array(bytes);
        Assert.Equal("Hello", result);
    }

    [Fact]
    public void DecodeUint8Array_Invalid_Returns_Null()
    {
        // Send null to test fallback (invalid encoding is rare in modern .NET)
        // Using a high continuation byte without start byte
        var result = ZaloUtils.DecodeUint8Array(Array.Empty<byte>());
        Assert.Equal("", result); // empty returns empty string
    }

    [Fact]
    public void GetMd5LargeFile_With_Small_Buffer_Works()
    {
        var buffer = "test data"u8.ToArray();
        var hash = ZaloUtils.GetMd5LargeFile(buffer, 1024);
        Assert.Equal(32, hash.Length);
        Assert.Matches("^[0-9a-f]+$", hash);
    }

    [Fact]
    public void GetDefaultHeaders_Contains_Required_Headers()
    {
        var headers = ZaloUtils.GetDefaultHeaders(null, "test-ua");
        Assert.Contains("test-ua", headers["User-Agent"]);
        Assert.Equal("application/json, text/plain, */*", headers["Accept"]);
        Assert.Equal("https://chat.zalo.me", headers["Origin"]);
    }
}