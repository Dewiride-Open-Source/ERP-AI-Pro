using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Text.Unicode;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Inspection;

internal static class ContentTypes
{
    public const char ListSeparator = ';';

    public const int HeadLength = 8 * 1024;

    public const string Pdf = "application/pdf";

    public const string Png = "image/png";

    public const string Jpeg = "image/jpeg";

    public const string Gif = "image/gif";

    public const string Webp = "image/webp";

    public const string PlainText = "text/plain";

    public const string Csv = "text/csv";

    public const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];

    private static ReadOnlySpan<byte> ZipSignature => [0x50, 0x4B, 0x03, 0x04];

    public static IReadOnlyList<string> Known { get; } = [Pdf, Png, Jpeg, Gif, Webp, PlainText, Csv, Xlsx, Docx];

    private static FrozenDictionary<string, string[]> Extensions { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [Pdf] = [".pdf"],
        [Png] = [".png"],
        [Jpeg] = [".jpg", ".jpeg"],
        [Gif] = [".gif"],
        [Webp] = [".webp"],
        [PlainText] = [".txt"],
        [Csv] = [".csv"],
        [Xlsx] = [".xlsx"],
        [Docx] = [".docx"],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static string Normalize(string? declared)
    {
        if (string.IsNullOrWhiteSpace(declared))
        {
            return string.Empty;
        }

        var parameters = declared.IndexOf(';', StringComparison.Ordinal);
        var mediaType = parameters < 0 ? declared : declared[..parameters];

        return mediaType.Trim().ToLowerInvariant();
    }

    public static bool TryParseAllowList(string? value, [NotNullWhen(true)] out IReadOnlySet<string>? allowed, [NotNullWhen(false)] out string? problem)
    {
        allowed = null;
        var entries = (value ?? string.Empty).Split(ListSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (entries.Length == 0)
        {
            problem = $"{AttachmentsOptions.SectionName}:AllowedContentTypes must name at least one content type, separated by '{ListSeparator}'.";
            return false;
        }

        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var normalized = Normalize(entry);
            if (!Known.Contains(normalized, StringComparer.Ordinal))
            {
                problem = $"{AttachmentsOptions.SectionName}:AllowedContentTypes names '{entry}', which has no content check; the checked types are {string.Join(", ", Known)}.";
                return false;
            }

            set.Add(normalized);
        }

        allowed = set;
        problem = null;
        return true;
    }

    // The leading bytes are the only evidence of a file's real type the server gets before storing it; text types have no
    // signature, so they must decode as UTF-8 and contain no NUL, which rules out binary content sent as text.
    public static bool Matches(string contentType, ReadOnlySpan<byte> head, bool isWholeFile) =>
        contentType switch
        {
            Pdf => head.StartsWith("%PDF-"u8),
            Png => head.StartsWith(PngSignature),
            Jpeg => head.StartsWith(JpegSignature),
            Gif => head.StartsWith("GIF87a"u8) || head.StartsWith("GIF89a"u8),
            Webp => head.Length >= 12 && head.StartsWith("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8),
            Xlsx or Docx => head.StartsWith(ZipSignature),
            PlainText or Csv => IsUtf8Text(head, isWholeFile),
            _ => false,
        };

    // A downloaded file is opened by whatever program its extension names, so the name must carry an extension of the checked
    // type: text saved as .hta or .js, or a ZIP saved as .jar, would otherwise run as a program on the reader's computer.
    public static bool NameMatches(string contentType, string fileName) =>
        Extensions.TryGetValue(contentType, out var extensions)
        && extensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);

    public static bool IsText(string contentType) => contentType is PlainText or Csv;

    private static bool IsUtf8Text(ReadOnlySpan<byte> head, bool isWholeFile)
    {
        if (head.Contains((byte)0))
        {
            return false;
        }

        var characters = new char[head.Length];
        var status = Utf8.ToUtf16(head, characters, out _, out _, replaceInvalidSequences: false, isFinalBlock: isWholeFile);

        return status == System.Buffers.OperationStatus.Done || (!isWholeFile && status == System.Buffers.OperationStatus.NeedMoreData);
    }
}
