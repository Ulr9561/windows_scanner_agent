namespace LocalScanAgent.Application.Exceptions;

public sealed class ScannerUnavailableException(string message, Exception? innerException = null)
    : ScannerException(message, "scanner_unavailable", innerException);
