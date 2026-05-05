namespace LocalScanAgent.Application.Exceptions;

public sealed class ScannerFeederEmptyException(string message, Exception? innerException = null)
    : ScannerException(message, "scanner_feeder_empty", innerException);
