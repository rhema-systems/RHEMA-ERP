using System.Globalization;
using ClosedXML.Excel;
using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Api.Services.HR;

/// <summary>
/// Round 2b (recruitment feedback), lane R4b: the establishment of a manpower budget's unit as an
/// Excel workbook the holder edits away from the screen and imports back — "use the position
/// establishment to initiate budget creation … export to Excel, edit, import back".
/// </summary>
/// <remarks>
/// <para>Same idiom as <c>EmployeeImportWorkbooks</c>: a Read Me sheet, one data sheet with the
/// editable columns coloured and validated against a Lists sheet, and a hidden <c>_meta</c> sheet
/// stamping the version and the budget the file came from. Number formats are set on the column,
/// not per cell (the 1.1 MB lesson).</para>
///
/// <para>Rows are matched by the hidden <b>Position Id</b> column, never by title — two posts can
/// share one. A row with nothing typed in an editable cell is left alone; a row for a post not yet
/// on the budget is added only when Planned posts is given. The Notch column is a list whose
/// values map back to ids through the Lists sheet in the same file; the service then checks every
/// id against the tenant's scale, so a tampered Lists sheet can name nothing that is not there.</para>
/// </remarks>
public static class ManpowerBudgetWorkbooks
{
    public const int Version = 1;
    public const string ReadMeSheet = "Read Me";
    public const string LinesSheet = "Lines";
    public const string ListsSheet = "Lists";
    public const string MetaSheet = "_meta";

    private static readonly XLColor HeaderFill = XLColor.FromHtml("#1F4E78");
    private static readonly XLColor EditableHeaderFill = XLColor.FromHtml("#BF8F00");
    private static readonly XLColor EditableFill = XLColor.FromHtml("#FFF2CC");

    // Column order is the contract; the reader maps by header text, so a reordered file still reads.
    private static readonly (string Header, bool Editable, double Width)[] Columns =
    {
        ("Position Id", false, 4),          // hidden
        ("Code", false, 14),
        ("Position", false, 36),
        ("Unit", false, 28),
        ("Established", false, 12),
        ("In post", false, 9),
        ("Gap", false, 8),
        ("Exits due", false, 10),
        ("Suggested new hires", false, 12),
        ("Grade", false, 10),
        ("On budget", false, 10),
        ("Planned posts", true, 13),
        ("Planned new hires", true, 13),
        ("Average salary", true, 15),
        ("Notch", true, 34),
        ("Quarter", true, 9),
        ("Priority", true, 11),
        ("Critical", true, 9),
        ("Notes", true, 50),
    };

    private const int PositionIdCol = 1;
    private const int PlannedPostsCol = 12;
    private const int NewHiresCol = 13;
    private const int SalaryCol = 14;
    private const int NotchCol = 15;
    private const int QuarterCol = 16;
    private const int PriorityCol = 17;
    private const int CriticalCol = 18;
    private const int NotesCol = 19;

    // ── Export ───────────────────────────────────────────────────────────────────────────────

    public static byte[] Build(ManpowerBudgetWorkbookModelDto model)
    {
        using var wb = new XLWorkbook();
        AddReadMe(wb, model);
        var lists = AddLists(wb, model);
        AddLines(wb, model, lists);
        AddMeta(wb, model);
        wb.Worksheet(ReadMeSheet).Position = 1;
        wb.Worksheet(LinesSheet).Position = 2;
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void AddReadMe(XLWorkbook wb, ManpowerBudgetWorkbookModelDto model)
    {
        var ws = wb.Worksheets.Add(ReadMeSheet);
        var lines = new (string Text, bool Bold)[]
        {
            ($"Establishment of {model.OrganizationUnitName} for budget {model.BudgetNumber} ({model.FiscalYear})", true),
            ("", false),
            (model.Editable
                ? $"The budget is {model.StatusName}. Edit the yellow columns on the Lines sheet and import this file back onto the budget."
                : $"The budget is {model.StatusName}. This export is for reference: a workbook can be imported only while the budget is Draft or Rejected.", false),
            ("", false),
            ("How to use", true),
            ("1. One row per post in the unit and the units under it, as the establishment stood when this file was exported.", false),
            ("2. Only the yellow columns are read back: Planned posts, Planned new hires, Average salary, Notch, Quarter, Priority, Critical, Notes.", false),
            ("3. A blank yellow cell leaves that field as it is on the budget. It never clears it.", false),
            ("4. A post not yet on the budget (On budget = No) is added when you give it Planned posts. Leave its row untouched to leave it off.", false),
            ("5. Pick the Notch from the list and the average salary is read from the salary scale. Type an amount to override it.", false),
            ("6. Do not add rows, delete the hidden Position Id column, or rename the columns. Rows are matched by Position Id, not by title.", false),
            ("7. Import the file on the budget page (Import from Excel). Every row is checked first: if any row has a problem, nothing is written.", false),
            ("", false),
            ("The establishment columns (Established, In post, Gap, Exits due, Suggested new hires) were read from the system when the file", false),
            ("was exported and are not imported. The screen shows the live figures.", false),
        };
        for (var i = 0; i < lines.Length; i++)
        {
            var cell = ws.Cell(i + 1, 1);
            cell.Value = lines[i].Text;
            if (lines[i].Bold) cell.Style.Font.Bold = true;
            if (i == 0) cell.Style.Font.FontSize = 14;
        }
        ws.Column(1).Width = 130;
    }

    private static (IXLRange? Notches, IXLRange Priorities, IXLRange YesNo) AddLists(XLWorkbook wb, ManpowerBudgetWorkbookModelDto model)
    {
        var ws = wb.Worksheets.Add(ListsSheet);
        ws.Cell(1, 1).Value = "Notch";
        ws.Cell(1, 2).Value = "Notch Id";
        ws.Cell(1, 4).Value = "Priority";
        ws.Cell(1, 6).Value = "Yes/No";
        ws.Row(1).Style.Font.Bold = true;
        for (var i = 0; i < model.Notches.Count; i++)
        {
            ws.Cell(i + 2, 1).SetValue(model.Notches[i].Label).Style.NumberFormat.Format = "@";
            ws.Cell(i + 2, 2).SetValue(model.Notches[i].Id.ToString("N")).Style.NumberFormat.Format = "@";
        }
        var priorities = new[] { "Critical", "High", "Medium", "Low" };
        for (var i = 0; i < priorities.Length; i++) ws.Cell(i + 2, 4).Value = priorities[i];
        ws.Cell(2, 6).Value = "Yes";
        ws.Cell(3, 6).Value = "No";
        ws.Column(1).Width = 36;
        ws.Column(2).Width = 34;
        ws.Column(4).Width = 12;
        ws.SheetView.FreezeRows(1);
        return (
            model.Notches.Count > 0 ? ws.Range(2, 1, model.Notches.Count + 1, 1) : null,
            ws.Range(2, 4, priorities.Length + 1, 4),
            ws.Range(2, 6, 3, 6));
    }

    private static void AddLines(XLWorkbook wb, ManpowerBudgetWorkbookModelDto model, (IXLRange? Notches, IXLRange Priorities, IXLRange YesNo) lists)
    {
        var ws = wb.Worksheets.Add(LinesSheet);
        for (var c = 0; c < Columns.Length; c++)
        {
            var (header, editable, width) = Columns[c];
            var cell = ws.Cell(1, c + 1);
            cell.Value = header;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = editable ? EditableHeaderFill : HeaderFill;
            cell.Style.Alignment.WrapText = true;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Column(c + 1).Width = width;
        }
        ws.Row(1).Height = 30;

        var r = 2;
        foreach (var row in model.Rows)
        {
            ws.Cell(r, PositionIdCol).SetValue(row.PositionId.ToString("N")).Style.NumberFormat.Format = "@";
            ws.Cell(r, 2).SetValue(row.Code ?? string.Empty).Style.NumberFormat.Format = "@";
            ws.Cell(r, 3).Value = row.Title;
            ws.Cell(r, 4).Value = row.OrganizationUnitName ?? string.Empty;
            ws.Cell(r, 5).Value = row.IsEstablished ? (XLCellValue)(double)row.ExpectedHeadcount : (XLCellValue)"not established";
            ws.Cell(r, 6).Value = (double)row.Filled;
            if (row.Gap.HasValue) ws.Cell(r, 7).Value = (double)row.Gap.Value; else ws.Cell(r, 7).Value = "—";
            ws.Cell(r, 8).Value = (double)row.ExitsDue;
            ws.Cell(r, 9).Value = (double)row.SuggestedNewHires;
            ws.Cell(r, 10).Value = row.GradeCode ?? string.Empty;
            ws.Cell(r, 11).Value = row.OnBudget ? "Yes" : "No";
            if (row.PlannedCount.HasValue) ws.Cell(r, PlannedPostsCol).Value = (double)row.PlannedCount.Value;
            if (row.PlannedNewPositions.HasValue) ws.Cell(r, NewHiresCol).Value = (double)row.PlannedNewPositions.Value;
            if (row.PlannedAverageSalary.HasValue) ws.Cell(r, SalaryCol).Value = (double)row.PlannedAverageSalary.Value;
            if (row.NotchLabel != null) ws.Cell(r, NotchCol).SetValue(row.NotchLabel);
            if (row.Quarter.HasValue) ws.Cell(r, QuarterCol).Value = (double)row.Quarter.Value;
            if (row.PriorityName != null) ws.Cell(r, PriorityCol).Value = row.PriorityName;
            if (row.IsCritical.HasValue) ws.Cell(r, CriticalCol).Value = row.IsCritical.Value ? "Yes" : "No";
            if (row.Notes != null) ws.Cell(r, NotesCol).SetValue(row.Notes);
            r++;
        }

        var last = Math.Max(r - 1, 2);
        var lastValidated = last + 500; // room for nothing — rows cannot be added — but keeps the formats past the data
        foreach (var c in new[] { PlannedPostsCol, NewHiresCol, SalaryCol, NotchCol, QuarterCol, PriorityCol, CriticalCol, NotesCol })
            ws.Range(2, c, last, c).Style.Fill.BackgroundColor = EditableFill;

        ws.Column(SalaryCol).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(NotchCol).Style.NumberFormat.Format = "@";
        ws.Column(NotesCol).Style.NumberFormat.Format = "@";
        ws.Column(NotesCol).Style.Alignment.WrapText = true;

        var whole = ws.Range(2, PlannedPostsCol, lastValidated, NewHiresCol).CreateDataValidation();
        whole.WholeNumber.EqualOrGreaterThan(0);
        whole.ErrorMessage = "A whole number, 0 or more.";
        var money = ws.Range(2, SalaryCol, lastValidated, SalaryCol).CreateDataValidation();
        money.Decimal.EqualOrGreaterThan(0);
        money.ErrorMessage = "An amount, 0 or more.";
        if (lists.Notches != null)
        {
            var notch = ws.Range(2, NotchCol, lastValidated, NotchCol).CreateDataValidation();
            notch.List(lists.Notches, true);
            notch.ErrorMessage = "Pick a notch from the list, or leave the cell blank.";
        }
        var quarter = ws.Range(2, QuarterCol, lastValidated, QuarterCol).CreateDataValidation();
        quarter.WholeNumber.Between(1, 4);
        quarter.ErrorMessage = "1 to 4.";
        var priority = ws.Range(2, PriorityCol, lastValidated, PriorityCol).CreateDataValidation();
        priority.List(lists.Priorities, true);
        var critical = ws.Range(2, CriticalCol, lastValidated, CriticalCol).CreateDataValidation();
        critical.List(lists.YesNo, true);

        ws.Column(PositionIdCol).Hide();
        ws.SheetView.FreezeRows(1);
        ws.SheetView.FreezeColumns(3);
    }

    private static void AddMeta(XLWorkbook wb, ManpowerBudgetWorkbookModelDto model)
    {
        var ws = wb.Worksheets.Add(MetaSheet);
        ws.Cell(1, 1).Value = "Version";
        ws.Cell(1, 2).Value = (double)Version;
        ws.Cell(2, 1).Value = "BudgetId";
        ws.Cell(2, 2).SetValue(model.BudgetId.ToString("N")).Style.NumberFormat.Format = "@";
        ws.Cell(3, 1).Value = "BudgetNumber";
        ws.Cell(3, 2).Value = model.BudgetNumber;
        ws.Cell(4, 1).Value = "GeneratedOn";
        ws.Cell(4, 2).Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
        ws.Visibility = XLWorksheetVisibility.Hidden;
    }

    // ── Import ───────────────────────────────────────────────────────────────────────────────

    public sealed class ReadResult
    {
        public ManpowerBudgetWorkbookImportDto? Import { get; init; }
        public List<string> FileErrors { get; } = new();
        public bool FileRejected => FileErrors.Count > 0;
    }

    /// <summary>
    /// Reads an uploaded workbook into rows. File-level problems (not a workbook, no stamp, wrong
    /// version, no Lines sheet, a required column missing) reject the file; cell-level problems
    /// travel as <see cref="ManpowerBudgetWorkbookImportDto.ReadErrors"/> so the service can report
    /// them together with its own. Nothing here touches the database.
    /// </summary>
    public static ReadResult Read(byte[] bytes)
    {
        var result = new ReadResult();
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(new MemoryStream(bytes, writable: false));
        }
        catch (Exception ex)
        {
            result.FileErrors.Add("The file is not a readable .xlsx workbook. Save it from Excel as 'Excel Workbook (*.xlsx)' and try again. " + ex.Message);
            return result;
        }

        using (workbook)
        {
            if (!workbook.Worksheets.TryGetWorksheet(MetaSheet, out var meta))
            {
                result.FileErrors.Add("This workbook was not exported by the system (the hidden _meta sheet is missing). Export the establishment from the budget page and edit that file.");
                return result;
            }
            var import = new ManpowerBudgetWorkbookImportDto();
            for (var r = 1; r <= (meta.LastRowUsed()?.RowNumber() ?? 0); r++)
            {
                switch (ReadText(meta.Cell(r, 1)))
                {
                    case "Version": import.Version = int.TryParse(ReadText(meta.Cell(r, 2)), out var v) ? v : 0; break;
                    case "BudgetId": import.BudgetId = Guid.TryParseExact(ReadText(meta.Cell(r, 2)), "N", out var id) ? id : Guid.Empty; break;
                }
            }
            if (import.Version != Version)
            {
                result.FileErrors.Add($"This workbook is version {import.Version}; the system now exports version {Version}. Export the establishment again and move your edits across.");
                return result;
            }
            if (import.BudgetId == Guid.Empty)
            {
                result.FileErrors.Add("The workbook's stamp names no budget. Export the establishment again from the budget page.");
                return result;
            }
            if (!workbook.Worksheets.TryGetWorksheet(LinesSheet, out var sheet))
            {
                result.FileErrors.Add($"The '{LinesSheet}' sheet is missing. Export the establishment again; do not rename or remove sheets.");
                return result;
            }

            // header map by text, so a moved column still reads; a missing one rejects the file
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            for (var c = 1; c <= lastCol; c++)
            {
                var h = ReadText(sheet.Cell(1, c));
                if (h != null && !index.ContainsKey(h)) index[h] = c;
            }
            foreach (var (header, _, _) in Columns)
            {
                if (!index.ContainsKey(header))
                {
                    result.FileErrors.Add($"The column '{header}' is missing from the Lines sheet. Do not rename or delete columns; export the establishment again if in doubt.");
                    return result;
                }
            }

            // the notch list: label → id, from the same file
            var notchIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            if (workbook.Worksheets.TryGetWorksheet(ListsSheet, out var lists))
            {
                for (var r = 2; r <= (lists.LastRowUsed()?.RowNumber() ?? 1); r++)
                {
                    var label = ReadText(lists.Cell(r, 1));
                    if (label != null && Guid.TryParseExact(ReadText(lists.Cell(r, 2)), "N", out var id)) notchIds[label] = id;
                }
            }

            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var r = 2; r <= lastRow; r++)
            {
                IXLCell Cell(string header) => sheet.Cell(r, index[header]);
                if (sheet.Row(r).IsEmpty()) continue;
                var row = new ManpowerBudgetWorkbookImportRowDto
                {
                    RowNumber = r,
                    PositionText = ReadText(Cell("Position")),
                    Notes = ReadText(Cell("Notes")),
                    PriorityText = ReadText(Cell("Priority")),
                };
                var idText = ReadText(Cell("Position Id"));
                if (idText != null && (Guid.TryParseExact(idText, "N", out var pid) || Guid.TryParse(idText, out pid))) row.PositionId = pid;

                void Err(string column, string message) => import.ReadErrors.Add(new ManpowerBudgetWorkbookRowErrorDto { Row = r, Column = column, Message = message });
                row.PlannedCount = ReadWhole(Cell("Planned posts"), "Planned posts", Err);
                row.PlannedNewPositions = ReadWhole(Cell("Planned new hires"), "Planned new hires", Err);
                row.PlannedAverageSalary = ReadMoney(Cell("Average salary"), "Average salary", Err);
                row.Quarter = ReadWhole(Cell("Quarter"), "Quarter", Err);
                var notchText = ReadText(Cell("Notch"));
                if (notchText != null)
                {
                    if (notchIds.TryGetValue(notchText, out var nid)) row.SalaryNotchId = nid;
                    else row.NotchText = notchText;
                }
                var critical = ReadText(Cell("Critical"));
                if (critical != null)
                {
                    if (critical.Equals("yes", StringComparison.OrdinalIgnoreCase) || critical.Equals("true", StringComparison.OrdinalIgnoreCase) || critical == "1") row.IsCritical = true;
                    else if (critical.Equals("no", StringComparison.OrdinalIgnoreCase) || critical.Equals("false", StringComparison.OrdinalIgnoreCase) || critical == "0") row.IsCritical = false;
                    else Err("Critical", $"'{critical}' is not Yes or No.");
                }
                import.Rows.Add(row);
            }
            return new ReadResult { Import = import };
        }
    }

    private static string? ReadText(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        var text = cell.DataType == XLDataType.Text ? cell.GetText() : cell.GetFormattedString();
        text = text.Trim();
        return text.Length == 0 ? null : text;
    }

    private static int? ReadWhole(IXLCell cell, string column, Action<string, string> err)
    {
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.Number)
        {
            var d = cell.GetDouble();
            if (Math.Abs(d - Math.Round(d)) > 0.000001) { err(column, $"{d} is not a whole number."); return null; }
            return (int)Math.Round(d);
        }
        var text = ReadText(cell);
        if (text == null) return null;
        if (int.TryParse(text, NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var n)) return n;
        err(column, $"'{text}' is not a whole number.");
        return null;
    }

    private static decimal? ReadMoney(IXLCell cell, string column, Action<string, string> err)
    {
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.Number) return Math.Round((decimal)cell.GetDouble(), 2);
        var text = ReadText(cell);
        if (text == null) return null;
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return Math.Round(d, 2);
        err(column, $"'{text}' is not an amount.");
        return null;
    }
}
