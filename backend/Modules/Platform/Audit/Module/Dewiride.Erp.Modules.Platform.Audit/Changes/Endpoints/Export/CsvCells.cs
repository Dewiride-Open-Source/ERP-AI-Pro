using System.Buffers;

namespace Dewiride.Erp.Modules.Platform.Audit.Changes.Endpoints.Export;

// RFC 4180, section 2: every cell is enclosed in double quotes and a double quote inside it is written twice, so a comma or a
// line break in a value stays in its cell. A spreadsheet runs a cell that begins with =, +, - or @, their full-width forms, a
// tab or a line break as a formula, so such a cell begins with an apostrophe (OWASP, CSV injection): a value someone typed into
// the ERP never runs on the computer that opens the file.
internal static class CsvCells
{
    private static readonly SearchValues<char> FormulaStarts = SearchValues.Create("=+-@\t\r\n＝＋－＠");

    public static string Of(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && FormulaStarts.Contains(text[0]))
        {
            text = "'" + text;
        }

        return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
