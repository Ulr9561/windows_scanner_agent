using LocalScanAgent.Application.Models;
using LocalScanAgent.Contracts;

namespace LocalScanAgent.Application.Abstractions;

public interface IScanSource
{
    Task<IReadOnlyList<DeviceDto>> GetDevicesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ScannedPage>> ScanAsync(ScanPdfRequest request, CancellationToken cancellationToken);
}
