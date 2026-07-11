using ICU.Lib.ZaloClientWeb.Exceptions;

namespace ICU.Lib.ZaloClientWeb.Exceptions;

/// <summary>
/// Exception thrown when QR code login is aborted by the user.
/// </summary>
public class ZaloApiLoginQrAbortedException : ZaloApiException
{
    public ZaloApiLoginQrAbortedException()
        : base("QR login was aborted by the user.")
    {
    }
}