using System;
using System.Security.Cryptography;
using System.Text;

namespace ICU.Lib.ZaloClientWeb.Crypto;

/// <summary>
/// Provides AES-CBC and AES-GCM encryption/decryption helpers
/// matching the implementation in zca-js (crypto-js + Web Crypto API).
/// </summary>
public static class AesHelper
{
    private static readonly byte[] ZeroIv = new byte[16];

    /// <summary>
    /// Encrypts data using AES-CBC with PKCS7 padding and zero IV.
    /// Key is a Base64-encoded string.
    /// Equivalent to encodeAES() in zca-js (when using Base64 key).
    /// </summary>
    public static string? EncryptAesCbc(string secretKeyBase64, string plainText, int retry = 0)
    {
        try
        {
            using var aes = Aes.Create();
            aes.Key = Convert.FromBase64String(secretKeyBase64);
            aes.IV = ZeroIv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var inputBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);
            return Convert.ToBase64String(cipherBytes);
        }
        catch
        {
            if (retry < 3)
                return EncryptAesCbc(secretKeyBase64, plainText, retry + 1);
            return null;
        }
    }

    /// <summary>
    /// Decrypts data using AES-CBC with PKCS7 padding and zero IV.
    /// Key is a Base64-encoded string.
    /// Equivalent to decodeAES() in zca-js.
    /// </summary>
    public static string? DecryptAesCbc(string secretKeyBase64, string cipherText, int retry = 0)
    {
        try
        {
            var decodedData = Uri.UnescapeDataString(cipherText);
            using var aes = Aes.Create();
            aes.Key = Convert.FromBase64String(secretKeyBase64);
            aes.IV = ZeroIv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var cipherBytes = Convert.FromBase64String(decodedData);
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            if (retry < 3)
                return DecryptAesCbc(secretKeyBase64, cipherText, retry + 1);
            return null;
        }
    }

    /// <summary>
    /// Encrypts data using AES-CBC with UTF-8 string as key (NOT hex-decoded).
    /// In zca-js: cryptojs.enc.Utf8.parse(e) — takes ASCII bytes of key string directly.
    /// crypto-js internally pads the key to valid AES key size (16/24/32 bytes).
    /// Uses zero IV, PKCS7 padding, hex output.
    /// Equivalent to ParamsEncryptor.encodeAES() in zca-js for the fixed key.
    /// </summary>
    public static string? EncryptAesCbcWithUtf8Key(string utf8Key, string plainText, string outputType = "hex", bool uppercase = false, int retry = 0)
    {
        try
        {
            var keyBytes = Encoding.UTF8.GetBytes(utf8Key);
            // crypto-js auto-pads/truncates to valid AES key size.
            // For the fixed key "3FC4F0D2AB50057BCE0D90D9187A22B1" (32 chars → 32 bytes):
            // it's exactly 32 bytes, so AES-256.
            if (keyBytes.Length != 16 && keyBytes.Length != 24 && keyBytes.Length != 32)
            {
                // Round up to next valid key size
                int targetSize = keyBytes.Length <= 16 ? 16 : keyBytes.Length <= 24 ? 24 : 32;
                Array.Resize(ref keyBytes, targetSize);
            }

            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = ZeroIv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var inputBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);

            if (outputType == "hex")
            {
                var result = BytesToHexString(cipherBytes);
                return uppercase ? result.ToUpperInvariant() : result;
            }
            return Convert.ToBase64String(cipherBytes);
        }
        catch
        {
            if (retry < 3)
                return EncryptAesCbcWithUtf8Key(utf8Key, plainText, outputType, uppercase, retry + 1);
            return null;
        }
    }

    /// <summary>
    /// Decrypts data using AES-GCM. Equivalent to the Web Crypto API AES-GCM decryption in decodeEventData().
    /// </summary>
    public static byte[]? DecryptAesGcm(byte[] key, byte[] iv, byte[] additionalData, byte[] cipherText, byte[] tag)
    {
        try
        {
            var plainText = new byte[cipherText.Length];
            using var aesGcm = new AesGcm(key);
            aesGcm.Decrypt(iv, cipherText, tag, plainText, additionalData);
            return plainText;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Decrypts event data using AES-GCM with the full structure: iv(16) + additionalData(16) + cipherText + tag(16).
    /// </summary>
    public static byte[]? DecryptEventDataGcm(byte[] key, byte[] fullData)
    {
        try
        {
            if (fullData.Length < 48) return null;

            var iv = new byte[16];
            var additionalData = new byte[16];
            var tag = new byte[16];
            var cipherText = new byte[fullData.Length - 48];

            Buffer.BlockCopy(fullData, 0, iv, 0, 16);
            Buffer.BlockCopy(fullData, 16, additionalData, 0, 16);
            Buffer.BlockCopy(fullData, 32, cipherText, 0, cipherText.Length);
            Buffer.BlockCopy(fullData, 32 + cipherText.Length, tag, 0, 16);

            return DecryptAesGcm(key, iv, additionalData, cipherText, tag);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Decrypts the response from Zalo API login using AES-CBC with UTF-8 key.
    /// Key is treated as UTF-8 bytes (NOT Base64) — matching zca-js decodeRespAES().
    /// The key is the "enk" from ParamsEncryptor (32-char hex string used as ASCII bytes).
    /// </summary>
    public static string? DecryptResponseAes(string utf8Key, string data)
    {
        try
        {
            data = Uri.UnescapeDataString(data);
            var keyBytes = Encoding.UTF8.GetBytes(utf8Key);
            if (keyBytes.Length != 16 && keyBytes.Length != 24 && keyBytes.Length != 32)
            {
                int targetSize = keyBytes.Length <= 16 ? 16 : keyBytes.Length <= 24 ? 24 : 32;
                Array.Resize(ref keyBytes, targetSize);
            }

            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = ZeroIv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var cipherBytes = Convert.FromBase64String(data);
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Decrypts the login response. Equivalent to decryptResp() in zca-js.
    /// </summary>
    public static string? DecryptResponse(string key, string data)
    {
        try
        {
            return DecryptResponseAes(key, data);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] HexStringToBytes(string hex)
    {
        var length = hex.Length;
        var bytes = new byte[length / 2];
        for (int i = 0; i < length; i += 2)
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        return bytes;
    }

    private static string BytesToHexString(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}