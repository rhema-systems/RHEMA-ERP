using System.Globalization;
using ClosedXML.Excel;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Api.Services.HR.EmployeeImport;

/// <summary>One data row as it goes back into the checked workbook.</summary>
public sealed record EmployeeImportAnnotatedRow(
    int RowNumber,
    IReadOnlyDictionary<string, string?> Values,
    EmployeeImportRowOutcome Outcome,
    bool Skip,
    IReadOnlyList<EmployeeImportFindingDto> Findings,
    string? CommitMessage);

/// <summary>
/// Writes the three workbooks the feature hands out: the template, the checked copy of an upload,
/// and the follow-up list. Reading lives in <see cref="EmployeeImportWorkbookReader"/>.
/// </summary>
public static class EmployeeImportWorkbooks
{
    private const int ValidationRows = EmployeeImportColumns.MaxDataRows + 1;
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#1F4E78");
    private static readonly XLColor RequiredFill = XLColor.FromHtml("#C00000");
    private static readonly XLColor NoteFill = XLColor.FromHtml("#FFF2CC");
    private static readonly XLColor ErrorFill = XLColor.FromHtml("#F8CBAD");
    private static readonly XLColor WarningFill = XLColor.FromHtml("#FFE699");
    private static readonly XLColor ReadyFill = XLColor.FromHtml("#C6E0B4");

    // ── Template ─────────────────────────────────────────────────────────────────────────────

    public static byte[] BuildTemplate(
        EmployeeImportReferenceData refs, IReadOnlyList<EmployeeImportColumn> columns, string tenantLabel)
    {
        using var wb = new XLWorkbook();
        AddReadMe(wb);
        var lists = AddLists(wb);
        var employees = AddEmployeesSheet(wb, columns, lists, withValidation: true);
        AddExampleRow(employees, columns);
        AddReferenceSheets(wb, refs);
        AddMeta(wb, columns, tenantLabel);

        // Sheet order: Read Me, Employees, then references, Lists, _meta.
        wb.Worksheet(EmployeeImportColumns.ReadMeSheet).Position = 1;
        employees.Position = 2;

        return Save(wb);
    }

    // ── Checked copy ─────────────────────────────────────────────────────────────────────────

    public static byte[] BuildAnnotated(
        EmployeeImportReferenceData refs,
        IReadOnlyList<EmployeeImportColumn> columns,
        IReadOnlyList<EmployeeImportAnnotatedRow> rows,
        string sessionReference,
        string tenantLabel)
    {
        using var wb = new XLWorkbook();
        AddReadMe(wb, checkedCopyOf: sessionReference);
        var lists = AddLists(wb);
        var ws = AddEmployeesSheet(wb, columns, lists, withValidation: true);

        var resultCol = columns.Count + 1;
        var findingsCol = columns.Count + 2;
        StyleHeader(ws.Cell(1, resultCol), EmployeeImportColumns.ResultHeader, HeaderFill);
        StyleHeader(ws.Cell(1, findingsCol), EmployeeImportColumns.FindingsHeader, HeaderFill);
        ws.Column(resultCol).Width = 14;
        ws.Column(findingsCol).Width = 80;

        var byKey = columns.Select((c, i) => (c, i)).ToDictionary(p => p.c.Key, p => p.i + 1);
        var excelRow = 2;
        foreach (var row in rows.OrderBy(r => r.RowNumber))
        {
            foreach (var column in columns)
            {
                if (!row.Values.TryGetValue(column.Key, out var text) || string.IsNullOrEmpty(text)) continue;
                WriteValue(ws.Cell(excelRow, byKey[column.Key]), column, text);
            }

            var label = row.Skip ? "Skipped" : row.Outcome.ToString();
            var resultCell = ws.Cell(excelRow, resultCol);
            resultCell.Value = label;
            resultCell.Style.Fill.BackgroundColor = row.Outcome switch
            {
                EmployeeImportRowOutcome.Error or EmployeeImportRowOutcome.Failed => ErrorFill,
                EmployeeImportRowOutcome.Warning or EmployeeImportRowOutcome.CommittedWithIssues => WarningFill,
                _ => ReadyFill,
            };

            var lines = row.Findings
                .OrderBy(f => f.Severity)
                .Select(f => $"{f.Severity}: {f.Message}" + (f.Suggestions.Count > 0 ? $" (did you mean: {string.Join(", ", f.Suggestions)})" : ""))
                .ToList();
            if (!string.IsNullOrWhiteSpace(row.CommitMessage)) lines.Add("Commit: " + row.CommitMessage);
            var findingsCell = ws.Cell(excelRow, findingsCol);
            findingsCell.Value = string.Join(Environment.NewLine, lines);
            findingsCell.Style.Alignment.WrapText = true;
            findingsCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            findingsCell.Style.Fill.BackgroundColor = NoteFill;

            foreach (var finding in row.Findings)
            {
                if (finding.Column == null || !byKey.TryGetValue(finding.Column, out var col)) continue;
                var cell = ws.Cell(excelRow, col);
                cell.Style.Fill.BackgroundColor = finding.Severity == EmployeeImportFindingSeverity.Error ? ErrorFill : WarningFill;
                var comment = cell.HasComment ? cell.GetComment() : cell.CreateComment();
                comment.AddText($"{finding.Severity}: {finding.Message}");
                if (finding.Suggestions.Count > 0) comment.AddNewLine().AddText("Did you mean: " + string.Join(", ", finding.Suggestions));
                comment.AddNewLine();
            }

            excelRow++;
        }

        AddReferenceSheets(wb, refs);
        AddMeta(wb, columns, tenantLabel);
        wb.Worksheet(EmployeeImportColumns.ReadMeSheet).Position = 1;
        ws.Position = 2;
        return Save(wb);
    }

    // ── Follow-up list ───────────────────────────────────────────────────────────────────────

    public static byte[] BuildFollowUp(IReadOnlyList<EmployeeImportFollowUpDto> items, string sessionReference)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Profiles to complete");
        ws.Cell(1, 1).Value = $"Employees created by import {sessionReference} whose profiles need completing";
        ws.Cell(1, 1).Style.Font.Bold = true;
        var headers = new[] { "Row", "Staff Number", "Name", "What to complete", "Done?" };
        for (var i = 0; i < headers.Length; i++) StyleHeader(ws.Cell(3, i + 1), headers[i], HeaderFill);
        var r = 4;
        foreach (var item in items.OrderBy(i => i.RowNumber))
        {
            ws.Cell(r, 1).Value = (double)item.RowNumber;
            ws.Cell(r, 2).SetValue(item.StaffNumber).Style.NumberFormat.Format = "@";
            ws.Cell(r, 3).Value = item.DisplayName;
            ws.Cell(r, 4).Value = string.Join(Environment.NewLine, item.Items);
            ws.Cell(r, 4).Style.Alignment.WrapText = true;
            ws.Cell(r, 4).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            r++;
        }
        ws.Column(1).Width = 6; ws.Column(2).Width = 16; ws.Column(3).Width = 32; ws.Column(4).Width = 90; ws.Column(5).Width = 8;
        ws.SheetView.FreezeRows(3);
        return Save(wb);
    }

    // ── Pieces ───────────────────────────────────────────────────────────────────────────────

    private static void AddReadMe(XLWorkbook wb, string? checkedCopyOf = null)
    {
        var ws = wb.Worksheets.Add(EmployeeImportColumns.ReadMeSheet);
        var lines = new List<(string Text, bool Bold)>
        {
            (checkedCopyOf == null ? "Employee Import Template" : $"Checked copy of import {checkedCopyOf}", true),
            ("", false),
        };
        if (checkedCopyOf != null)
        {
            lines.Add(("Every row carries a Result and a Findings column, and each cell with a problem is coloured and", false));
            lines.Add(("commented. Fix the cells, delete nothing else, and upload this file again as a new import.", false));
            lines.Add(("Rows marked Error will not import until fixed. Rows marked Warning import and are listed for follow-up.", false));
            lines.Add(("", false));
        }
        lines.AddRange(new (string, bool)[]
        {
            ("How to use", true),
            ("1. Enter one employee per row on the Employees sheet. Do not add, rename or reorder columns.", false),
            ("2. Red headers are required. Everything else may be left blank and completed later in the application.", false),
            ("3. Dates must be real date cells (type 08/07/1970 and Excel stores a date). Text dates are rejected.", false),
            ("4. Staff Number, phone numbers and ID numbers are text columns so leading zeros are kept.", false),
            ("5. Department, Section, Position, Location and Salary Level must match a code or name on the reference sheets.", false),
            ("6. Do not put department banner rows, totals or serial numbers in the data.", false),
            ("7. The example row (Staff Number EXAMPLE-001) is ignored; delete it or overwrite it.", false),
            ("8. Upload the file in HR → Employees → Import. Nothing is written until every row has been checked and you confirm.", false),
            ("9. To change employees already in the register, choose 'Update existing' when uploading: rows are matched by", false),
            ("   Staff Number, only the cells you filled change, and a blank cell leaves the record exactly as it is.", false),
            ("", false),
            ("What happens on upload", true),
            ("Every row is checked and gets one of three results: Ready, Warning (imports, but flagged for follow-up)", false),
            ("and Error (will not import until fixed). You can download the checked file, fix it in Excel and upload again.", false),
            ("", false),
            ("The reference sheets were generated from the system when this file was downloaded. If a department or", false),
            ("position is missing, add it in the application and download the template again.", false),
        });
        for (var i = 0; i < lines.Count; i++)
        {
            var cell = ws.Cell(i + 1, 1);
            cell.Value = lines[i].Text;
            if (lines[i].Bold) cell.Style.Font.Bold = true;
            if (i == 0) cell.Style.Font.FontSize = 14;
        }
        ws.Column(1).Width = 120;
    }

    private static Dictionary<string, IXLRange> AddLists(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add(EmployeeImportColumns.ListsSheet);
        var ranges = new Dictionary<string, IXLRange>();
        var col = 1;
        foreach (var (name, values) in EmployeeImportColumns.Lists)
        {
            ws.Cell(1, col).Value = name;
            ws.Cell(1, col).Style.Font.Bold = true;
            for (var i = 0; i < values.Length; i++) ws.Cell(i + 2, col).Value = values[i];
            ranges[name] = ws.Range(2, col, values.Length + 1, col);
            ws.Column(col).Width = 24;
            col++;
        }
        ws.SheetView.FreezeRows(1);
        return ranges;
    }

    private static IXLWorksheet AddEmployeesSheet(
        XLWorkbook wb, IReadOnlyList<EmployeeImportColumn> columns, Dictionary<string, IXLRange> lists, bool withValidation)
    {
        var ws = wb.Worksheets.Add(EmployeeImportColumns.EmployeesSheet);
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var c = i + 1;
            StyleHeader(ws.Cell(1, c), EmployeeImportColumns.RenderHeader(column), column.Required ? RequiredFill : HeaderFill);
            if (!string.IsNullOrWhiteSpace(column.Help)) ws.Cell(1, c).CreateComment().AddText(column.Help);
            ws.Column(c).Width = column.Width;

            // ⚠ Formats go on the COLUMN, not on a 10,000-row range: a range style materialises a
            // cell per row and the empty template weighed 1.1 MB (measured 2026-09-03). Data
            // validation on a range does not create cells, so it stays on the range.
            var columnStyle = ws.Column(c).Style;
            var body = ws.Range(2, c, ValidationRows, c);
            switch (column.Kind)
            {
                case EmployeeImportColumnKind.Date:
                    columnStyle.DateFormat.Format = "dd/mm/yyyy";
                    if (withValidation)
                    {
                        var dv = body.CreateDataValidation();
                        dv.Date.Between(new DateTime(1930, 1, 1), new DateTime(2100, 12, 31));
                        dv.IgnoreBlanks = true;
                        dv.ShowErrorMessage = true;
                        dv.ErrorStyle = XLErrorStyle.Stop;
                        dv.ErrorTitle = "Not a date";
                        dv.ErrorMessage = "Type a real date such as 08/07/1970.";
                    }
                    break;
                case EmployeeImportColumnKind.WholeNumber:
                    if (withValidation)
                    {
                        var dv = body.CreateDataValidation();
                        dv.WholeNumber.Between(0, 99999);
                        dv.IgnoreBlanks = true;
                        dv.ShowErrorMessage = true;
                        dv.ErrorStyle = XLErrorStyle.Stop;
                        dv.ErrorTitle = "Whole number";
                        dv.ErrorMessage = "Enter a whole number.";
                    }
                    break;
                case EmployeeImportColumnKind.Money:
                    columnStyle.NumberFormat.Format = "#,##0.00";
                    break;
                case EmployeeImportColumnKind.List:
                    if (withValidation && column.ListName != null && lists.TryGetValue(column.ListName, out var range))
                    {
                        var dv = body.CreateDataValidation();
                        dv.List(range, true);
                        dv.IgnoreBlanks = true;
                        dv.ShowErrorMessage = true;
                        dv.ErrorStyle = XLErrorStyle.Stop;
                        dv.ErrorTitle = "Not in list";
                        dv.ErrorMessage = $"Choose one of the values on the Lists sheet ({column.ListName}).";
                    }
                    break;
                default:
                    if (column.TextFormat) columnStyle.NumberFormat.Format = "@";
                    break;
            }
        }
        // The header row must not inherit the column formats (a "@" header is harmless, a date one is not).
        ws.Row(1).Style.NumberFormat.Format = "General";
        ws.Row(1).Height = 32;
        ws.SheetView.Freeze(1, 5);
        return ws;
    }

    private static void AddExampleRow(IXLWorksheet ws, IReadOnlyList<EmployeeImportColumn> columns)
    {
        var sample = new Dictionary<string, object>
        {
            [EmployeeImportColumns.StaffNumber] = EmployeeImportColumns.ExampleStaffNumber,
            [EmployeeImportColumns.Title] = "Mr",
            [EmployeeImportColumns.FirstName] = "Kofi",
            [EmployeeImportColumns.MiddleName] = "A.",
            [EmployeeImportColumns.Surname] = "Example",
            [EmployeeImportColumns.Gender] = "Male",
            [EmployeeImportColumns.DateOfBirth] = new DateTime(1985, 3, 14),
            [EmployeeImportColumns.MaritalStatus] = "Married",
            [EmployeeImportColumns.EmploymentType] = "Permanent",
            [EmployeeImportColumns.DateEmployed] = new DateTime(2015, 6, 1),
            [EmployeeImportColumns.Department] = "(code or name from Departments)",
            [EmployeeImportColumns.Position] = "(code or title from Positions)",
            [EmployeeImportColumns.SalaryLevel] = "(from Salary Structure)",
            [EmployeeImportColumns.Notch] = 1,
            [EmployeeImportColumns.OnPayroll] = "Yes",
            [EmployeeImportColumns.Email] = "kofi.example@example.com",
            [EmployeeImportColumns.MobileNumber] = "0241234567",
            [EmployeeImportColumns.HighestQualification] = "Bachelor of Science",
            [EmployeeImportColumns.Institution] = "University of Ghana",
            [EmployeeImportColumns.YearCompleted] = 2007,
        };
        for (var i = 0; i < columns.Count; i++)
        {
            if (!sample.TryGetValue(columns[i].Key, out var value)) continue;
            var cell = ws.Cell(2, i + 1);
            switch (value)
            {
                case string s: cell.Value = s; break;
                case int n: cell.Value = (double)n; break;
                case DateTime d: cell.Value = d; break;
            }
            cell.Style.Font.Italic = true;
            cell.Style.Font.FontColor = XLColor.Gray;
        }
        ws.Cell(2, 1).CreateComment().AddText("Example row — ignored by the importer. Delete it or overwrite it.");
    }

    private static void AddReferenceSheets(XLWorkbook wb, EmployeeImportReferenceData refs)
    {
        const string banner = "Generated from the system when this file was downloaded. Use the Code or the Name.";

        AddReference(wb, "Departments", banner, ["Code", "Name"],
            refs.Departments.Items.OrderBy(i => i.Display).Select(i => new object[] { i.Item.Code, i.Item.Name }));

        var departmentNames = refs.Departments.Items.ToDictionary(i => i.Item.Id, i => i.Item.Name);
        AddReference(wb, "Sections", banner, ["Code", "Name", "Department"],
            refs.Sections.Items.OrderBy(i => i.Display).Select(i => new object[]
            {
                i.Item.Code, i.Item.Name, departmentNames.GetValueOrDefault(i.Item.DepartmentId, ""),
            }));

        AddReference(wb, "Positions", banner, ["Code", "Title", "Organisation Unit", "Staff Level"],
            refs.Positions.Items.OrderBy(i => i.Display).Select(i => new object[]
            {
                i.Item.Code, i.Item.Title,
                refs.OrganizationUnitNames.GetValueOrDefault(i.Item.OrganizationUnitId, ""),
                i.Item.StaffLevelId.HasValue ? refs.StaffLevelNames.GetValueOrDefault(i.Item.StaffLevelId.Value, "") : "",
            }));

        AddReference(wb, "Locations", banner, ["Code", "Name"],
            refs.Locations.Items.OrderBy(i => i.Display).Select(i => new object[] { i.Item.Code, i.Item.Name }));

        AddReference(wb, "Salary Structure",
            "Salary Level and Notch, as the system holds them. Amounts are the notch amounts on record.",
            ["Grade", "Salary Level", "Notch", "Amount"],
            refs.SalaryLevels.Values.Distinct().OrderBy(l => l.GradeCode).ThenBy(l => l.Code).SelectMany(level =>
                refs.NotchesByLevel.TryGetValue(level.Id, out var notches) && notches.Count > 0
                    ? notches.OrderBy(n => n.Number).Select(n => new object[] { level.GradeCode, level.Code, n.Number, n.Amount })
                    : new[] { new object[] { level.GradeCode, level.Code, "", "" } }));

        AddReference(wb, "Identification Types", "The identification columns on the Employees sheet, one per type configured in the system.",
            ["Type", "Has expiry date"],
            refs.IdentificationTypes.OrderBy(t => t.Name).Select(t => new object[] { t.Name, t.HasExpiryDate ? "Yes" : "No" }));
    }

    private static void AddReference(XLWorkbook wb, string title, string banner, string[] headers, IEnumerable<object[]> rows)
    {
        var ws = wb.Worksheets.Add(title);
        ws.Cell(1, 1).Value = banner;
        ws.Cell(1, 1).Style.Font.Italic = true;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.Gray;
        for (var c = 0; c < headers.Length; c++)
        {
            StyleHeader(ws.Cell(2, c + 1), headers[c], HeaderFill);
            ws.Column(c + 1).Width = Math.Max(16, headers[c].Length + 8);
        }
        var r = 3;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Length; c++)
            {
                var cell = ws.Cell(r, c + 1);
                switch (row[c])
                {
                    case string s: cell.Value = s; break;
                    case int n: cell.Value = (double)n; break;
                    case decimal d: cell.Value = (double)d; cell.Style.NumberFormat.Format = "#,##0.00"; break;
                    case double d: cell.Value = d; break;
                }
            }
            r++;
        }
        ws.Columns(1, headers.Length).AdjustToContents(3, Math.Max(3, r - 1), 12, 60);
        ws.SheetView.FreezeRows(2);
    }

    /// <summary>
    /// The hidden stamp: version, when, for whom, and the binding of every identification column to
    /// its type id — so the reader matches by id, not by header text.
    /// </summary>
    private static void AddMeta(XLWorkbook wb, IReadOnlyList<EmployeeImportColumn> columns, string tenantLabel)
    {
        var ws = wb.Worksheets.Add(EmployeeImportColumns.MetaSheet);
        ws.Cell(1, 1).Value = "TemplateVersion";
        ws.Cell(1, 2).Value = (double)EmployeeImportColumns.TemplateVersion;
        ws.Cell(2, 1).Value = "GeneratedOn";
        ws.Cell(2, 2).Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
        ws.Cell(3, 1).Value = "Tenant";
        ws.Cell(3, 2).Value = tenantLabel;
        var r = 4;
        foreach (var column in columns)
        {
            if (!EmployeeImportColumns.TryParseIdKey(column.Key, out var typeId, out var isExpiry)) continue;
            ws.Cell(r, 1).Value = "IdColumn";
            ws.Cell(r, 2).Value = column.Header;
            ws.Cell(r, 3).Value = typeId.ToString("N");
            ws.Cell(r, 4).Value = isExpiry ? "Expiry" : "Number";
            r++;
        }
        ws.Visibility = XLWorksheetVisibility.Hidden;
    }

    private static void StyleHeader(IXLCell cell, string text, XLColor fill)
    {
        cell.Value = text;
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontColor = XLColor.White;
        cell.Style.Fill.BackgroundColor = fill;
        cell.Style.Alignment.WrapText = true;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    /// <summary>Writes a raw value back in the type its column expects, so the checked copy re-reads cleanly.</summary>
    private static void WriteValue(IXLCell cell, EmployeeImportColumn column, string text)
    {
        switch (column.Kind)
        {
            case EmployeeImportColumnKind.Date when EmployeeImportValues.TryParseDate(text, out var date):
                cell.Value = date.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = "dd/mm/yyyy";
                return;
            case EmployeeImportColumnKind.WholeNumber when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n):
                cell.Value = (double)n;
                return;
            case EmployeeImportColumnKind.Money when decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var d):
                cell.Value = (double)d;
                return;
            default:
                if (column.TextFormat) cell.Style.NumberFormat.Format = "@";
                cell.SetValue(text);
                return;
        }
    }

    private static byte[] Save(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
