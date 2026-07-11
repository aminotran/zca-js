using ICU.Lib.ZaloClientWeb.Exceptions;

namespace ICU.Lib.ZaloClientWeb.Exceptions;

/// <summary>
/// Exception thrown when QR code login is declined by the user on their phone.
/// </summary>
public class ZaloApiLoginQrDeclinedException : ZaloApiException
{
    public ZaloApiLoginQrDeclinedException()
        : base("QR login was declined by the user.")
    {
    }
}