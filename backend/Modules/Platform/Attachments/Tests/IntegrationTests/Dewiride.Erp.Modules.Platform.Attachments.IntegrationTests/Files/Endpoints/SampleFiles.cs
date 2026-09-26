using System.Text;

namespace Dewiride.Erp.Modules.Platform.Attachments.IntegrationTests.Files.Endpoints;

internal static class SampleFiles
{
    public const string PngType = "image/png";

    public const string PdfType = "application/pdf";

    public const string TextType = "text/plain";

    public static byte[] OnePixelPng() =>
        Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    public static byte[] Pdf(string marker) =>
        Encoding.ASCII.GetBytes(
            $"""
            %PDF-1.7
            % {marker}
            1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj
            2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj
            3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >> endobj
            trailer << /Root 1 0 R >>
            %%EOF

            """);

    public static byte[] Text(string text) => Encoding.UTF8.GetBytes(text);
}
