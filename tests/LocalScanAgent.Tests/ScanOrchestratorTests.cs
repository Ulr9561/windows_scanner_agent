using LocalScanAgent.Application.Exceptions;
using LocalScanAgent.Application.Models;
using LocalScanAgent.Application.Services;
using LocalScanAgent.Contracts;
using LocalScanAgent.Tests.Stubs;

namespace LocalScanAgent.Tests;

public sealed class ScanOrchestratorTests
{
    private static ScanOrchestrator BuildOrchestrator(
        StubScanSource? scanSource = null,
        StubPdfService? pdfService = null,
        bool allowOnlyOne = false,
        int scanQueueWaitSeconds = 0,
        int scanTimeoutSeconds = 120)
    {
        return new ScanOrchestrator(
            scanSource ?? new StubScanSource(),
            pdfService ?? new StubPdfService(),
            new StubAgentLogger(),
            allowOnlyOne,
            scanQueueWaitSeconds,
            scanTimeoutSeconds);
    }

    private static ScanPdfRequest ValidRequest(int pages = 1) =>
        new() { Output = OutputFormat.Pdf, Mode = ScanMode.Fake, SimulatedPages = pages, Dpi = 300 };

    [Fact]
    public async Task GetDevicesAsync_DelegatesToScanSource()
    {
        var devices = new[] { new DeviceDto("id1", "Scanner A", DriverKind.Fake) };
        var source = new StubScanSource(devices: devices);
        var orchestrator = BuildOrchestrator(scanSource: source);

        var result = await orchestrator.GetDevicesAsync(CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("id1", result[0].Id);
    }

    [Fact]
    public async Task ScanToPdfAsync_ReturnsResult_WhenScanSucceeds()
    {
        var source = new StubScanSource(
            scanFactory: _ => new[] { new ScannedPage(1, "T", "B") });
        var pdf = new StubPdfService();
        var orchestrator = BuildOrchestrator(scanSource: source, pdfService: pdf);

        var result = await orchestrator.ScanToPdfAsync(ValidRequest(), CancellationToken.None);

        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal(1, result.PageCount);
        Assert.Equal(1, pdf.CallCount);
    }

    [Fact]
    public async Task State_IsReady_WhenNoScanInProgress()
    {
        var orchestrator = BuildOrchestrator(allowOnlyOne: true);

        Assert.Equal(ScannerState.Ready, orchestrator.State);
    }

    [Fact]
    public async Task State_IsBusy_WhileScanInProgress()
    {
        var scanStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var barrier = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new StubScanSource(
            scanFactory: _ =>
            {
                scanStarted.SetResult();
                barrier.Task.GetAwaiter().GetResult();
                return new[] { new ScannedPage(1, "T", "B") };
            });
        var orchestrator = BuildOrchestrator(scanSource: source, allowOnlyOne: true);

        var first = Task.Run(() => orchestrator.ScanToPdfAsync(ValidRequest(), CancellationToken.None));
        await scanStarted.Task;

        Assert.Equal(ScannerState.Busy, orchestrator.State);

        barrier.SetResult(true);
        await first;
        Assert.Equal(ScannerState.Ready, orchestrator.State);
    }

    [Fact]
    public async Task ScanToPdfAsync_NormalizesDpiToDefault_WhenDpiIsZero()
    {
        ScanPdfRequest? captured = null;
        var source = new StubScanSource(
            scanFactory: req =>
            {
                captured = req;
                return new[] { new ScannedPage(1, "T", "B") };
            });
        var orchestrator = BuildOrchestrator(scanSource: source);

        var request = new ScanPdfRequest { Output = OutputFormat.Pdf, Mode = ScanMode.Fake, SimulatedPages = 1, Dpi = 0 };
        await orchestrator.ScanToPdfAsync(request, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(300, captured!.Dpi);
    }

    [Fact]
    public async Task ScanToPdfAsync_ThrowsArgumentOutOfRange_WhenOutputIsNotPdf()
    {
        var orchestrator = BuildOrchestrator();
        var request = new ScanPdfRequest { Output = (OutputFormat)99, Mode = ScanMode.Fake, SimulatedPages = 1 };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => orchestrator.ScanToPdfAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task ScanToPdfAsync_ThrowsInvalidOperation_WhenScanAlreadyInProgress_AndQueueWaitIsZero()
    {
        var barrier = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new StubScanSource(
            scanFactory: _ =>
            {
                barrier.Task.GetAwaiter().GetResult();
                return new[] { new ScannedPage(1, "T", "B") };
            });
        // scanQueueWaitSeconds = 0 → rejet immédiat si occupé
        var orchestrator = BuildOrchestrator(scanSource: source, allowOnlyOne: true, scanQueueWaitSeconds: 0);

        var first = Task.Run(() => orchestrator.ScanToPdfAsync(ValidRequest(), CancellationToken.None));
        await Task.Delay(50);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.ScanToPdfAsync(ValidRequest(), CancellationToken.None));

        barrier.SetResult(true);
        await first;
    }

    [Fact]
    public async Task ScanToPdfAsync_ThrowsScannerScanFailed_WhenScanTimeoutExpires()
    {
        // Factory async : Task.Delay respecte le CancellationToken → annulation observée quand le timeout fire
        var source = new StubScanSource(
            asyncScanFactory: async (_, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                return new[] { new ScannedPage(1, "T", "B") };
            });
        var orchestrator = BuildOrchestrator(scanSource: source, scanTimeoutSeconds: 1);

        await Assert.ThrowsAsync<ScannerScanFailedException>(
            () => orchestrator.ScanToPdfAsync(ValidRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task ScanToPdfAsync_ThrowsArgumentOutOfRange_WhenSimulatedPagesExceedsMax()
    {
        var orchestrator = BuildOrchestrator();
        var request = new ScanPdfRequest { Output = OutputFormat.Pdf, Mode = ScanMode.Fake, SimulatedPages = 999 };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => orchestrator.ScanToPdfAsync(request, CancellationToken.None));
    }
}
