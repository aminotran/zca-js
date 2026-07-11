using ICU.Lib.ZaloClientWeb.Crypto;

namespace ICU.Lib.ZaloClientWeb.Test.Crypto;

public class AesHelperTests
{
    [Fact]
    public void EncryptAesCbc_Should_Return_Base64_String()
    {
        var key = Convert.ToBase64String(new byte[32]); // 256-bit key
        var plain = "Hello Zalo!";

        var result = AesHelper.EncryptAesCbc(key, plain);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.IsType<string>(result);
    }

    [Fact]
    public void EncryptAesCbc_Then_DecryptAesCbc_Should_Return_Original()
    {
        var key = Convert.ToBase64String(new byte[32]);
        var plain = "Test message 123!@#";

        var encrypted = AesHelper.EncryptAesCbc(key, plain);
        Assert.NotNull(encrypted);

        var decrypted = AesHelper.DecryptAesCbc(key, encrypted);
        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void EncryptAesCbc_With_Empty_String_Should_Return_Value()
    {
        var key = Convert.ToBase64String(new byte[32]);

        var result = AesHelper.EncryptAesCbc(key, "");

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void DecryptAesCbc_With_Wrong_Key_Should_Return_Null()
    {
        var key1 = Convert.ToBase64String(new byte[32] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 });
        var key2 = Convert.ToBase64String(new byte[32] { 32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 });

        var encrypted = AesHelper.EncryptAesCbc(key1, "secret");
        Assert.NotNull(encrypted);

        var decrypted = AesHelper.DecryptAesCbc(key2, encrypted);
        Assert.Null(decrypted); // wrong key should fail to decrypt
    }

    [Fact]
    public void EncryptAesCbcWithHexKey_Should_Return_Hex_String()
    {
        var hexKey = "3FC4F0D2AB50057BCE0D90D9187A22B1";
        var plain = "30,test-imei,1234567890";

        var result = AesHelper.EncryptAesCbcWithHexKey(hexKey, plain);

        Assert.NotNull(result);
        Assert.Matches("^[0-9a-f]+$", result); // hex (lowercase by default)
    }

    [Fact]
    public void EncryptAesCbcWithHexKey_Lowercase_Flag_Works()
    {
        var hexKey = "3FC4F0D2AB50057BCE0D90D9187A22B1";

        var upper = AesHelper.EncryptAesCbcWithHexKey(hexKey, "test", true);
        var lower = AesHelper.EncryptAesCbcWithHexKey(hexKey, "test", false);

        Assert.NotNull(upper);
        Assert.NotNull(lower);
        Assert.Equal(upper.ToLowerInvariant(), lower);
    }

    [Fact]
    public void DecryptResponseAes_Should_Validate_Key()
    {
        // DecryptResponseAes uses UTF8 key, not hex key
        // Just verify it doesn't crash and returns something consistent
        var hexKey = "3FC4F0D2AB50057BCE0D90D9187A22B1";
        var data = Uri.EscapeDataString("somebase64data");
        var result = AesHelper.DecryptResponseAes(hexKey, data);
        // Expected to fail gracefully since data isn't valid ciphertext
        Assert.Null(result);
    }

    [Fact]
    public void EncryptAesCbc_Retry_Logic_Works()
    {
        // Pass invalid base64 key to trigger retries
        var result = AesHelper.EncryptAesCbc("invalid-key!!!", "test");
        Assert.Null(result);
    }

    [Fact]
    public void DecryptEventDataGcm_With_Short_Data_Returns_Null()
    {
        var key = new byte[32];
        var shortData = new byte[10]; // less than 48 bytes

        var result = AesHelper.DecryptEventDataGcm(key, shortData);
        Assert.Null(result);
    }
}