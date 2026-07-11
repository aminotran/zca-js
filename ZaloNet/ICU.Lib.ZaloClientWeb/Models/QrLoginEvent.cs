using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Stages of the QR code login process.
/// Equivalent to LoginQRCallbackEventType in zca-js.
/// </summary>
public enum QrLoginEventType
{
    /// <summary>QR code has been generated and saved to file.</summary>
    QrCodeGenerated,
    /// <summary>QR code has expired (100 second timeout).</summary>
    QrCodeExpired,
    /// <summary>QR code was scanned by the user's phone.</summary>
    QrCodeScanned,
    /// <summary>QR login was declined on the phone.</summary>
    QrCodeDeclined,
    /// <summary>Login successful — credentials obtained.</summary>
    GotLoginInfo
}

/// <summary>
/// Event data for each stage of QR login.
/// Equivalent to LoginQRCallbackEvent in zca-js.
/// </summary>
public class QrLoginEvent : EventArgs
{
    /// <summary>Stage of the QR login process.</summary>
    public QrLoginEventType Type { get; set; }

    /// <summary>QR code data (available when Type = QrCodeGenerated).</summary>
    public string? QrCode { get; set; }

    /// <summary>QR code token (available when Type = QrCodeGenerated).</summary>
    public string? Token { get; set; }

    /// <summary>File path where the QR image was saved.</summary>
    public string? FilePath { get; set; }

    /// <summary>Display name of the scanned user (available when Type = QrCodeScanned).</summary>
    public string? DisplayName { get; set; }

    /// <summary>Avatar URL of the scanned user.</summary>
    public string? Avatar { get; set; }
}