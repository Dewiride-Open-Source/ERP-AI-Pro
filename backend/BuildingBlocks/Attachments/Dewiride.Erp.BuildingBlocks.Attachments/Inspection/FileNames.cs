using System.Globalization;
using System.Text;
using Dewiride.Erp.BuildingBlocks.Attachments.Domain;

namespace Dewiride.Erp.BuildingBlocks.Attachments.Inspection;

internal static class FileNames
{
    // The name is kept only for display and for the download's Content-Disposition: directories a browser or client
    // sent are dropped, and control and formatting characters (bidirectional overrides can disguise an extension) removed.
    // Characters are filtered before normalising because string.Normalize throws on an unpaired surrogate, which
    // EnumerateRunes turns into the replacement character.
    public static string? Sanitize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var lastSeparator = raw.LastIndexOfAny(['/', '\\']);
        var name = lastSeparator < 0 ? raw : raw[(lastSeparator + 1)..];
        var builder = new StringBuilder(name.Length);
        foreach (var rune in name.EnumerateRunes())
        {
            if (rune != Rune.ReplacementChar && Rune.GetUnicodeCategory(rune) is not (UnicodeCategory.Control or UnicodeCategory.Format))
            {
                builder.Append(rune.ToString());
            }
        }

        var sanitized = builder.ToString().Normalize(NormalizationForm.FormC).Trim();

        return sanitized.Length is 0 or > Attachment.FileNameMaxLength || sanitized.All(c => c == '.') ? null : sanitized;
    }
}
