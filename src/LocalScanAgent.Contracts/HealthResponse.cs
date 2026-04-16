namespace LocalScanAgent.Contracts;

public sealed record HealthResponse(string Status, string Version, ScannerState ScannerState);
