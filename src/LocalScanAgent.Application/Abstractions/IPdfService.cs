using LocalScanAgent.Application.Models;

namespace LocalScanAgent.Application.Abstractions;

public interface IPdfService
{
    Task<byte[]> CreatePdfAsync(IReadOnlyList<ScannedPage> pages);
}
