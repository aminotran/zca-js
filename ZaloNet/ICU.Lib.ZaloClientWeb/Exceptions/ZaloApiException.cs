namespace ICU.Lib.ZaloClientWeb.Exceptions;

/// <summary>
/// Exception thrown when Zalo API returns an error or an unexpected response occurs.
/// </summary>
public class ZaloApiException : Exception
{
    public int? ErrorCode { get; }

    public ZaloApiException(string message) : base(message)
    {
    }

    public ZaloApiException(string message, int? errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }

    public ZaloApiException(string message, Exception innerException) : base(message, innerException)
    {
    }
}