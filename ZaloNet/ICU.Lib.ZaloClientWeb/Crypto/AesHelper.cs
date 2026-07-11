using System;
using System.IO;
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
    /// Equivalent to encodeAES() in zca-js.
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
    /// Encrypts data using AES-CBC with a hex key, zero IV, PKCS7 padding.
    /// Equivalent to ParamsEncryptor.encodeAES() in zca-js.
    /// </summary>
    public static string? EncryptAesCbcWithHexKey(string hexKey, string plainText, bool uppercase = false, int retry = 0)
    {
        try
        {
            var keyBytes = HexStringToBytes(hexKey);
            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = ZeroIv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var inputBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);
            var result = BytesToHexString(cipherBytes);
            return uppercase ? result.ToUpperInvariant() : result;
        }
        catch
        {
            if (retry < 3)
                return EncryptAesCbcWithHexKey(hexKey, plainText, uppercase, retry + 1);
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
#if NETSTANDARD2_1
            // .NET Standard 2.1 uses AesGcm with different constructor
            using var aesGcm = new AesGcm(key);
            var plainText = new byte[cipherText.Length];
            aesGcm.Decrypt(iv, cipherText, tag, plainText, additionalData);
            return plainText;
#else
            var plainText = new byte[cipherText.Length];
            using var aesGcm = new AesGcm(key);
            aesGcm.Decrypt(iv, cipherText, tag, plainText, additionalData);
            return plainText;
#endif
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
    /// Decrypts the response from Zalo API using AES-CBC with hex key.
    /// Equivalent to decodeRespAES() in zca-js.
    /// </summary>
    public static string? DecryptResponseAes(string hexKey, string data)
    {
        try
        {
            data = Uri.UnescapeDataString(data);
            var keyBytes = Encoding.UTF8.GetBytes(hexKey);

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
    /// Decrypts the response from login API using the secret key.
    /// Equivalent to decryptResp() in zca-js.
    /// </summary>
    public static string? DecryptResponse(string key, string data)
    {
        try
        {
            var result = DecryptResponseAes(key, data);
            return result;
        }
        catch
        {
            // In zca-js, if JSON parse fails, raw string is returned
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