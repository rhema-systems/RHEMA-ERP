using System.Globalization;
using System.Security.Cryptography;
using ClosedXML.Excel;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Documents;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Colors = QuestPDF.Helpers.Colors;

namespace ErpSystem.Api.Services.Documents.Finance;

/// <summary>
/// Produces controlled supplier statements from the existing AP detailed-ledger read service.
/// This builder owns presentation only: it must not query or recalculate invoices, payments,
/// discounts, WHT or adjustments independently because the on-screen report, PDF and workbook
/// must reconcile to one canonical statement result.
/// </summary>
public sealed class ApSupplierStatementDocumentBuilder : IDocumentBuilder
{
    private const string FinanceModule = "AP";
    private const string PdfFormat = "pdf";
    private const string XlsxFormat = "xlsx";
    private const string SpreadsheetContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string MoneyNumberFormat = "#,##0.00;[Red](#,##0.00);-";
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

    private readonly IApReportsService _apReportsService;
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceAuditService? _financeAuditService;

    public ApSupplierStatementDocumentBuilder(
        IApReportsService apReportsService,
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IFinanceAuditService? financeAuditService = null)
    {
        _apReportsService = apReportsService;
        _context = context;
        _currentUser = currentUser;
        _financeAuditService = financeAuditService;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string DocumentType => DocumentTypes.FinanceApSupplierStatement;

    public bool RequiresEntityId => false;

    public bool SupportsFormat(string format)
        => string.Equals(format, PdfFormat, StringComparison.OrdinalIgnoreCase)
           || string.Equals(format, XlsxFormat, StringComparison.OrdinalIgnoreCase);

    public async Task<RenderedDocumentDto> RenderAsync(
        DocumentRenderRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsFormat(request.Format))
        {
            throw new NotSupportedException("AP supplier statements support PDF and XLSX output only.");
        }

        var tenantId = _currentUser.TenantId
            ?? throw new UnauthorizedAccessException("Invalid tenant context.");
        var generatedAtUtc = DateTime.UtcNow;

        try
        {
            var parameters = ParseParameters(request, generatedAtUtc);
            var tenant = await _context.Tenants
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == tenantId && !item.IsDeleted, cancellationToken)
                ?? throw new KeyNotFoundException("The current tenant was not found.");

            // This is the same read model used by the AP statement screen and CSV export. Keeping
            // rendering downstream of this call prevents three subtly different balance engines.
            var report = await _apReportsService.GetSupplierDetailedLedgerAsync(
                parameters.FromDate,
                parameters.ToDate,
                parameters.BusinessPartnerIds,
                parameters.ShowSupplierCurrency,
                cancellationToken);

            var content = string.Equals(request.Format, PdfFormat, StringComparison.OrdinalIgnoreCase)
                ? BuildPdf(tenant, report, generatedAtUtc, _currentUser.UserName)
                : BuildWorkbook(tenant, report, generatedAtUtc, _currentUser.UserName);
            var outputHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            var fileStem = BuildFileStem(report);

            await RecordSuccessAuditAsync(
                tenantId,
                request.Format,
                report,
                content.Length,
                outputHash,
                generatedAtUtc,
                cancellationToken);

            return new RenderedDocumentDto
            {
                Content = content,
                ContentType = string.Equals(request.Format, PdfFormat, StringComparison.OrdinalIgnoreCase)
                    ? "application/pdf"
                    : SpreadsheetContentType,
                FileName = $"{fileStem}.{request.Format.ToLowerInvariant()}",
                DocumentType = DocumentType,
                EntityId = Guid.Empty,
                Format = request.Format.ToLowerInvariant()
            };
        }
        catch (Exception exception)
        {
            // Failed generation is material because a user may otherwise assume that a statement
            // was issued. Record the requested parameters without leaking another tenant's data.
            await RecordFailureAuditAsync(tenantId, request, exception, generatedAtUtc, cancellationToken);
            throw;
        }
    }

    private static StatementParameters ParseParameters(DocumentRenderRequestDto request, DateTime generatedAtUtc)
    {
        var defaultToDate = generatedAtUtc.Date;
        var defaultFromDate = new DateTime(defaultToDate.Year, defaultToDate.Month, 1);
        var fromDate = ParseDateOption(request, "fromDate", defaultFromDate);
        var toDate = ParseDateOption(request, "toDate", defaultToDate);

        if (toDate < fromDate)
        {
            throw new ArgumentException("The statement end date must be on or after the start date.");
        }

        var businessPartnerIds = ParseGuidListOption(request, "businessPartnerIds", "businessPartnerId");
        var showSupplierCurrency = ParseBoolOption(request, "showSupplierCurrency", false);
        return new StatementParameters(fromDate, toDate, businessPartnerIds, showSupplierCurrency);
    }

    private static DateTime ParseDateOption(
        DocumentRenderRequestDto request,
        string key,
        DateTime fallback)
    {
        if (request.Options == null
            || !request.Options.TryGetValue(key, out var rawValue)
            || string.IsNullOrWhiteSpace(rawValue))
        {
            return fallback.Date;
        }

        if (!DateTime.TryParse(rawValue, InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            throw new ArgumentException($"'{rawValue}' is not a valid {key} statement date.");
        }

        return parsed.Date;
    }

    private static bool ParseBoolOption(
        DocumentRenderRequestDto request,
        string key,
        bool fallback)
    {
        if (request.Options == null
            || !request.Options.TryGetValue(key, out var rawValue)
            || string.IsNullOrWhiteSpace(rawValue))
        {
            return fallback;
        }

        if (!bool.TryParse(rawValue, out var parsed))
        {
            throw new ArgumentException($"'{rawValue}' is not a valid {key} value.");
        }

        return parsed;
    }

    private static IReadOnlyCollection<Guid> ParseGuidListOption(
        DocumentRenderRequestDto request,
        params string[] keys)
    {
        if (request.Options == null)
        {
            return Array.Empty<Guid>();
        }

        var ids = new HashSet<Guid>();
        foreach (var key in keys)
        {
            if (!request.Options.TryGetValue(key, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            foreach (var token in rawValue.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Guid.TryParse(token, out var id) || id == Guid.Empty)
                {
                    throw new ArgumentException($"'{token}' is not a valid supplier identifier.");
                }

                ids.Add(id);
            }
        }

        return ids.ToArray();
    }

    private static byte[] BuildPdf(
        Tenant tenant,
        SupplierDetailedLedgerReportDto report,
        DateTime generatedAtUtc,
        string? generatedBy)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.PageColor(Colors.White);
                // Disable standard ligatures so supplier names, document references and statement
                // descriptions remain byte-for-byte searchable when Finance archives or copies
                // text from the PDF. This changes no visual layout.
                page.DefaultTextStyle(style => style
                    .FontSize(7.5f)
                    .FontColor(Colors.Grey.Darken4)
                    .DisableFontFeature(FontFeatures.StandardLigatures));

                page.Header().Element(header => ComposePdfHeader(header, tenant, report));
                page.Content().PaddingVertical(10).Element(content =>
                    ComposePdfContent(content, report));
                page.Footer().Element(footer =>
                    ComposePdfFooter(footer, generatedAtUtc, generatedBy));
            });
        }).GeneratePdf();
    }

    private static void ComposePdfHeader(
        IContainer container,
        Tenant tenant,
        SupplierDetailedLedgerReportDto report)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(tenant.Name).Bold().FontSize(13).FontColor(Colors.Blue.Darken3);
                    left.Item().Text(string.IsNullOrWhiteSpace(tenant.Address) ? "Tema, Ghana" : tenant.Address)
                        .FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(310).AlignRight().Column(right =>
                {
                    right.Item().Text("SUPPLIER STATEMENT OF ACCOUNT").Bold().FontSize(15).FontColor(Colors.Blue.Darken3);
                    right.Item().Text($"Period: {report.FromDate:dd MMM yyyy} to {report.ToDate:dd MMM yyyy}")
                        .FontSize(8.5f);
                });
            });

            column.Item().PaddingTop(7).LineHorizontal(1).LineColor(Colors.Blue.Darken2);
        });
    }

    private static void ComposePdfContent(
        IContainer container,
        SupplierDetailedLedgerReportDto report)
    {
        container.Column(column =>
        {
            if (report.Suppliers.Count == 0)
            {
                column.Item().PaddingTop(30).AlignCenter().Text("No supplier statement activity was found for the selected period.")
                    .Italic().FontSize(10).FontColor(Colors.Grey.Darken1);
                return;
            }

            for (var supplierIndex = 0; supplierIndex < report.Suppliers.Count; supplierIndex++)
            {
                if (supplierIndex > 0)
                {
                    // Batch exports retain one supplier per page section. This avoids interleaving
                    // counterparties when a long table flows across physical pages.
                    column.Item().PageBreak();
                }

                ComposePdfSupplier(column, report.Suppliers[supplierIndex], report.FromDate, report.Warnings);
            }
        });
    }

    private static void ComposePdfSupplier(
        ColumnDescriptor column,
        SupplierDetailedLedgerAccountDto supplier,
        DateTime fromDate,
        IReadOnlyCollection<string> warnings)
    {
        column.Item().Row(row =>
        {
            row.RelativeItem().Column(identity =>
            {
                identity.Item().Text(supplier.SupplierName).Bold().FontSize(11);
                identity.Item().Text($"Supplier code: {Fallback(supplier.SupplierCode, "Not assigned")} | Statement currency: {supplier.CurrencyCode}")
                    .FontColor(Colors.Grey.Darken1);
            });
            row.ConstantItem(480).Element(summary => ComposePdfSummary(summary, supplier));
        });

        column.Item().PaddingTop(8).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(58);
                columns.ConstantColumn(78);
                columns.ConstantColumn(92);
                columns.ConstantColumn(105);
                columns.RelativeColumn(2.4f);
                columns.ConstantColumn(76);
                columns.ConstantColumn(76);
                columns.ConstantColumn(83);
            });

            table.Header(header =>
            {
                PdfHeaderCell(header, "Date");
                PdfHeaderCell(header, "Type");
                PdfHeaderCell(header, "Document");
                PdfHeaderCell(header, "Reference");
                PdfHeaderCell(header, "Description");
                PdfHeaderCell(header, "Debit", alignRight: true);
                PdfHeaderCell(header, "Credit", alignRight: true);
                PdfHeaderCell(header, "Balance", alignRight: true);
            });

            PdfBodyCell(table, fromDate.ToString("dd MMM yy", InvariantCulture));
            PdfBodyCell(table, "Opening");
            PdfBodyCell(table, string.Empty);
            PdfBodyCell(table, string.Empty);
            PdfBodyCell(table, "Opening balance brought forward");
            PdfBodyCell(table, string.Empty, alignRight: true);
            PdfBodyCell(table, string.Empty, alignRight: true);
            PdfBodyCell(table, Money(supplier.OpeningBalance), alignRight: true, isTotal: true);

            foreach (var line in supplier.Lines)
            {
                PdfBodyCell(table, line.TransactionDate.ToString("dd MMM yy", InvariantCulture));
                PdfBodyCell(table, line.TransactionType);
                PdfBodyCell(table, line.DocumentNumber);
                PdfBodyCell(table, Fallback(line.Reference, "-"));
                PdfBodyCell(table, line.Description);
                PdfBodyCell(table, MoneyOrBlank(line.Debit), alignRight: true);
                PdfBodyCell(table, MoneyOrBlank(line.Credit), alignRight: true);
                PdfBodyCell(table, Money(line.RunningBalance), alignRight: true);
            }

            PdfBodyCell(table, string.Empty, isTotal: true);
            PdfBodyCell(table, "Closing", isTotal: true);
            PdfBodyCell(table, string.Empty, isTotal: true);
            PdfBodyCell(table, string.Empty, isTotal: true);
            PdfBodyCell(table, "Closing balance carried forward", isTotal: true);
            PdfBodyCell(table, Money(supplier.TotalDebits), alignRight: true, isTotal: true);
            PdfBodyCell(table, Money(supplier.TotalCredits), alignRight: true, isTotal: true);
            PdfBodyCell(table, Money(supplier.ClosingBalance), alignRight: true, isTotal: true);
        });

        column.Item().PaddingTop(6).Text(
                "AP convention: credits increase the supplier payable; debits (payments, discounts, WHT and credits) reduce it.")
            .FontSize(6.8f).Italic().FontColor(Colors.Grey.Darken1);
        column.Item().Text("This statement is generated from the Finance AP subledger and is not a payment instruction.")
            .FontSize(6.8f).Italic().FontColor(Colors.Grey.Darken1);

        foreach (var warning in warnings)
        {
            column.Item().PaddingTop(2).Text($"Currency note: {warning}")
                .FontSize(6.8f).FontColor(Colors.Orange.Darken3);
        }
    }

    private static void ComposePdfSummary(IContainer container, SupplierDetailedLedgerAccountDto supplier)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Row(row =>
        {
            PdfSummaryItem(row, "Opening", supplier.OpeningBalance, supplier.CurrencyCode);
            PdfSummaryItem(row, "Debits", supplier.TotalDebits, supplier.CurrencyCode);
            PdfSummaryItem(row, "Credits", supplier.TotalCredits, supplier.CurrencyCode);
            PdfSummaryItem(row, "Closing", supplier.ClosingBalance, supplier.CurrencyCode, emphasize: true);
        });
    }

    private static void PdfSummaryItem(
        RowDescriptor row,
        string label,
        decimal amount,
        string currency,
        bool emphasize = false)
    {
        row.RelativeItem().PaddingHorizontal(4).Column(column =>
        {
            column.Item().Text(label).FontSize(6.5f).FontColor(Colors.Grey.Darken1);
            var text = column.Item().Text($"{currency} {Money(amount)}").FontSize(8f);
            if (emphasize)
            {
                text.Bold().FontColor(Colors.Blue.Darken3);
            }
        });
    }

    private static void PdfHeaderCell(TableCellDescriptor header, string value, bool alignRight = false)
    {
        var cell = header.Cell()
            .Background(Colors.Blue.Darken3)
            .PaddingVertical(5)
            .PaddingHorizontal(4)
            .DefaultTextStyle(style => style.SemiBold().FontSize(7).FontColor(Colors.White));
        if (alignRight)
        {
            cell = cell.AlignRight();
        }

        cell.Text(value);
    }

    private static void PdfBodyCell(
        TableDescriptor table,
        string value,
        bool alignRight = false,
        bool isTotal = false)
    {
        var cell = table.Cell()
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten3)
            .Background(isTotal ? Colors.Grey.Lighten4 : Colors.White)
            .PaddingVertical(4)
            .PaddingHorizontal(4);
        if (alignRight)
        {
            cell = cell.AlignRight();
        }

        var text = cell.Text(value);
        if (isTotal)
        {
            text.SemiBold();
        }
    }

    private static void ComposePdfFooter(
        IContainer container,
        DateTime generatedAtUtc,
        string? generatedBy)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(5).Row(row =>
        {
            row.RelativeItem().Text(text =>
            {
                text.Span($"Generated {generatedAtUtc:yyyy-MM-dd HH:mm} UTC");
                if (!string.IsNullOrWhiteSpace(generatedBy))
                {
                    text.Span($" by {generatedBy}");
                }
            });
            row.RelativeItem().AlignRight().Text(text =>
            {
                text.Span("Controlled Finance output | Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        });
    }

    private static byte[] BuildWorkbook(
        Tenant tenant,
        SupplierDetailedLedgerReportDto report,
        DateTime generatedAtUtc,
        string? generatedBy)
    {
        using var workbook = new XLWorkbook();
        workbook.Properties.Title = "AP Supplier Statements";
        workbook.Properties.Subject = "Controlled AP supplier statement export";
        workbook.Properties.Author = string.IsNullOrWhiteSpace(generatedBy) ? tenant.Name : generatedBy;
        workbook.Properties.Comments = "Generated from the same AP detailed-ledger result used by the Finance report screen.";

        var summary = workbook.Worksheets.Add("Summary");
        var supplierSheets = new List<SupplierSheetReference>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { summary.Name };

        foreach (var supplier in report.Suppliers)
        {
            var sheetName = UniqueWorksheetName(supplier.SupplierCode, supplier.SupplierName, usedNames);
            var sheet = workbook.Worksheets.Add(sheetName);
            supplierSheets.Add(BuildSupplierWorksheet(sheet, tenant, report, supplier, generatedAtUtc, generatedBy));
        }

        BuildSummaryWorksheet(summary, tenant, report, supplierSheets, generatedAtUtc, generatedBy);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static SupplierSheetReference BuildSupplierWorksheet(
        IXLWorksheet sheet,
        Tenant tenant,
        SupplierDetailedLedgerReportDto report,
        SupplierDetailedLedgerAccountDto supplier,
        DateTime generatedAtUtc,
        string? generatedBy)
    {
        sheet.ShowGridLines = false;
        sheet.SheetView.FreezeRows(9);
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.Margins.Top = 0.35;
        sheet.PageSetup.Margins.Bottom = 0.35;
        sheet.PageSetup.Margins.Left = 0.3;
        sheet.PageSetup.Margins.Right = 0.3;

        sheet.Range("A1:H1").Merge().Value = tenant.Name;
        StyleTitle(sheet.Range("A1:H1"), 15);
        sheet.Range("A2:H2").Merge().Value = "SUPPLIER STATEMENT OF ACCOUNT";
        StyleTitle(sheet.Range("A2:H2"), 13);

        sheet.Cell("A3").Value = "Supplier";
        sheet.Cell("B3").Value = supplier.SupplierName;
        sheet.Range("B3:D3").Merge();
        sheet.Cell("E3").Value = "Supplier code";
        sheet.Cell("F3").Value = Fallback(supplier.SupplierCode, "Not assigned");
        sheet.Cell("G3").Value = "Currency";
        sheet.Cell("H3").Value = supplier.CurrencyCode;

        sheet.Cell("A4").Value = "Statement period";
        sheet.Cell("B4").Value = report.FromDate;
        sheet.Cell("B4").Style.DateFormat.Format = "dd mmm yyyy";
        sheet.Cell("C4").Value = "to";
        sheet.Cell("D4").Value = report.ToDate;
        sheet.Cell("D4").Style.DateFormat.Format = "dd mmm yyyy";
        sheet.Cell("E4").Value = "Generated UTC";
        sheet.Cell("F4").Value = generatedAtUtc;
        sheet.Cell("F4").Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
        sheet.Cell("G4").Value = "Generated by";
        sheet.Cell("H4").Value = Fallback(generatedBy, "System");
        sheet.Range("A3:H4").Style.Border.BottomBorder = XLBorderStyleValues.Hair;
        sheet.Range("A3:H4").Style.Border.BottomBorderColor = XLColor.LightGray;

        var summaryLabels = new[] { "Opening Balance", "Period Debits", "Period Credits", "Closing Balance" };
        var summaryValues = new[] { supplier.OpeningBalance, supplier.TotalDebits, supplier.TotalCredits, supplier.ClosingBalance };
        for (var index = 0; index < summaryLabels.Length; index++)
        {
            var column = 1 + index * 2;
            sheet.Cell(6, column).Value = summaryLabels[index];
            sheet.Cell(7, column).Value = summaryValues[index];
            sheet.Range(6, column, 6, column + 1).Merge();
            sheet.Range(7, column, 7, column + 1).Merge();
            sheet.Range(6, column, 7, column + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            sheet.Range(6, column, 7, column + 1).Style.Border.OutsideBorderColor = XLColor.LightGray;
            sheet.Range(6, column, 6, column + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE6F1");
            sheet.Range(6, column, 6, column + 1).Style.Font.Bold = true;
            sheet.Range(7, column, 7, column + 1).Style.NumberFormat.Format = MoneyNumberFormat;
            sheet.Range(7, column, 7, column + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }

        sheet.Range("G6:H7").Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F1FB");
        sheet.Range("G6:H7").Style.Font.Bold = true;

        var headers = new[] { "Date", "Type", "Document", "Reference", "Description", "Debit", "Credit", "Running Balance" };
        for (var index = 0; index < headers.Length; index++)
        {
            sheet.Cell(9, index + 1).Value = headers[index];
        }
        StyleTableHeader(sheet.Range("A9:H9"));

        const int openingRow = 10;
        sheet.Cell(openingRow, 1).Value = report.FromDate;
        sheet.Cell(openingRow, 2).Value = "Opening Balance";
        sheet.Cell(openingRow, 5).Value = "Opening balance brought forward";
        sheet.Cell(openingRow, 8).FormulaA1 = "=$A$7";
        StyleStatementRow(sheet.Range(openingRow, 1, openingRow, 8), isTotal: true);

        var row = openingRow + 1;
        foreach (var line in supplier.Lines)
        {
            sheet.Cell(row, 1).Value = line.TransactionDate;
            sheet.Cell(row, 2).Value = line.TransactionType;
            sheet.Cell(row, 3).Value = line.DocumentNumber;
            sheet.Cell(row, 4).Value = line.Reference ?? string.Empty;
            sheet.Cell(row, 5).Value = line.Description;
            sheet.Cell(row, 6).Value = line.Debit;
            sheet.Cell(row, 7).Value = line.Credit;
            // AP running balance uses the control-account convention: credit increases the
            // payable, while debit reduces it. A live formula makes edits visibly reconcile.
            sheet.Cell(row, 8).FormulaA1 = $"=H{row - 1}-F{row}+G{row}";
            StyleStatementRow(sheet.Range(row, 1, row, 8));
            row++;
        }

        var closingRow = row;
        sheet.Cell(closingRow, 2).Value = "Closing Balance";
        sheet.Cell(closingRow, 5).Value = "Closing balance carried forward";
        if (supplier.Lines.Count == 0)
        {
            sheet.Cell(closingRow, 6).Value = 0m;
            sheet.Cell(closingRow, 7).Value = 0m;
        }
        else
        {
            sheet.Cell(closingRow, 6).FormulaA1 = $"=SUM(F{openingRow + 1}:F{closingRow - 1})";
            sheet.Cell(closingRow, 7).FormulaA1 = $"=SUM(G{openingRow + 1}:G{closingRow - 1})";
        }
        sheet.Cell(closingRow, 8).FormulaA1 = $"=H{closingRow - 1}";
        StyleStatementRow(sheet.Range(closingRow, 1, closingRow, 8), isTotal: true);

        sheet.Range(9, 1, closingRow, 8).SetAutoFilter();
        sheet.Cell(closingRow + 2, 7).Value = "Reconciliation difference";
        sheet.Cell(closingRow + 2, 8).FormulaA1 = $"=ROUND(H{closingRow}-$G$7,2)";
        sheet.Cell(closingRow + 3, 7).Value = "Statement check";
        sheet.Cell(closingRow + 3, 8).FormulaA1 = $"=IF(ABS(H{closingRow + 2})<=0.01,\"PASS\",\"FAIL\")";
        sheet.Range(closingRow + 2, 7, closingRow + 3, 8).Style.Font.Bold = true;
        sheet.Range(closingRow + 2, 7, closingRow + 3, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Cell(closingRow + 2, 8).Style.NumberFormat.Format = MoneyNumberFormat;

        var noteRow = closingRow + 5;
        sheet.Range(noteRow, 1, noteRow, 8).Merge().Value =
            "AP convention: credits increase the supplier payable; debits (payments, discounts, WHT and credits) reduce it.";
        sheet.Range(noteRow + 1, 1, noteRow + 1, 8).Merge().Value =
            "Controlled Finance output generated from the AP detailed ledger. This statement is not a payment instruction.";
        sheet.Range(noteRow, 1, noteRow + 1, 8).Style.Font.Italic = true;
        sheet.Range(noteRow, 1, noteRow + 1, 8).Style.Font.FontColor = XLColor.DimGray;

        if (report.Warnings.Count > 0)
        {
            var warningRow = noteRow + 3;
            foreach (var warning in report.Warnings)
            {
                sheet.Range(warningRow, 1, warningRow, 8).Merge().Value = $"Currency note: {warning}";
                sheet.Range(warningRow, 1, warningRow, 8).Style.Font.FontColor = XLColor.DarkOrange;
                sheet.Range(warningRow, 1, warningRow, 8).Style.Alignment.WrapText = true;
                warningRow++;
            }
        }

        sheet.Column(1).Width = 13;
        sheet.Column(2).Width = 20;
        sheet.Column(3).Width = 19;
        sheet.Column(4).Width = 22;
        sheet.Column(5).Width = 42;
        sheet.Column(6).Width = 17;
        sheet.Column(7).Width = 24;
        sheet.Column(8).Width = 18;
        sheet.Rows(1, 2).Height = 23;
        sheet.RangeUsed()!.Style.Font.FontName = "Aptos";
        sheet.RangeUsed()!.Style.Font.FontSize = 10;
        sheet.Range(1, 1, noteRow + Math.Max(3, report.Warnings.Count), 8).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        return new SupplierSheetReference(
            sheet.Name,
            supplier.SupplierCode,
            supplier.SupplierName,
            supplier.CurrencyCode,
            "A7",
            "C7",
            "E7",
            "G7");
    }

    private static void BuildSummaryWorksheet(
        IXLWorksheet sheet,
        Tenant tenant,
        SupplierDetailedLedgerReportDto report,
        IReadOnlyList<SupplierSheetReference> supplierSheets,
        DateTime generatedAtUtc,
        string? generatedBy)
    {
        sheet.ShowGridLines = false;
        sheet.SheetView.FreezeRows(8);
        sheet.Range("A1:I1").Merge().Value = tenant.Name;
        StyleTitle(sheet.Range("A1:I1"), 15);
        sheet.Range("A2:I2").Merge().Value = "AP SUPPLIER STATEMENT EXPORT CONTROL SUMMARY";
        StyleTitle(sheet.Range("A2:I2"), 13);

        sheet.Cell("A4").Value = "Statement period";
        sheet.Cell("B4").Value = report.FromDate;
        sheet.Cell("B4").Style.DateFormat.Format = "dd mmm yyyy";
        sheet.Cell("C4").Value = "to";
        sheet.Cell("D4").Value = report.ToDate;
        sheet.Cell("D4").Style.DateFormat.Format = "dd mmm yyyy";
        sheet.Cell("E4").Value = "Display basis";
        sheet.Cell("F4").Value = report.ShowSupplierCurrency ? "Supplier currency" : report.CurrencyCode;
        sheet.Cell("G4").Value = "Generated UTC";
        sheet.Cell("H4").Value = generatedAtUtc;
        sheet.Cell("H4").Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
        sheet.Cell("I4").Value = Fallback(generatedBy, "System");

        sheet.Range("A6:B6").Merge().Value = "CONTROL STATUS";
        sheet.Range("A6:B6").Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE6F1");
        sheet.Range("A6:B6").Style.Font.Bold = true;
        sheet.Cell("C6").FormulaA1 = supplierSheets.Count == 0
            ? "=\"PASS\""
            : $"=IF(COUNTIF(I9:I{8 + supplierSheets.Count},\"FAIL\")=0,\"PASS\",\"FAIL\")";
        sheet.Cell("C6").Style.Font.Bold = true;

        var headers = new[] { "Supplier Code", "Supplier Name", "Currency", "Opening Balance", "Debits", "Credits", "Closing Balance", "Difference", "Check" };
        for (var index = 0; index < headers.Length; index++)
        {
            sheet.Cell(8, index + 1).Value = headers[index];
        }
        StyleTableHeader(sheet.Range("A8:I8"));

        var row = 9;
        foreach (var supplier in supplierSheets)
        {
            var escapedSheetName = supplier.SheetName.Replace("'", "''", StringComparison.Ordinal);
            sheet.Cell(row, 1).Value = supplier.SupplierCode;
            sheet.Cell(row, 2).Value = supplier.SupplierName;
            sheet.Cell(row, 3).Value = supplier.CurrencyCode;
            sheet.Cell(row, 4).FormulaA1 = $"='{escapedSheetName}'!{supplier.OpeningCell}";
            sheet.Cell(row, 5).FormulaA1 = $"='{escapedSheetName}'!{supplier.DebitCell}";
            sheet.Cell(row, 6).FormulaA1 = $"='{escapedSheetName}'!{supplier.CreditCell}";
            sheet.Cell(row, 7).FormulaA1 = $"='{escapedSheetName}'!{supplier.ClosingCell}";
            sheet.Cell(row, 8).FormulaA1 = $"=ROUND(D{row}+F{row}-E{row}-G{row},2)";
            sheet.Cell(row, 9).FormulaA1 = $"=IF(ABS(H{row})<=0.01,\"PASS\",\"FAIL\")";
            sheet.Range(row, 4, row, 8).Style.NumberFormat.Format = MoneyNumberFormat;
            row++;
        }

        if (supplierSheets.Count == 0)
        {
            sheet.Range("A9:I9").Merge().Value = "No supplier statement activity was found for the selected period.";
            sheet.Range("A9:I9").Style.Font.Italic = true;
            sheet.Range("A9:I9").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            row = 10;
        }
        else
        {
            sheet.Range(8, 1, row - 1, 9).SetAutoFilter();
        }

        var totalRow = row + 1;
        sheet.Cell(totalRow, 2).Value = "CONTROL TOTALS";
        if (supplierSheets.Count > 0)
        {
            sheet.Cell(totalRow, 4).FormulaA1 = $"=SUM(D9:D{row - 1})";
            sheet.Cell(totalRow, 5).FormulaA1 = $"=SUM(E9:E{row - 1})";
            sheet.Cell(totalRow, 6).FormulaA1 = $"=SUM(F9:F{row - 1})";
            sheet.Cell(totalRow, 7).FormulaA1 = $"=SUM(G9:G{row - 1})";
            sheet.Cell(totalRow, 8).FormulaA1 = $"=SUM(H9:H{row - 1})";
        }
        else
        {
            sheet.Range(totalRow, 4, totalRow, 8).Value = 0m;
        }
        sheet.Cell(totalRow, 9).FormulaA1 = $"=IF(ABS(H{totalRow})<=0.01,\"PASS\",\"FAIL\")";
        StyleStatementRow(sheet.Range(totalRow, 1, totalRow, 9), isTotal: true);
        sheet.Range(totalRow, 4, totalRow, 8).Style.NumberFormat.Format = MoneyNumberFormat;

        var noteRow = totalRow + 3;
        sheet.Range(noteRow, 1, noteRow, 9).Merge().Value =
            "Each supplier tab contains formula-driven running balances and a visible closing-balance reconciliation check.";
        sheet.Range(noteRow + 1, 1, noteRow + 1, 9).Merge().Value =
            "Source: Finance AP detailed ledger. Credits increase the payable; debits reduce it.";
        sheet.Range(noteRow, 1, noteRow + 1, 9).Style.Font.Italic = true;
        sheet.Range(noteRow, 1, noteRow + 1, 9).Style.Font.FontColor = XLColor.DimGray;

        var warningRow = noteRow + 3;
        foreach (var warning in report.Warnings)
        {
            sheet.Range(warningRow, 1, warningRow, 9).Merge().Value = $"Currency note: {warning}";
            sheet.Range(warningRow, 1, warningRow, 9).Style.Font.FontColor = XLColor.DarkOrange;
            sheet.Range(warningRow, 1, warningRow, 9).Style.Alignment.WrapText = true;
            warningRow++;
        }

        sheet.Column(1).Width = 18;
        sheet.Column(2).Width = 34;
        sheet.Column(3).Width = 13;
        sheet.Columns(4, 8).Width = 18;
        sheet.Column(9).Width = 18;
        sheet.Rows(1, 2).Height = 23;
        sheet.RangeUsed()!.Style.Font.FontName = "Aptos";
        sheet.RangeUsed()!.Style.Font.FontSize = 10;
        sheet.RangeUsed()!.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void StyleTitle(IXLRange range, double fontSize)
    {
        // Dark text remains legible in spreadsheet viewers that omit imported fill colours while
        // the restrained light-blue band still gives Excel users a clear TDC report hierarchy.
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE6F1");
        range.Style.Font.FontColor = XLColor.FromHtml("#17365D");
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = fontSize;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void StyleTableHeader(IXLRange range)
    {
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE6F1");
        range.Style.Font.FontColor = XLColor.FromHtml("#17365D");
        range.Style.Font.Bold = true;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
        range.Style.Border.BottomBorderColor = XLColor.FromHtml("#17365D");
    }

    private static void StyleStatementRow(IXLRange range, bool isTotal = false)
    {
        range.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
        range.Style.Border.BottomBorderColor = XLColor.LightGray;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        range.Columns(4, 5).Style.Alignment.WrapText = true;
        range.Columns(6, 8).Style.NumberFormat.Format = MoneyNumberFormat;
        range.Columns(6, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        range.Column(1).Style.DateFormat.Format = "dd mmm yyyy";

        if (isTotal)
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
            range.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            range.Style.Border.TopBorderColor = XLColor.Gray;
        }
    }

    private async Task RecordSuccessAuditAsync(
        Guid tenantId,
        string format,
        SupplierDetailedLedgerReportDto report,
        int contentLength,
        string outputHash,
        DateTime generatedAtUtc,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = FinanceAuditEvents.SupplierStatementExported,
            TenantId = tenantId,
            SourceModule = FinanceModule,
            SourceDocumentType = "SupplierStatement",
            Resource = "Finance.AP.SupplierStatement",
            ResourceId = $"{report.FromDate:yyyyMMdd}:{report.ToDate:yyyyMMdd}",
            AfterValues = new
            {
                Format = format.ToUpperInvariant(),
                report.FromDate,
                report.ToDate,
                report.ShowSupplierCurrency,
                SupplierCount = report.Suppliers.Count,
                LineCount = report.Suppliers.Sum(item => item.Lines.Count),
                report.TotalOpeningBalance,
                report.TotalDebits,
                report.TotalCredits,
                report.TotalClosingBalance,
                WarningCount = report.Warnings.Count,
                ContentLength = contentLength,
                OutputSha256 = outputHash,
                GeneratedAtUtc = generatedAtUtc
            },
            Comment = "Controlled AP supplier statement generated from the canonical detailed-ledger result."
        }, cancellationToken);
    }

    private async Task RecordFailureAuditAsync(
        Guid tenantId,
        DocumentRenderRequestDto request,
        Exception exception,
        DateTime generatedAtUtc,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = FinanceAuditEvents.ReportExportFailed,
            TenantId = tenantId,
            SourceModule = FinanceModule,
            SourceDocumentType = "SupplierStatement",
            Resource = "Finance.AP.SupplierStatement",
            ResourceId = DocumentType,
            AfterValues = new
            {
                request.Format,
                request.Options,
                Error = exception.Message,
                GeneratedAtUtc = generatedAtUtc
            },
            Comment = "Controlled AP supplier statement generation failed."
        }, cancellationToken);
    }

    private static string BuildFileStem(SupplierDetailedLedgerReportDto report)
    {
        var prefix = report.Suppliers.Count == 1 && !string.IsNullOrWhiteSpace(report.Suppliers[0].SupplierCode)
            ? $"supplier-statement-{SafeFileName(report.Suppliers[0].SupplierCode)}"
            : "supplier-statements";
        return $"{prefix}-{report.FromDate:yyyyMMdd}-{report.ToDate:yyyyMMdd}";
    }

    private static string UniqueWorksheetName(
        string? supplierCode,
        string supplierName,
        ISet<string> usedNames)
    {
        var preferred = !string.IsNullOrWhiteSpace(supplierCode) ? supplierCode : supplierName;
        var invalid = new HashSet<char>(['[', ']', ':', '*', '?', '/', '\\']);
        var sanitized = new string(preferred.Where(character => !invalid.Contains(character)).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "Supplier";
        }

        sanitized = sanitized[..Math.Min(31, sanitized.Length)];
        var candidate = sanitized;
        var suffix = 2;
        while (usedNames.Contains(candidate))
        {
            var suffixText = $"-{suffix++}";
            candidate = $"{sanitized[..Math.Min(31 - suffixText.Length, sanitized.Length)]}{suffixText}";
        }

        usedNames.Add(candidate);
        return candidate;
    }

    private static string SafeFileName(string value)
    {
        var result = value.Trim().ToLowerInvariant().Replace(' ', '-');
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(invalidCharacter, '-');
        }

        return result;
    }

    private static string Fallback(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string Money(decimal value)
        => value.ToString("N2", InvariantCulture);

    private static string MoneyOrBlank(decimal value)
        => value == 0m ? string.Empty : Money(value);

    private sealed record StatementParameters(
        DateTime FromDate,
        DateTime ToDate,
        IReadOnlyCollection<Guid> BusinessPartnerIds,
        bool ShowSupplierCurrency);

    private sealed record SupplierSheetReference(
        string SheetName,
        string SupplierCode,
        string SupplierName,
        string CurrencyCode,
        string OpeningCell,
        string DebitCell,
        string CreditCell,
        string ClosingCell);
}
