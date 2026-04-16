using LocalScanAgent.Application.Models;

namespace LocalScanAgent.Application.Abstractions;

public interface IPdfService
{
    byte[] CreatePdf(IReadOnlyList<ScannedPage> pages);
}
