using NAPS2.Images;

namespace LocalScanAgent.Application.Models;

public sealed record ScannedPage(int PageNumber, string Title, string Body, ProcessedImage? Image = null);
