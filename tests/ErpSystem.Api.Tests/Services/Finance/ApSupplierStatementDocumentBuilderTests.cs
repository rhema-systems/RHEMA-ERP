using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ErpSystem.Api.Controllers;
using ErpSystem.Api.Services.Documents.Finance;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class ApSupplierStatementDocumentBuilderTests
{
    private static readonly DateTime FromDate = new(2026, 7, 1);
    private static readonly DateTime ToDate = new(2026, 7, 31);

    [Fact]
    [Trait("Batch", "FinanceGoLive-ApSupplierStatement")]
    public async Task RenderPdf_ShouldUseCanonicalDetailedLedgerAndAuditEmittedBytes()
    {
        var fixture = await CreateFixtureAsync();
        var request = Request("pdf", fixture.SupplierId);

        var result = await fixture.Builder.RenderAsync(request);

        result.Content.Should().StartWith(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF
        result.ContentType.Should().Be("application/pdf");
        result.FileName.Should().Be("supplier-statement-sup-001-20260701-20260731.pdf");
        result.DocumentType.Should().Be(DocumentTypes.FinanceApSupplierStatement);
        fixture.ApReports.Verify(service => service.GetSupplierDetailedLedgerAsync(
            FromDate,
            ToDate,
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { fixture.SupplierId })),
            false,
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Audit.Events.Should().ContainSingle(item =>
            item.EventType == FinanceAuditEvents.SupplierStatementExported
            && item.TenantId == fixture.TenantId
            && item.Resource == "Finance.AP.SupplierStatement");

        var qaOutputPath = Environment.GetEnvironmentVariable("TDC_AP_STATEMENT_QA_PDF");
        if (!string.IsNullOrWhiteSpace(qaOutputPath))
        {
            // Opt-in artifact output supports the PDF render-and-visual-inspection gate without
            // leaving files behind during normal CI runs.
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(qaOutputPath))!);
            await File.WriteAllBytesAsync(qaOutputPath, result.Content);
        }
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ApSupplierStatement")]
    public async Task RenderXlsx_ShouldCreateNativeTypedWorkbookWithLiveBalanceChecks()
    {
        var fixture = await CreateFixtureAsync();

        var result = await fixture.Builder.RenderAsync(Request("xlsx", fixture.SupplierId));

        result.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        result.FileName.Should().Be("supplier-statement-sup-001-20260701-20260731.xlsx");
        using var stream = new MemoryStream(result.Content);
        using var workbook = SpreadsheetDocument.Open(stream, false);
        var workbookPart = workbook.WorkbookPart!;
        var sheets = workbookPart.Workbook.Sheets!.Elements<Sheet>().ToList();
        sheets.Select(sheet => sheet.Name!.Value).Should().BeEquivalentTo("Summary", "SUP-001");

        var summary = GetWorksheetPart(workbookPart, sheets.Single(sheet => sheet.Name == "Summary"));
        CellText(workbookPart, Cell(summary, "A1")).Should().Be("Tema Development Corporation");
        CellText(workbookPart, Cell(summary, "A2")).Should().Contain("CONTROL SUMMARY");
        Cell(summary, "H9").CellFormula!.Text.Should().Contain("D9+F9-E9-G9");
        Cell(summary, "I9").CellFormula!.Text.Should().Contain("PASS");

        var statement = GetWorksheetPart(workbookPart, sheets.Single(sheet => sheet.Name == "SUP-001"));
        CellText(workbookPart, Cell(statement, "B3")).Should().Be("TDC Works Supplier");
        Cell(statement, "B4").StyleIndex.Should().NotBeNull("the real date cell must carry an explicit date format");
        Cell(statement, "F11").DataType.Should().BeNull("numeric amounts must not be exported as text");
        Cell(statement, "H10").CellFormula!.Text.Should().Contain("$A$7");
        Cell(statement, "H11").CellFormula!.Text.Should().Contain("H10-F11+G11");
        statement.Worksheet.Descendants<AutoFilter>().Should().ContainSingle();
        Cell(statement, "H17").CellFormula!.Text.Should().Contain("PASS");

        fixture.Audit.Events.Should().ContainSingle(item =>
            item.EventType == FinanceAuditEvents.SupplierStatementExported
            && item.Resource == "Finance.AP.SupplierStatement");

        var qaOutputPath = Environment.GetEnvironmentVariable("TDC_AP_STATEMENT_QA_XLSX");
        if (!string.IsNullOrWhiteSpace(qaOutputPath))
        {
            // The spreadsheet skill inspects and renders this opt-in native workbook after the
            // production builder has generated it; the test itself does not author a second file.
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(qaOutputPath))!);
            await File.WriteAllBytesAsync(qaOutputPath, result.Content);
        }
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ApSupplierStatement")]
    public async Task Render_ShouldRejectInvalidPeriodAndRetainFailureAudit()
    {
        var fixture = await CreateFixtureAsync();
        var request = Request("pdf", fixture.SupplierId);
        request.Options!["fromDate"] = "2026-08-01";
        request.Options["toDate"] = "2026-07-31";

        var action = () => fixture.Builder.RenderAsync(request);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*end date must be on or after the start date*");
        fixture.ApReports.Verify(service => service.GetSupplierDetailedLedgerAsync(
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>(),
            It.IsAny<IReadOnlyCollection<Guid>>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.Audit.Events.Should().ContainSingle(item =>
            item.EventType == FinanceAuditEvents.ReportExportFailed
            && item.Resource == "Finance.AP.SupplierStatement");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-ApSupplierStatement")]
    public void SupplierStatementDocumentType_ShouldUseFinanceReportExportPolicy()
    {
        DocumentsController.FinanceDocumentPolicies[DocumentTypes.FinanceApSupplierStatement]
            .Should().Be(FinancePermissions.ExportFinanceReports);
    }

    private static DocumentRenderRequestDto Request(string format, Guid supplierId) => new()
    {
        DocumentType = DocumentTypes.FinanceApSupplierStatement,
        Format = format,
        Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fromDate"] = "2026-07-01",
            ["toDate"] = "2026-07-31",
            ["businessPartnerIds"] = supplierId.ToString(),
            ["showSupplierCurrency"] = "false"
        }
    };

    private static async Task<TestFixture> CreateFixtureAsync()
    {
        var tenantId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var context = CreateContext();
        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Tema Development Corporation",
            Code = "TDC",
            BaseCurrency = "GHS",
            Address = "Tema, Ghana"
        });
        await context.SaveChangesAsync();

        var report = BuildReport(supplierId);
        var apReports = new Mock<IApReportsService>(MockBehavior.Strict);
        apReports.Setup(service => service.GetSupplierDetailedLedgerAsync(
                FromDate,
                ToDate,
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(supplierId)),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        var audit = new CapturingFinanceAuditService();
        var currentUser = new Mock<ICurrentUserService>(MockBehavior.Strict);
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.UserName).Returns("finance.officer");

        return new TestFixture(
            tenantId,
            supplierId,
            context,
            apReports,
            audit,
            new ApSupplierStatementDocumentBuilder(apReports.Object, context, currentUser.Object, audit));
    }

    private static SupplierDetailedLedgerReportDto BuildReport(Guid supplierId) => new()
    {
        FromDate = FromDate,
        ToDate = ToDate,
        CurrencyCode = "GHS",
        TotalOpeningBalance = 25m,
        TotalDebits = 35m,
        TotalCredits = 100m,
        TotalClosingBalance = 90m,
        Suppliers =
        {
            new SupplierDetailedLedgerAccountDto
            {
                BusinessPartnerId = supplierId,
                SupplierCode = "SUP-001",
                SupplierName = "TDC Works Supplier",
                CurrencyCode = "GHS",
                OpeningBalance = 25m,
                TotalDebits = 35m,
                TotalCredits = 100m,
                ClosingBalance = 90m,
                Lines =
                {
                    new SupplierDetailedLedgerLineDto
                    {
                        SourceDocumentId = Guid.NewGuid(),
                        TransactionDate = new DateTime(2026, 7, 8),
                        TransactionType = "Invoice",
                        DocumentNumber = "AP-INV-001",
                        Reference = "SUP-INV-447",
                        Description = "Road maintenance materials",
                        TransactionCurrencyCode = "GHS",
                        ExchangeRate = 1m,
                        Credit = 100m,
                        RunningBalance = 125m
                    },
                    new SupplierDetailedLedgerLineDto
                    {
                        SourceDocumentId = Guid.NewGuid(),
                        TransactionDate = new DateTime(2026, 7, 24),
                        TransactionType = "Payment",
                        DocumentNumber = "AP-PAY-001",
                        Reference = "TDC-BANK-778",
                        Description = "Supplier payment - Bank Transfer",
                        TransactionCurrencyCode = "GHS",
                        ExchangeRate = 1m,
                        Debit = 30m,
                        RunningBalance = 95m
                    },
                    new SupplierDetailedLedgerLineDto
                    {
                        SourceDocumentId = Guid.NewGuid(),
                        TransactionDate = new DateTime(2026, 7, 24),
                        TransactionType = "Withholding Tax",
                        DocumentNumber = "AP-PAY-001",
                        Reference = "WHT-2026-001",
                        Description = "Withholding tax on supplier payment",
                        TransactionCurrencyCode = "GHS",
                        ExchangeRate = 1m,
                        Debit = 5m,
                        RunningBalance = 90m
                    }
                }
            }
        }
    };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ap-supplier-statement-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static WorksheetPart GetWorksheetPart(WorkbookPart workbookPart, Sheet sheet)
        => (WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!);

    private static Cell Cell(WorksheetPart worksheetPart, string reference)
        => worksheetPart.Worksheet.Descendants<Cell>()
            .Single(cell => string.Equals(cell.CellReference?.Value, reference, StringComparison.OrdinalIgnoreCase));

    private static string CellText(WorkbookPart workbookPart, Cell cell)
    {
        if (cell.DataType?.Value == CellValues.SharedString)
        {
            var index = int.Parse(cell.CellValue!.Text, System.Globalization.CultureInfo.InvariantCulture);
            return workbookPart.SharedStringTablePart!.SharedStringTable.Elements<SharedStringItem>().ElementAt(index).InnerText;
        }

        return cell.InlineString?.InnerText ?? cell.CellValue?.Text ?? string.Empty;
    }

    private sealed record TestFixture(
        Guid TenantId,
        Guid SupplierId,
        ApplicationDbContext Context,
        Mock<IApReportsService> ApReports,
        CapturingFinanceAuditService Audit,
        ApSupplierStatementDocumentBuilder Builder);

    private sealed class CapturingFinanceAuditService : IFinanceAuditService
    {
        public List<FinanceAuditEventDto> Events { get; } = new();

        public Task<AuditLog> RecordAsync(
            FinanceAuditEventDto auditEvent,
            CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.FromResult(new AuditLog { Id = Guid.NewGuid() });
        }

        public Task<IReadOnlyList<AuditLog>> GetAuditTrailAsync(
            Guid tenantId,
            string resource,
            string resourceId,
            int limit = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditLog>>(Array.Empty<AuditLog>());
    }
}
