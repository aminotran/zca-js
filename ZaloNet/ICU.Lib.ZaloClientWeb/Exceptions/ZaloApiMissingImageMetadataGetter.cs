using ICU.Lib.ZaloClientWeb.Exceptions;

namespace ICU.Lib.ZaloClientWeb.Exceptions;

/// <summary>
/// Exception thrown when an image operation is attempted without an ImageMetadataGetter configured.
/// </summary>
public class ZaloApiMissingImageMetadataGetterException : ZaloApiException
{
    public ZaloApiMissingImageMetadataGetterException()
        : base("ImageMetadataGetter is not configured. Set it in ZaloOptions.")
    {
    }
}