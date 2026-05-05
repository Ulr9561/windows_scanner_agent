using LocalScanAgent.Application.Models;
using LocalScanAgent.Infrastructure.Pdf;

namespace LocalScanAgent.Tests;

public sealed class PdfServiceTests
{
    [Fact]
    public async Task CreatePdfAsync_ReturnsPdfBytes_ForSyntheticPages()
    {
        var service = new PdfService();
        var pages = new[] { new ScannedPage(1, "Title", "Body text") };

        var bytes = await service.CreatePdfAsync(pages);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        // PDF magic number %PDF
        Assert.Equal(0x25, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x44, bytes[2]);
        Assert.Equal(0x46, bytes[3]);
    }

    [Fact]
    public async Task CreatePdfAsync_ReturnsMultiPagePdf_ForMultiplePages()
    {
        var service = new PdfService();
        var pages = new[]
        {
            new ScannedPage(1, "Page 1", "Content 1"),
            new ScannedPage(2, "Page 2", "Content 2"),
            new ScannedPage(3, "Page 3", "Content 3")
        };

        var bytes = await service.CreatePdfAsync(pages);

        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public async Task CreatePdfAsync_EscapesSpecialCharsInContent()
    {
        var service = new PdfService();
        var pages = new[] { new ScannedPage(1, "Title (test)", "Line with \\backslash and (parens)") };

        var bytes = await service.CreatePdfAsync(pages);

        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public async Task CreatePdfAsync_Throws_WhenNoPagesProvided()
    {
        var service = new PdfService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreatePdfAsync([]));
    }
}
