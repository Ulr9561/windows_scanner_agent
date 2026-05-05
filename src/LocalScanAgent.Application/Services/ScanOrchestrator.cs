using LocalScanAgent.Application.Abstractions;
using LocalScanAgent.Application.Exceptions;
using LocalScanAgent.Application.Models;
using LocalScanAgent.Contracts;

namespace LocalScanAgent.Application.Services;

public sealed class ScanOrchestrator
{
    private const int DefaultDpi = 300;
    private const int DefaultSimulatedPages = 3;
    private const int MaxSimulatedPages = 25;

    private readonly IScanSource _scanSource;
    private readonly IPdfService _pdfService;
    private readonly IAgentLogger _logger;
    private readonly bool _allowOnlyOneScanAtATime;
    private readonly TimeSpan _scanQueueWait;
    private readonly TimeSpan _scanTimeout;
    private readonly SemaphoreSlim _scanLock = new(1, 1);

    public ScanOrchestrator(
        IScanSource scanSource,
        IPdfService pdfService,
        IAgentLogger logger,
        bool allowOnlyOneScanAtATime,
        int scanQueueWaitSeconds = 30,
        int scanTimeoutSeconds = 120)
    {
        _scanSource = scanSource;
        _pdfService = pdfService;
        _logger = logger;
        _allowOnlyOneScanAtATime = allowOnlyOneScanAtATime;
        _scanQueueWait = TimeSpan.FromSeconds(scanQueueWaitSeconds);
        _scanTimeout = TimeSpan.FromSeconds(scanTimeoutSeconds);
    }

    public ScannerState State => _allowOnlyOneScanAtATime && _scanLock.CurrentCount == 0
        ? ScannerState.Busy
        : ScannerState.Ready;

    public Task<IReadOnlyList<DeviceDto>> GetDevicesAsync(CancellationToken cancellationToken)
        => _scanSource.GetDevicesAsync(cancellationToken);

    public async Task<ScanResult> ScanToPdfAsync(ScanPdfRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedRequest = Normalize(request);

        if (_allowOnlyOneScanAtATime && !await _scanLock.WaitAsync(_scanQueueWait, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Un scan est deja en cours. Reessayez dans quelques secondes.");
        }

        try
        {
            _logger.LogInformation("Starting scan in {Mode} mode.", normalizedRequest.Mode);

            using var timeoutCts = new CancellationTokenSource(_scanTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            IReadOnlyList<ScannedPage> scannedPages;
            byte[] pdfBytes;

            try
            {
                scannedPages = await _scanSource.ScanAsync(normalizedRequest, linkedCts.Token);
                pdfBytes = await _pdfService.CreatePdfAsync(scannedPages);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new ScannerScanFailedException(
                    $"Le scan a depasse le delai maximum de {(int)_scanTimeout.TotalSeconds} secondes. Verifiez que le scanner repond.");
            }

            _logger.LogInformation("Generated a PDF with {PageCount} pages.", scannedPages.Count);

            return new ScanResult(pdfBytes, "application/pdf", "scan_note.pdf", scannedPages.Count);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "The scan workflow failed.");
            throw;
        }
        finally
        {
            if (_allowOnlyOneScanAtATime)
            {
                _scanLock.Release();
            }
        }
    }

    private static ScanPdfRequest Normalize(ScanPdfRequest request)
    {
        if (request.Output != OutputFormat.Pdf)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Output), "Only PDF output is supported.");
        }

        if (request.Dpi <= 0)
        {
            request = request with { Dpi = DefaultDpi };
        }

        var simulatedPages = request.SimulatedPages ?? DefaultSimulatedPages;
        if (simulatedPages <= 0 || simulatedPages > MaxSimulatedPages)
        {
            throw new ArgumentOutOfRangeException(nameof(request.SimulatedPages), $"SimulatedPages must be between 1 and {MaxSimulatedPages}.");
        }

        return request with { SimulatedPages = simulatedPages };
    }
}
