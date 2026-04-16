using LocalScanAgent.Application.Abstractions;
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
    private readonly SemaphoreSlim _scanLock = new(1, 1);

    public ScanOrchestrator(
        IScanSource scanSource,
        IPdfService pdfService,
        IAgentLogger logger,
        bool allowOnlyOneScanAtATime)
    {
        _scanSource = scanSource;
        _pdfService = pdfService;
        _logger = logger;
        _allowOnlyOneScanAtATime = allowOnlyOneScanAtATime;
    }

    public Task<IReadOnlyList<DeviceDto>> GetDevicesAsync(CancellationToken cancellationToken)
        => _scanSource.GetDevicesAsync(cancellationToken);

    public async Task<ScanResult> ScanToPdfAsync(ScanPdfRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedRequest = Normalize(request);

        if (normalizedRequest.Mode != ScanMode.Fake)
        {
            throw new NotSupportedException("Only fake scan mode is implemented in this MVP.");
        }

        if (_allowOnlyOneScanAtATime && !await _scanLock.WaitAsync(0, cancellationToken))
        {
            throw new InvalidOperationException("A scan is already in progress.");
        }

        try
        {
            _logger.LogInformation(
                "Starting scan in {Mode} mode with {PageCount} simulated pages.",
                normalizedRequest.Mode,
                normalizedRequest.SimulatedPages ?? DefaultSimulatedPages);

            var scannedPages = await _scanSource.ScanAsync(normalizedRequest, cancellationToken);
            var pdfBytes = _pdfService.CreatePdf(scannedPages);

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
