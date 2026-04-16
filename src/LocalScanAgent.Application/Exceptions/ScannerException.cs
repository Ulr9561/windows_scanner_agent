namespace LocalScanAgent.Application.Exceptions;

public abstract class ScannerException : Exception
{
    protected ScannerException(string message, string errorCode, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
