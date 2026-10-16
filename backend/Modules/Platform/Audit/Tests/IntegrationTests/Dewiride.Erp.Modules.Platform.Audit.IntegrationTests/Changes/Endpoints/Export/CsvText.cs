using System.Text;

namespace Dewiride.Erp.Modules.Platform.Audit.IntegrationTests.Changes.Endpoints.Export;

// Reads RFC 4180 text the way a spreadsheet does, so a test sees each cell as the person who opens the file would.
internal static class CsvText
{
    public static List<List<string>> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lines = new List<List<string>>();
        var cells = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (quoted)
            {
                if (character != '"')
                {
                    cell.Append(character);
                }
                else if (index + 1 < text.Length && text[index + 1] == '"')
                {
                    cell.Append('"');
                    index++;
                }
                else
                {
                    quoted = false;
                }
            }
            else if (character == '"')
            {
                quoted = true;
            }
            else if (character == ',')
            {
                cells.Add(cell.ToString());
                cell.Clear();
            }
            else if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                cells.Add(cell.ToString());
                cell.Clear();
                lines.Add(cells);
                cells = [];
                index++;
            }
            else
            {
                cell.Append(character);
            }
        }

        Assert.False(quoted, "The file ends inside a quoted cell.");
        Assert.True(cell.Length == 0 && cells.Count == 0, "The last line of the file has no line end.");

        return lines;
    }
}
