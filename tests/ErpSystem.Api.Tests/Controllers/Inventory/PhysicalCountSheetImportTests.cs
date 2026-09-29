using System.Text.Json;
using ClosedXML.Excel;
using ErpSystem.Api.Services;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Services.Inventory;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public class PhysicalCountSheetImportTests
{
    private static List<PhysicalCountItemDto> Items() => new() {
        new() { Id = Guid.NewGuid(), ItemCode = "A", ItemName = "Item A", UnitOfMeasure = "EA" },
        new() { Id = Guid.NewGuid(), ItemCode = "B", ItemName = "Item B", UnitOfMeasure = "EACH" }
    };

    private static MemoryStream Workbook(bool located = false, Action<IXLWorksheet>? change = null)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Count Sheet");
        var headers = located ? new[] { "Item Code", "Item Name", "UOM", "Location", "Counted Qty" }
            : new[] { "Item Code", "Item Name", "UOM", "Counted Qty" };
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Cell(2, 1).Value = "A"; sheet.Cell(2, 2).Value = "Item A"; sheet.Cell(2, 3).Value = "EA";
        sheet.Cell(3, 1).Value = "B"; sheet.Cell(3, 2).Value = "Item B"; sheet.Cell(3, 3).Value = "EACH";
        sheet.Cell(2, headers.Length).Value = 0;
        sheet.Cell(3, headers.Length).Value = 2;
        change?.Invoke(sheet);
        var stream = new MemoryStream(); book.SaveAs(stream); stream.Position = 0; return stream;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImportsActualFileWithFourOrFiveColumnsAndKeepsZero(bool located)
    {
        var items = Items(); using var stream = Workbook(located);
        var rows = PhysicalCountSheetReader.Read(stream, items);
        Assert.Equal(2, rows.Count); Assert.Equal(0, rows[0].CountedQuantity);
        Assert.Equal(items[0].Id, rows[0].PhysicalCountItemId);
        Assert.Equal(2, rows[1].CountedQuantity);
    }

    [Fact]
    public void BlankUncountedItemIsNotSavedAsZero()
    {
        using var stream = Workbook(change: sheet => sheet.Cell(3, 4).Clear());
        Assert.Single(PhysicalCountSheetReader.Read(stream, Items()));
    }

    [Fact]
    public void ReplacementCannotBlankAnAlreadyCountedItem()
    {
        var items = Items(); items[1].IsCounted = true;
        using var stream = Workbook(change: sheet => sheet.Cell(3, 4).Clear());
        Assert.Contains("cannot omit", Assert.Throws<InvalidOperationException>(() => PhysicalCountSheetReader.Read(stream, items)).Message);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("name")]
    [InlineData("uom")]
    [InlineData("unknown")]
    [InlineData("formula")]
    [InlineData("header-formula")]
    [InlineData("negative")]
    [InlineData("precision")]
    [InlineData("boolean")]
    [InlineData("system-column")]
    public void RejectsInvalidWorkbooksBeforeReturningAnyRows(string scenario)
    {
        using var stream = Workbook(change: sheet => {
            switch (scenario) {
                case "missing": sheet.Row(3).Delete(); break;
                case "duplicate": sheet.Cell(3, 1).Value = "A"; break;
                case "name": sheet.Cell(3, 2).Value = "Different item"; break;
                case "uom": sheet.Cell(3, 3).Value = "BOX"; break;
                case "unknown": sheet.Cell(3, 1).Value = "C"; break;
                case "formula": sheet.Cell(3, 4).FormulaA1 = "1+1"; break;
                case "header-formula": sheet.Cell(1, 1).FormulaA1 = "1+1"; break;
                case "negative": sheet.Cell(3, 4).Value = -1; break;
                case "precision": sheet.Cell(3, 4).Value = 1.12345; break;
                case "boolean": sheet.Cell(3, 4).Value = true; break;
                case "system-column": sheet.Cell(1, 5).Value = "System Qty"; break;
            }
        });
        Assert.Throws<InvalidOperationException>(() => PhysicalCountSheetReader.Read(stream, Items()));
    }

    [Fact]
    public void MatchesSavedLocationsAndDoesNotInventThem()
    {
        var items = Items(); items[0].LocationId = Guid.NewGuid(); items[0].LocationName = "LOC-001";
        using var noLocation = Workbook();
        Assert.Throws<InvalidOperationException>(() => PhysicalCountSheetReader.Read(noLocation, items));
        using var located = Workbook(true, sheet => sheet.Cell(2, 4).Value = "LOC-001");
        Assert.Equal(2, PhysicalCountSheetReader.Read(located, items).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImportsDefectiveQuantityAndNotesWithoutReducingPhysicalQuantity(bool located)
    {
        using var stream = Workbook(located, sheet => {
            var quantityColumn = located ? 5 : 4;
            sheet.Cell(1, quantityColumn + 1).Value = "Defective Qty";
            sheet.Cell(1, quantityColumn + 2).Value = "Defective Notes";
            sheet.Cell(2, quantityColumn).Value = 100;
            sheet.Cell(2, quantityColumn + 1).Value = 5;
            sheet.Cell(2, quantityColumn + 2).Value = "Damaged packaging";
        });
        var row = PhysicalCountSheetReader.Read(stream, Items())[0];
        Assert.Equal(100, row.CountedQuantity);
        Assert.Equal(5, row.DefectiveQuantity);
        Assert.Equal("Damaged packaging", row.DefectiveNotes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(0.00001)]
    public void RejectsInvalidDefectiveQuantity(double quantity)
    {
        using var stream = Workbook(change: sheet => {
            sheet.Cell(1, 5).Value = "Defective Qty";
            sheet.Cell(1, 6).Value = "Defective Notes";
            sheet.Cell(2, 4).Value = 100;
            sheet.Cell(2, 5).Value = quantity;
        });
        Assert.Throws<InvalidOperationException>(() => PhysicalCountSheetReader.Read(stream, Items()));
    }

    private static PhysicalCountAction Imported(int sequence, Guid version) => new() {
        Sequence = sequence, ActionType = PhysicalCountActionType.CountRecorded,
        SnapshotJson = JsonSerializer.Serialize(new { payload = new { countSheet = new PhysicalCountSheetBinding(version, "hash", 2, 0) } })
    };

    [Fact]
    public void OnlyLastImportedSheetIsCurrentAndOlderMarkersRemainReadable()
    {
        var old = Imported(2, Guid.NewGuid()); var current = Imported(5, Guid.NewGuid());
        Assert.Equal(PhysicalCountSheetLineage.Read(current), PhysicalCountSheetLineage.Current(new[] { current, old }));
        Assert.NotNull(PhysicalCountSheetLineage.Read(old));
    }

    [Fact]
    public void ManualEditInvalidatesCurrentSheetButUnrelatedAttachmentDoesNot()
    {
        var sheet = Imported(2, Guid.NewGuid());
        var manual = new PhysicalCountAction { Sequence = 3, ActionType = PhysicalCountActionType.CountRecorded, SnapshotJson = "{\"payload\":{\"countedQuantity\":4}}" };
        Assert.Null(PhysicalCountSheetLineage.Current(new[] { sheet, manual }));
        manual.IsDeleted = true;
        Assert.Equal(PhysicalCountSheetLineage.Read(sheet), PhysicalCountSheetLineage.Current(new[] { sheet, manual }));
    }

    [Fact]
    public void LegacyQuantityActionsAreNotGuessedToBeCountSheetImports()
    {
        var action = new PhysicalCountAction { ActionType = PhysicalCountActionType.CountRecorded, SnapshotJson = "{\"payload\":{\"countedQuantity\":2}}" };
        Assert.Null(PhysicalCountSheetLineage.Read(action));
        Assert.Null(PhysicalCountSheetLineage.Current(new[] { action }));
    }
}
