using System.Globalization;
using System.IO.Compression;
using ClosedXML.Excel;
using ErpSystem.Core.DTOs.Inventory;

namespace ErpSystem.Api.Services;

public static class PhysicalCountSheetReader
{
    public static List<RecordCountItemDto> Read(Stream stream, IReadOnlyList<PhysicalCountItemDto> items)
    {
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
            if (zip.Entries.Count > 2000 || zip.Entries.Sum(e => e.Length) > 32 * 1024 * 1024)
                throw new InvalidOperationException("The count sheet is too large. Use the downloaded template.");
        stream.Position = 0;
        using var workbook = new XLWorkbook(stream);
        if (workbook.Worksheets.Count != 1) throw new InvalidOperationException("Use one count-sheet worksheet.");
        var sheet = workbook.Worksheet(1);
        var columns = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        if (columns is not (4 or 5) || lastRow > 10001)
            throw new InvalidOperationException("Use the downloaded four or five count-sheet columns.");
        if (sheet.CellsUsed().Any(c => c.HasFormula)) throw new InvalidOperationException("Enter counted values, not formulas.");
        var located = columns == 5;
        var headers = located ? new[] { "Item Code", "Item Name", "UOM", "Location", "Counted Qty" }
            : new[] { "Item Code", "Item Name", "UOM", "Counted Qty" };
        if (headers.Where((h, i) => sheet.Cell(1, i + 1).GetString().Trim() != h).Any())
            throw new InvalidOperationException("The count-sheet headings have changed. Download the current template.");
        if (!located && items.Any(i => i.LocationId.HasValue || !string.IsNullOrWhiteSpace(i.LocationName)))
            throw new InvalidOperationException("Keep the Location column for this count's saved locations.");
        var seen = new HashSet<Guid>();
        var result = new List<RecordCountItemDto>();
        for (var row = 2; row <= lastRow; row++)
        {
            string Text(int col) => sheet.Cell(row, col).GetString().Trim();
            if (Enumerable.Range(1, columns).All(col => Text(col).Length == 0)) continue;
            var matches = items.Where(i => Equal(i.ItemCode, Text(1)) && Equal(i.LocationName, located ? Text(4) : "")).ToList();
            if (matches.Count != 1) throw new InvalidOperationException($"Row {row}: item and location must match one saved count line.");
            var item = matches[0];
            if (!seen.Add(item.Id) || !Equal(item.ItemName, Text(2)) || !Equal(item.UnitOfMeasure, Text(3)))
                throw new InvalidOperationException($"Row {row}: duplicate item or changed item name/UOM.");
            var cell = sheet.Cell(row, columns);
            if (cell.IsEmpty() || string.IsNullOrWhiteSpace(Text(columns)))
            {
                if (item.IsCounted) throw new InvalidOperationException($"Row {row}: enter a quantity for {item.ItemCode}; a replacement cannot omit an already saved count.");
                continue;
            }
            decimal quantity;
            if (cell.DataType == XLDataType.Number)
            {
                if (!cell.TryGetValue<decimal>(out quantity)) throw new InvalidOperationException($"Row {row}: Counted Qty is too large.");
            }
            else if (cell.DataType != XLDataType.Text || !decimal.TryParse(Text(columns), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out quantity))
                throw new InvalidOperationException($"Row {row}: enter a numeric Counted Qty.");
            if (quantity < 0 || quantity > 99999999999999.9999m || decimal.Round(quantity, 4) != quantity)
                throw new InvalidOperationException($"Row {row}: Counted Qty must be non-negative with at most four decimal places.");
            result.Add(new RecordCountItemDto { PhysicalCountItemId = item.Id, CountedQuantity = quantity,
                LotNumber = item.LotNumber, SerialNumber = item.SerialNumber, Notes = item.Notes });
        }
        if (seen.Count != items.Count) throw new InvalidOperationException("Keep every item row in the count sheet. Leave only uncounted quantities blank.");
        if (result.Count == 0) throw new InvalidOperationException("Enter at least one counted quantity.");
        return result;
    }

    private static bool Equal(string? left, string? right) => string.Equals(left?.Trim() ?? "", right?.Trim() ?? "", StringComparison.OrdinalIgnoreCase);
}
