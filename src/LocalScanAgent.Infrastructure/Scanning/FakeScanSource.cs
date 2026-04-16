using LocalScanAgent.Application.Abstractions;
using LocalScanAgent.Application.Models;
using LocalScanAgent.Contracts;

namespace LocalScanAgent.Infrastructure.Scanning;

public sealed class FakeScanSource : IScanSource
{
    private static readonly IReadOnlyList<DeviceDto> Devices =
    [
        new("fake-hp-7000s3", "Fake HP ScanJet Enterprise Flow 7000 s3", DriverKind.Fake),
        new("fake-hp-officejet-pro", "Fake HP OfficeJet Pro", DriverKind.Fake)
    ];

    public Task<IReadOnlyList<DeviceDto>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Devices);
    }

    public Task<IReadOnlyList<ScannedPage>> ScanAsync(ScanPdfRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pageCount = request.SimulatedPages ?? 3;
        var selectedDevice = string.IsNullOrWhiteSpace(request.PreferredDeviceId)
            ? Devices[0].Id
            : request.PreferredDeviceId;

        var pages = Enumerable.Range(1, pageCount)
            .Select(pageNumber => new ScannedPage(
                pageNumber,
                $"Fake scan page {pageNumber}/{pageCount}",
                $"Device: {selectedDevice}{Environment.NewLine}" +
                $"DPI: {request.Dpi}{Environment.NewLine}" +
                $"Paper source: {request.PaperSource}{Environment.NewLine}" +
                $"Duplex: {request.Duplex}{Environment.NewLine}" +
                $"Color mode: {request.ColorMode}{Environment.NewLine}" +
                $"Generated at: {DateTimeOffset.UtcNow:O}"))
            .ToArray();

        return Task.FromResult<IReadOnlyList<ScannedPage>>(pages);
    }
}
