using LocalScanAgent.Application.Abstractions;
using LocalScanAgent.Application.Models;

namespace LocalScanAgent.Tests.Stubs;

internal sealed class StubPdfService : IPdfService
{
    private readonly byte[] _bytes;

    public StubPdfService(byte[]? bytes = null)
    {
        _bytes = bytes ?? [0x25, 0x50, 0x44, 0x46];
    }

    public int CallCount { get; private set; }

    public Task<byte[]> CreatePdfAsync(IReadOnlyList<ScannedPage> pages)
    {
        CallCount++;
        return Task.FromResult(_bytes);
    }
}
