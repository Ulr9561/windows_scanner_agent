namespace LocalScanAgent.Contracts;

public sealed record ScanPdfRequest
{
    public ScanMode Mode { get; init; } = ScanMode.Fake;

    public string? PreferredDeviceId { get; init; }

    public int Dpi { get; init; } = 300;

    public PaperSource PaperSource { get; init; } = PaperSource.Feeder;

    public bool Duplex { get; init; } = false;

    public ColorMode ColorMode { get; init; } = ColorMode.Grayscale;

    public OutputFormat Output { get; init; } = OutputFormat.Pdf;

    public int? SimulatedPages { get; init; }
}
