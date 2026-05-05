using LocalScanAgent.Application.Exceptions;
using LocalScanAgent.Contracts;
using LocalScanAgent.Infrastructure.Scanning;

namespace LocalScanAgent.Tests;

public sealed class FakeScanSourceTests
{
    [Fact]
    public async Task GetDevicesAsync_ReturnsTwoFakeDevices()
    {
        var source = new FakeScanSource();

        var devices = await source.GetDevicesAsync(CancellationToken.None);

        Assert.Equal(2, devices.Count);
        Assert.All(devices, d => Assert.Equal(DriverKind.Fake, d.Driver));
    }

    [Fact]
    public async Task ScanAsync_ReturnsRequestedPageCount()
    {
        var source = new FakeScanSource();
        var request = new ScanPdfRequest { SimulatedPages = 5 };

        var pages = await source.ScanAsync(request, CancellationToken.None);

        Assert.Equal(5, pages.Count);
    }

    [Fact]
    public async Task ScanAsync_UsesFirstDevice_WhenNoPreferenceGiven()
    {
        var source = new FakeScanSource();
        var request = new ScanPdfRequest { SimulatedPages = 1 };

        var pages = await source.ScanAsync(request, CancellationToken.None);

        Assert.Contains("fake-hp-7000s3", pages[0].Body);
    }

    [Fact]
    public async Task ScanAsync_UsesRequestedDevice_WhenValidIdGiven()
    {
        var source = new FakeScanSource();
        var request = new ScanPdfRequest { SimulatedPages = 1, PreferredDeviceId = "fake-hp-officejet-pro" };

        var pages = await source.ScanAsync(request, CancellationToken.None);

        Assert.Contains("fake-hp-officejet-pro", pages[0].Body);
    }

    [Fact]
    public async Task ScanAsync_ThrowsScannerNotFound_WhenDeviceIdUnknown()
    {
        var source = new FakeScanSource();
        var request = new ScanPdfRequest { SimulatedPages = 1, PreferredDeviceId = "does-not-exist" };

        await Assert.ThrowsAsync<ScannerNotFoundException>(
            () => source.ScanAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task ScanAsync_ThrowsOperationCanceled_WhenTokenCancelled()
    {
        var source = new FakeScanSource();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => source.ScanAsync(new ScanPdfRequest(), cts.Token));
    }
}
