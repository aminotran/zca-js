using System;
using System.Security.Cryptography;
using System.Text;

namespace ICU.Lib.ZaloClientWeb.Crypto;

/// <summary>
/// Encrypts parameters for Zalo API requests.
/// Equivalent to ParamsEncryptor class in zca-js.
/// </summary>
public class ParamsEncryptor
{
    private string? _zcid;
    private readonly string _zcidExt;
    private string? _encryptKey;
    private const string FixedKey = "3FC4F0D2AB50057BCE0D90D9187A22B1";

    /// <summary>Encryption version string (always "v2").</summary>
    public string EncVer { get; } = "v2";

    public ParamsEncryptor(int type, string imei, long firstLaunchTime)
    {
        _zcid = CreateZcid(type, imei, firstLaunchTime);
        _zcidExt = RandomString();
        CreateEncryptKey(0);
    }

    /// <summary>
    /// Gets the derived encrypt key used for request data encryption.
    /// Equivalent to getEncryptKey() in zca-js.
    /// </summary>
    public string GetEncryptKey()
    {
        if (string.IsNullOrEmpty(_encryptKey))
            throw new InvalidOperationException("EncryptKey not created yet");
        return _encryptKey;
    }

    /// <summary>
    /// Gets the encryption parameters (zcid, zcid_ext, enc_ver) for API requests.
    /// Equivalent to getParams() in zca-js.
    /// </summary>
    public Dictionary<string, string> GetParams()
    {
        if (string.IsNullOrEmpty(_zcid))
            return new Dictionary<string, string>();

        return new Dictionary<string, string>
        {
            ["zcid"] = _zcid,
            ["zcid_ext"] = _zcidExt,
            ["enc_ver"] = EncVer
        };
    }

    /// <summary>
    /// Creates the zcid value by encrypting "{type},{imei},{firstLaunchTime}" with the fixed key.
    /// In zca-js: the key is used as UTF-8 bytes, NOT hex-decoded! (cryptojs.enc.Utf8.parse(e)).
    /// Output is uppercase hex.
    /// </summary>
    private string? CreateZcid(int type, string imei, long firstLaunchTime)
    {
        if (type == 0 || string.IsNullOrEmpty(imei) || firstLaunchTime == 0)
            throw new ArgumentException("createZcid: missing params");

        var msg = $"{type},{imei},{firstLaunchTime}";
        return AesHelper.EncryptAesCbcWithUtf8Key(FixedKey, msg, "hex", true);
    }

    /// <summary>
    /// Creates the encrypt key from zcid and zcid_ext using MD5 + string processing.
    /// Retries up to 3 times if the key generation fails.
    /// Equivalent to createEncryptKey() in zca-js.
    /// </summary>
    private void CreateEncryptKey(int attempt)
    {
        if (string.IsNullOrEmpty(_zcid) || string.IsNullOrEmpty(_zcidExt))
            throw new InvalidOperationException("zcid or zcid_ext is null");

        try
        {
            var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(_zcidExt));
            var n = BytesToHexUpper(hash);

            if (TryGenerateKey(n, _zcid))
                return;

            if (attempt < 3)
                CreateEncryptKey(attempt + 1);
        }
        catch
        {
            if (attempt < 3)
                CreateEncryptKey(attempt + 1);
        }
    }

    private bool TryGenerateKey(string n, string zcid)
    {
        var (evenN, _) = ProcessString(n);
        var (evenZ, oddZ) = ProcessString(zcid);

        if (evenN == null || evenZ == null || oddZ == null)
            return false;

        var evenNArr = evenN.ToCharArray();
        var evenZArr = evenZ.ToCharArray();
        var oddZArr = oddZ.ToCharArray();
        Array.Reverse(oddZArr);

        var evenNStr = new string(evenNArr, 0, Math.Min(8, evenNArr.Length));
        var evenZStr = new string(evenZArr, 0, Math.Min(12, evenZArr.Length));
        var oddZStr = new string(oddZArr, 0, Math.Min(12, oddZArr.Length));

        _encryptKey = evenNStr + evenZStr + oddZStr;
        return true;
    }

    private static (string? even, string? odd) ProcessString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return (null, null);

        var even = new StringBuilder();
        var odd = new StringBuilder();

        for (int i = 0; i < input.Length; i++)
        {
            if (i % 2 == 0)
                even.Append(input[i]);
            else
                odd.Append(input[i]);
        }

        return (even.ToString(), odd.ToString());
    }

    private static string RandomString(int min = 6, int max = 12)
    {
        var random = new Random();
        var length = random.Next(min, Math.Max(min + 1, max + 1));
        length = Math.Min(length, 12);
        const string chars = "0123456789abcdef";
        var result = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            result.Append(chars[random.Next(chars.Length)]);
        return result.ToString();
    }

    private static string BytesToHexUpper(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("X2"));
        return sb.ToString();
    }
}