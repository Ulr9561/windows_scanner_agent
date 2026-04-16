namespace LocalScanAgent.Infrastructure.Scanning;

public sealed class ScanQualitySettings
{
    public bool AutoDeskew { get; init; } = true;

    public bool MaxQuality { get; init; } = true;

    public bool BrightnessContrastAfterScan { get; init; } = true;

    public int Brightness { get; init; } = 0;

    public int Contrast { get; init; } = 0;

    public bool ExcludeBlankPages { get; init; } = false;

    public int BlankPageWhiteThreshold { get; init; } = 99;

    public int BlankPageCoverageThreshold { get; init; } = 2;

    public bool CropToPageSize { get; init; } = false;

    public string? PageSize { get; init; } = "A4";

    public int JpegQuality { get; init; } = 90;
}
