namespace LocalScanAgent.Application.Models;

public sealed record ScanResult(byte[] Content, string ContentType, string FileName, int PageCount);
