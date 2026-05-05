using LocalScanAgent.Application.Abstractions;
using LocalScanAgent.Application.Models;
using LocalScanAgent.Contracts;

namespace LocalScanAgent.Tests.Stubs;

internal sealed class StubScanSource : IScanSource
{
    private readonly IReadOnlyList<DeviceDto> _devices;
    private readonly Func<ScanPdfRequest, IReadOnlyList<ScannedPage>>? _syncFactory;
    private readonly Func<ScanPdfRequest, CancellationToken, Task<IReadOnlyList<ScannedPage>>>? _asyncFactory;

    public StubScanSource(
        IReadOnlyList<DeviceDto>? devices = null,
        Func<ScanPdfRequest, IReadOnlyList<ScannedPage>>? scanFactory = null,
        Func<ScanPdfRequest, CancellationToken, Task<IReadOnlyList<ScannedPage>>>? asyncScanFactory = null)
    {
        _devices = devices ?? [];
        _syncFactory = scanFactory;
        _asyncFactory = asyncScanFactory;
    }

    public Task<IReadOnlyList<DeviceDto>> GetDevicesAsync(CancellationToken cancellationToken)
        => Task.FromResult(_devices);

    public async Task<IReadOnlyList<ScannedPage>> ScanAsync(ScanPdfRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_asyncFactory is not null)
            return await _asyncFactory(request, cancellationToken);

        if (_syncFactory is not null)
            return _syncFactory(request);

        throw new InvalidOperationException("StubScanSource: no scan factory configured.");
    }
}
