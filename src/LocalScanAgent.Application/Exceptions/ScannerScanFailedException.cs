namespace LocalScanAgent.Application.Exceptions;

public sealed class ScannerScanFailedException(string message, Exception? innerException = null)
    : ScannerException(message, "scanner_scan_failed", innerException);
