using System.Text;

namespace ErpSystem.Core.Services.HR.CompanySchedule;

/// <summary>
/// The company schedule's CSV exports (lane 2g-1, D-9; C-12, C-25) — the leave register's conventions
/// (<c>LeaveService.CsvCell</c>): every cell quoted, a cell a spreadsheet would run as a formula (<c>= + - @</c>) prefixed
/// with an apostrophe, and a UTF-8 byte-order mark so Excel reads the names right.
/// </summary>
internal static class CompanyScheduleCsv
{
    public static string Cell(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@')
            text = "'" + text;
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    /// <summary>A header row and the data rows, as the bytes of a UTF-8 CSV with its byte-order mark.</summary>
    public static byte[] Build(IEnumerable<string> header, IEnumerable<IEnumerable<string?>> rows)
    {
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", header.Select(Cell)));
        foreach (var row in rows) csv.AppendLine(string.Join(",", row.Select(Cell)));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }
}
