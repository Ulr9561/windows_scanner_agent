namespace LocalScanAgent.Application.Exceptions;

public sealed class ScannerNotFoundException(string message, Exception? innerException = null)
    : ScannerException(message, "scanner_not_found", innerException);
