using LocalScanAgent.Application.Abstractions;
using LocalScanAgent.Application.Exceptions;
using LocalScanAgent.Application.Models;
using LocalScanAgent.Contracts;
using NAPS2.Images;
using NAPS2.Images.ImageSharp;
using NAPS2.Scan;
using NAPS2.Scan.Exceptions;

namespace LocalScanAgent.Infrastructure.Scanning;

public sealed class Naps2ScanSource : IScanSource, IDisposable
{
    private readonly IAgentLogger _logger;
    private readonly Driver[] _preferredDrivers;
    private readonly ScanQualitySettings _qualitySettings;
    private readonly ScanningContext _scanningContext;
    private readonly ScanController _controller;

    public Naps2ScanSource(
        IAgentLogger logger,
        IEnumerable<string> preferredDriverOrder,
        ScanQualitySettings qualitySettings)
    {
        _logger = logger;
        _preferredDrivers = ParsePreferredDrivers(preferredDriverOrder).ToArray();
        _qualitySettings = qualitySettings;
        _scanningContext = new ScanningContext(new ImageSharpImageContext());
        _scanningContext.SetUpWin32Worker();
        _controller = new ScanController(_scanningContext);
    }

    public async Task<IReadOnlyList<DeviceDto>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        var devices = await GetAvailableDevicesAsync(cancellationToken);
        return devices.Select(item => item.Dto).ToArray();
    }

    public async Task<IReadOnlyList<ScannedPage>> ScanAsync(ScanPdfRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var availableDevices = await GetAvailableDevicesAsync(cancellationToken);
        var selectedDevice = SelectDevice(availableDevices, request.PreferredDeviceId);
        var scanOptions = BuildScanOptions(request, selectedDevice.Device, _qualitySettings);

        _logger.LogInformation(
            "Using scanner {ScannerName} ({ScannerId}) with driver {Driver}. Quality profile: deskew={AutoDeskew}, maxQuality={MaxQuality}, cropToPageSize={CropToPageSize}, pageSize={PageSize}.",
            selectedDevice.Dto.Name,
            selectedDevice.Dto.Id,
            selectedDevice.Dto.Driver,
            _qualitySettings.AutoDeskew,
            _qualitySettings.MaxQuality,
            _qualitySettings.CropToPageSize,
            _qualitySettings.PageSize);

        var pages = new List<ScannedPage>();
        try
        {
            await foreach (var image in _controller.Scan(scanOptions, cancellationToken))
            {
                var pageNumber = pages.Count + 1;
                pages.Add(new ScannedPage(
                    pageNumber,
                    $"Scanned page {pageNumber}",
                    $"Scanned from {selectedDevice.Dto.Name} at {request.Dpi} DPI.",
                    image));
            }
        }
        catch (DeviceFeederEmptyException exception)
        {
            throw new ScannerFeederEmptyException(
                "Le chargeur est vide. Ajoute des feuilles ou utilise la vitre du scanner.",
                exception);
        }
        catch (Exception exception)
        {
            throw new ScannerScanFailedException(
                "Le scanner a renvoye une erreur pendant la numerisation.",
                exception);
        }

        if (pages.Count == 0)
        {
            throw new ScannerScanFailedException("Le scanner n'a renvoye aucune page.");
        }

        _logger.LogInformation("Completed real scan with {PageCount} page(s).", pages.Count);
        return pages;
    }

    public void Dispose()
    {
        _scanningContext.Dispose();
    }

    private async Task<IReadOnlyList<DetectedDevice>> GetAvailableDevicesAsync(CancellationToken cancellationToken)
    {
        var devices = new List<DetectedDevice>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var driver in GetEffectiveDrivers())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var scanDevices = await _controller.GetDeviceList(driver);
                foreach (var scanDevice in scanDevices)
                {
                    if (!seen.Add($"{scanDevice.Driver}:{scanDevice.ID}"))
                    {
                        continue;
                    }

                    devices.Add(new DetectedDevice(
                        scanDevice,
                        new DeviceDto(
                            BuildDeviceId(scanDevice),
                            scanDevice.Name,
                            MapDriver(scanDevice.Driver))));
                }

                _logger.LogInformation(
                    "Detected {DeviceCount} scanner(s) using {Driver}.",
                    scanDevices.Count,
                    driver);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Scanner detection failed for driver {Driver}: {ErrorMessage}",
                    driver,
                    exception.Message);
            }
        }

        if (devices.Count == 0)
        {
            _logger.LogWarning("No scanner was detected through the configured drivers.");
        }

        return devices;
    }

    private IEnumerable<Driver> GetEffectiveDrivers()
        => _preferredDrivers.Length > 0 ? _preferredDrivers : [Driver.Wia];

    private static DetectedDevice SelectDevice(IReadOnlyList<DetectedDevice> devices, string? preferredDeviceId)
    {
        if (devices.Count == 0)
        {
            throw new ScannerUnavailableException("Aucun scanner n'est disponible.");
        }

        if (!string.IsNullOrWhiteSpace(preferredDeviceId))
        {
            var explicitDevice = devices.FirstOrDefault(device =>
                string.Equals(device.Dto.Id, preferredDeviceId, StringComparison.OrdinalIgnoreCase));

            if (explicitDevice is null)
            {
                throw new ScannerNotFoundException($"Le scanner demande '{preferredDeviceId}' est introuvable.");
            }

            return explicitDevice;
        }

        return devices[0];
    }

    private static ScanOptions BuildScanOptions(ScanPdfRequest request, ScanDevice device, ScanQualitySettings qualitySettings)
    {
        var options = new ScanOptions
        {
            Device = device,
            Dpi = request.Dpi > 0 ? request.Dpi : 300,
            PaperSource = MapPaperSource(request),
            BitDepth = MapBitDepth(request.ColorMode),
            AutoDeskew = qualitySettings.AutoDeskew,
            MaxQuality = qualitySettings.MaxQuality,
            Quality = qualitySettings.JpegQuality,
            BrightnessContrastAfterScan = qualitySettings.BrightnessContrastAfterScan,
            Brightness = qualitySettings.Brightness,
            Contrast = qualitySettings.Contrast,
            ExcludeBlankPages = qualitySettings.ExcludeBlankPages,
            BlankPageWhiteThreshold = qualitySettings.BlankPageWhiteThreshold,
            BlankPageCoverageThreshold = qualitySettings.BlankPageCoverageThreshold,
            CropToPageSize = qualitySettings.CropToPageSize,
            WiaOptions = new WiaOptions
            {
                WiaApiVersion = WiaApiVersion.Wia20
            }
        };

        var pageSize = ParsePageSize(qualitySettings.PageSize);
        if (pageSize is not null)
        {
            options.PageSize = pageSize;
        }

        return options;
    }

    private static IEnumerable<Driver> ParsePreferredDrivers(IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            if (TryParseDriver(value, out var driver))
            {
                yield return driver;
            }
        }
    }

    private static bool TryParseDriver(string? value, out Driver driver)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "twain":
                driver = Driver.Twain;
                return true;
            case "wia":
                driver = Driver.Wia;
                return true;
            case "escl":
                driver = Driver.Escl;
                return true;
            default:
                driver = default;
                return false;
        }
    }

    private static DriverKind MapDriver(Driver driver)
        => driver switch
        {
            Driver.Twain => DriverKind.Twain,
            Driver.Wia => DriverKind.Wia,
            _ => DriverKind.Fake
        };

    private static NAPS2.Scan.PaperSource MapPaperSource(ScanPdfRequest request)
        => request.PaperSource switch
        {
            Contracts.PaperSource.Flatbed => NAPS2.Scan.PaperSource.Flatbed,
            _ when request.Duplex => NAPS2.Scan.PaperSource.Duplex,
            _ => NAPS2.Scan.PaperSource.Feeder
        };

    private static BitDepth MapBitDepth(ColorMode colorMode)
        => colorMode switch
        {
            ColorMode.BlackAndWhite => BitDepth.BlackAndWhite,
            ColorMode.Grayscale => BitDepth.Grayscale,
            _ => BitDepth.Color
        };

    private static string BuildDeviceId(ScanDevice device)
        => $"{device.Driver.ToString().ToLowerInvariant()}::{device.ID}";

    private static PageSize? ParsePageSize(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "a4" => PageSize.A4,
            "letter" => PageSize.Letter,
            "legal" => PageSize.Legal,
            null or "" => null,
            _ => null
        };

    private sealed record DetectedDevice(ScanDevice Device, DeviceDto Dto);
}
