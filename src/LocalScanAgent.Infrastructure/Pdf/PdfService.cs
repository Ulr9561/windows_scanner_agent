using System.Globalization;
using System.IO;
using System.Text;
using LocalScanAgent.Application.Abstractions;
using LocalScanAgent.Application.Models;
using NAPS2.Images.ImageSharp;
using NAPS2.Pdf;
using NAPS2.Scan;

namespace LocalScanAgent.Infrastructure.Pdf;

public sealed class PdfService : IPdfService
{
    public async Task<byte[]> CreatePdfAsync(IReadOnlyList<ScannedPage> pages)
    {
        if (pages.Count == 0)
        {
            throw new ArgumentException("At least one page is required to build a PDF.", nameof(pages));
        }

        if (pages.All(page => page.Image is not null))
        {
            return await CreateScannedImagePdfAsync(pages);
        }

        return CreateSyntheticPdf(pages);
    }

    private static async Task<byte[]> CreateScannedImagePdfAsync(IReadOnlyList<ScannedPage> pages)
    {
        using var scanningContext = new ScanningContext(new ImageSharpImageContext());
        var exporter = new PdfExporter(scanningContext);
        using var stream = new MemoryStream();

        var images = pages
            .Select(page => page.Image)
            .OfType<NAPS2.Images.ProcessedImage>()
            .ToList();

        try
        {
            await exporter.Export(stream, images);
            return stream.ToArray();
        }
        finally
        {
            foreach (var image in images)
            {
                image.Dispose();
            }
        }
    }

    private static byte[] CreateSyntheticPdf(IReadOnlyList<ScannedPage> pages)
    {
        var objects = new List<string>
        {
            string.Empty,
            string.Empty
        };

        var pageObjectNumbers = new List<int>(pages.Count);

        foreach (var page in pages)
        {
            var contentStream = BuildContentStream(page);
            var contentLength = Encoding.ASCII.GetByteCount(contentStream);
            var contentObjectNumber = objects.Count + 1;
            objects.Add($"<< /Length {contentLength} >>\nstream\n{contentStream}\nendstream");

            var pageObjectNumber = objects.Count + 1;
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 {(2 * pages.Count) + 3} 0 R >> >> /Contents {contentObjectNumber} 0 R >>");
            pageObjectNumbers.Add(pageObjectNumber);
        }

        objects[1] = $"<< /Type /Pages /Count {pages.Count} /Kids [{string.Join(' ', pageObjectNumbers.Select(number => $"{number} 0 R"))}] >>";
        objects[0] = "<< /Type /Catalog /Pages 2 0 R >>";
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        var documentBuilder = new StringBuilder();
        documentBuilder.AppendLine("%PDF-1.4");

        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(documentBuilder.ToString()));
            documentBuilder.Append(index + 1)
                .AppendLine(" 0 obj")
                .AppendLine(objects[index])
                .AppendLine("endobj");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(documentBuilder.ToString());
        documentBuilder.AppendLine("xref");
        documentBuilder.Append("0 ").AppendLine((objects.Count + 1).ToString(CultureInfo.InvariantCulture));
        documentBuilder.AppendLine("0000000000 65535 f ");

        foreach (var offset in offsets.Skip(1))
        {
            documentBuilder.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).AppendLine(" 00000 n ");
        }

        documentBuilder.AppendLine("trailer");
        documentBuilder.Append("<< /Size ").Append(objects.Count + 1).AppendLine(" /Root 1 0 R >>");
        documentBuilder.AppendLine("startxref");
        documentBuilder.AppendLine(xrefOffset.ToString(CultureInfo.InvariantCulture));
        documentBuilder.AppendLine("%%EOF");

        return Encoding.ASCII.GetBytes(documentBuilder.ToString());
    }

    private static string BuildContentStream(ScannedPage page)
    {
        var lines = page.Body.Split(Environment.NewLine, StringSplitOptions.None);
        var builder = new StringBuilder();
        builder.AppendLine("BT");
        builder.AppendLine("/F1 24 Tf");
        builder.AppendLine("50 780 Td");
        builder.Append('(').Append(Escape(page.Title)).AppendLine(") Tj");
        builder.AppendLine("/F1 12 Tf");

        foreach (var line in lines)
        {
            builder.AppendLine("0 -24 Td");
            builder.Append('(').Append(Escape(line)).AppendLine(") Tj");
        }

        builder.AppendLine("ET");
        return builder.ToString();
    }

    private static string Escape(string value)
        => value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
}
