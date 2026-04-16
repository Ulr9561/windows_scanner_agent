using LocalScanAgent.Application.Abstractions;
using LocalScanAgent.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalScanAgent.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IScanSource>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<IAgentLogger>();
            var mode = ParseScanMode(configuration["Agent:Mode"]);
            var preferredDriverOrder = configuration
                .GetSection("Agent:PreferredDriverOrder")
                .GetChildren()
                .Select(section => section.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToArray();
            var qualitySettings = ReadScanQualitySettings(configuration);

            return mode == ScanMode.Real
                ? new Scanning.Naps2ScanSource(logger, preferredDriverOrder, qualitySettings)
                : new Scanning.FakeScanSource();
        });
        services.AddSingleton<IPdfService, Pdf.PdfService>();
        services.AddSingleton<IAgentLogger, Logging.AgentLogger>();

        return services;
    }

    private static ScanMode ParseScanMode(string? value)
        => Enum.TryParse<ScanMode>(value, ignoreCase: true, out var mode)
            ? mode
            : ScanMode.Fake;

    private static Scanning.ScanQualitySettings ReadScanQualitySettings(IConfiguration configuration)
    {
        var section = configuration.GetSection("Agent:Quality");

        return new Scanning.ScanQualitySettings
        {
            AutoDeskew = ReadBool(section["AutoDeskew"], true),
            MaxQuality = ReadBool(section["MaxQuality"], true),
            BrightnessContrastAfterScan = ReadBool(section["BrightnessContrastAfterScan"], true),
            Brightness = ReadInt(section["Brightness"], 0),
            Contrast = ReadInt(section["Contrast"], 0),
            ExcludeBlankPages = ReadBool(section["ExcludeBlankPages"], false),
            BlankPageWhiteThreshold = ReadInt(section["BlankPageWhiteThreshold"], 99),
            BlankPageCoverageThreshold = ReadInt(section["BlankPageCoverageThreshold"], 2),
            CropToPageSize = ReadBool(section["CropToPageSize"], false),
            PageSize = section["PageSize"] ?? "A4",
            JpegQuality = ReadInt(section["JpegQuality"], 90)
        };
    }

    private static bool ReadBool(string? value, bool defaultValue)
        => bool.TryParse(value, out var parsed) ? parsed : defaultValue;

    private static int ReadInt(string? value, int defaultValue)
        => int.TryParse(value, out var parsed) ? parsed : defaultValue;
}
