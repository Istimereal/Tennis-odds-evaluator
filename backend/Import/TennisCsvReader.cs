using System.Text;

namespace Tennis.Api.Import;

public static class TennisCsvReader
{
    public static List<string[]> ReadRows(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        while (true)
        {
            var next = reader.Read();
            if (next == -1)
            {
                if (inQuotes)
                {
                    throw new InvalidDataException("The CSV has an unclosed quote.");
                }

                if (field.Length > 0 || row.Count > 0)
                {
                    row.Add(field.ToString());
                    rows.Add(row.ToArray());
                }

                return rows;
            }

            var character = (char)next;
            if (inQuotes)
            {
                if (character != '"')
                {
                    field.Append(character);
                    continue;
                }

                if (reader.Peek() == '"')
                {
                    reader.Read();
                    field.Append('"');
                    continue;
                }

                inQuotes = false;
                continue;
            }

            if (character == '"')
            {
                inQuotes = true;
                continue;
            }

            if (character == ',')
            {
                row.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (character == '\n')
            {
                row.Add(field.ToString());
                field.Clear();
                if (row.Count > 1 || row[0].Length > 0)
                {
                    rows.Add(row.ToArray());
                }

                row.Clear();
                continue;
            }

            if (character != '\r')
            {
                field.Append(character);
            }
        }
    }
}
