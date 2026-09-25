using ErpSystem.Api.Controllers;
using ErpSystem.Api.Services.Documents.Finance;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class SubledgerReportDocumentBuilderTests
{
    private static readonly DateTime AsOfDate = new(2025, 1, 31);

    [Fact]
    public async Task ArAging_ShouldRenderCanonicalDatedReportAsPdf()
    {
        var (context, currentUser) = await CreateContextAsync();
        var reports = new Mock<IArReportsService>(MockBehavior.Strict);
        reports.Setup(service => service.GetAgingReportAsync(
                AsOfDate, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgingReportDto
            {
                AsOfDate = AsOfDate,
                CurrencyCode = "GHS",
                Summary = new AgingSummaryDto
                {
                    TotalCustomers = 1,
                    OverdueCustomers = 1,
                    GrandTotal = 100_000m
                },
                Customers =
                {
                    new CustomerAgingDto
                    {
                        CustomerCode = "CUS-001",
                        CustomerName = "Meridian Property Holdings Ltd",
                        Days1To30 = 100_000m,
                        TotalOutstanding = 100_000m
                    }
                }
            });
        var audit = new Mock<IFinanceAuditService>(MockBehavior.Strict);
        audit.Setup(service => service.RecordAsync(
                It.Is<FinanceAuditEventDto>(item =>
                    item.EventType == FinanceAuditEvents.ReportExported
                    && item.Resource == DocumentTypes.FinanceArAgingReport),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditLog());
        var builder = new ArAgingReportDocumentBuilder(reports.Object, context, currentUser.Object, audit.Object);

        var result = await builder.RenderAsync(Request(DocumentTypes.FinanceArAgingReport,
            ("asOfDate", "2025-01-31")));

        AssertPdf(result, "ar-aging-2025-01-31.pdf", "AR Aging Analysis", "Meridian Property Holdings Ltd");
        var qaOutputPath = Environment.GetEnvironmentVariable("TDC_SUBLEDGER_REPORT_QA_PDF");
        if (!string.IsNullOrWhiteSpace(qaOutputPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(qaOutputPath))!);
            await File.WriteAllBytesAsync(qaOutputPath, result.Content);
        }
        reports.VerifyAll();
        audit.VerifyAll();
    }

    [Fact]
    public async Task ArCustomerStatement_ShouldPreserveCustomerFilterAndCurrencyPresentation()
    {
        var customerId = Guid.NewGuid();
        var (context, currentUser) = await CreateContextAsync();
        var reports = new Mock<IArReportsService>(MockBehavior.Strict);
        reports.Setup(service => service.GetCustomerDetailedLedgerAsync(
                new DateTime(2025, 1, 1),
                new DateTime(2025, 1, 31),
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { customerId })),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerDetailedLedgerReportDto
            {
                FromDate = new DateTime(2025, 1, 1),
                ToDate = new DateTime(2025, 1, 31),
                ShowCustomerCurrency = true,
                Customers =
                {
                    new CustomerDetailedLedgerAccountDto
                    {
                        BusinessPartnerId = customerId,
                        CustomerCode = "CUS-USD",
                        CustomerName = "Atlantic Development Partners Ltd",
                        CurrencyCode = "USD",
                        ClosingBalance = 70_000m,
                        Lines =
                        {
                            new CustomerDetailedLedgerLineDto
                            {
                                TransactionDate = new DateTime(2025, 1, 1),
                                TransactionType = "Opening Invoice",
                                DocumentNumber = "INV-USD-001",
                                Description = "Approved customer cutover schedule",
                                TransactionCurrencyCode = "USD",
                                Debit = 70_000m,
                                RunningBalance = 70_000m
                            }
                        }
                    }
                }
            });
        var builder = new ArCustomerStatementDocumentBuilder(reports.Object, context, currentUser.Object);

        var result = await builder.RenderAsync(Request(DocumentTypes.FinanceArCustomerStatement,
            ("fromDate", "2025-01-01"),
            ("toDate", "2025-01-31"),
            ("businessPartnerIds", customerId.ToString()),
            ("showCustomerCurrency", "true")));

        AssertPdf(result, "customer-statement-cus-usd-2025-01-01-2025-01-31.pdf",
            "Customer Statement", "Development Partners Ltd", "CUS-USD", "USD");
        reports.VerifyAll();
    }

    [Fact]
    public async Task ApAgingAndCashRequirements_ShouldUseCanonicalApReportService()
    {
        var (context, currentUser) = await CreateContextAsync();
        var reports = new Mock<IApReportsService>(MockBehavior.Strict);
        reports.Setup(service => service.GetAgingReportAsync(
                AsOfDate, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApAgingReportDto
            {
                AsOfDate = AsOfDate,
                CurrencyCode = "GHS",
                TotalSuppliers = 1,
                TotalInvoices = 2,
                TotalOutstanding = 220_000m,
                SupplierDetails =
                {
                    new SupplierAgingDetailDto
                    {
                        SupplierCode = "SUP-001",
                        SupplierName = "Tema Engineering Services Ltd",
                        Current = 220_000m,
                        TotalOutstanding = 220_000m,
                        InvoiceCount = 2
                    }
                }
            });
        reports.Setup(service => service.GetCashRequirementForecastAsync(
                AsOfDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CashRequirementForecastDto
            {
                AsOfDate = AsOfDate,
                CurrencyCode = "GHS",
                TotalPayable = 220_000m,
                Periods =
                {
                    new CashRequirementPeriodDto
                    {
                        Period = "Overdue",
                        PeriodStart = new DateTime(2025, 1, 1),
                        PeriodEnd = AsOfDate,
                        InvoiceCount = 2,
                        AmountDue = 220_000m
                    }
                }
            });

        var aging = await new ApAgingReportDocumentBuilder(reports.Object, context, currentUser.Object)
            .RenderAsync(Request(DocumentTypes.FinanceApAgingReport, ("asOfDate", "2025-01-31")));
        var cash = await new ApCashRequirementsDocumentBuilder(reports.Object, context, currentUser.Object)
            .RenderAsync(Request(DocumentTypes.FinanceApCashRequirements, ("asOfDate", "2025-01-31")));

        AssertPdf(aging, "ap-aging-2025-01-31.pdf", "AP Aging Analysis", "Tema Engineering Services Ltd");
        AssertPdf(cash, "ap-cash-requirements-2025-01-31.pdf", "AP Cash Requirements Forecast", "Overdue");
        reports.VerifyAll();
    }

    [Fact]
    public async Task ApControlledRegisters_ShouldRenderMatchAndProcurementReconciliationReadModels()
    {
        var (context, currentUser) = await CreateContextAsync();
        var reports = new Mock<IApReportsService>(MockBehavior.Strict);
        reports.Setup(service => service.GetThreeWayMatchExceptionsAsync(
                new DateTime(2025, 1, 1),
                new DateTime(2025, 1, 31),
                VendorInvoiceMatchExceptionStatus.Approved,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VendorInvoiceMatchExceptionReportDto
            {
                FromDate = new DateTime(2025, 1, 1),
                ToDate = new DateTime(2025, 1, 31),
                TotalCount = 1,
                ApprovedCount = 1,
                Rows = new[]
                {
                    new VendorInvoiceMatchExceptionReportRowDto
                    {
                        InvoiceNumber = "VI-2025-001",
                        SupplierName = "Tema Engineering Services Ltd",
                        PurchaseOrderNumber = "PO-2025-001",
                        Status = VendorInvoiceMatchExceptionStatus.Approved,
                        VarianceType = "Price",
                        MaximumVariancePercentage = 2m,
                        RootCauseCategory = "Price variance",
                        RootCauseDescription = "Approved contract adjustment",
                        CorrectiveActionOwnerName = "AP Manager",
                        CorrectiveActionStatus = VendorInvoiceMatchCorrectiveActionStatus.Completed,
                        CorrectiveActionDueAtUtc = AsOfDate,
                        EvidenceCount = 2
                    }
                }
            });
        reports.Setup(service => service.GetProcurementFinanceReconciliationAsync(
                AsOfDate, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementFinanceReconciliationReportDto
            {
                AsOfDate = AsOfDate,
                RuleCode = "AP-005",
                TaskCode = "TDC-0508",
                IsReconciled = true,
                PurchaseOrderCount = 1,
                Rows = new[]
                {
                    new ProcurementFinanceReconciliationRowDto
                    {
                        PurchaseOrderNumber = "PO-2025-001",
                        PurchaseOrderStatus = "Completed",
                        CurrencyCode = "GHS",
                        PurchaseOrderAmount = 150_000m,
                        CommitmentAmount = 150_000m,
                        AcceptedReceiptAmount = 150_000m,
                        InvoiceAmount = 150_000m,
                        SettledAmount = 150_000m,
                        InvoicePostedAmount = 150_000m,
                        PaymentPostedAmount = 150_000m,
                        IsReconciled = true
                    }
                }
            });

        var exceptions = await new ApMatchExceptionReportDocumentBuilder(reports.Object, context, currentUser.Object)
            .RenderAsync(Request(DocumentTypes.FinanceApMatchExceptionReport,
                ("fromDate", "2025-01-01"), ("toDate", "2025-01-31"), ("status", "Approved")));
        var reconciliation = await new ApProcurementReconciliationDocumentBuilder(reports.Object, context, currentUser.Object)
            .RenderAsync(Request(DocumentTypes.FinanceApProcurementReconciliation, ("asOfDate", "2025-01-31")));

        AssertPdf(exceptions, "ap-match-exceptions-2025-01-01-2025-01-31.pdf", "VI-2025-001", "Approved");
        AssertPdf(reconciliation, "procurement-finance-reconciliation-2025-01-31.pdf", "PO-2025-001", "Reconciled");
        reports.VerifyAll();
    }

    [Fact]
    public void NewReportTypes_ShouldRequireFinanceReportExportPermission()
    {
        var documentTypes = new[]
        {
            DocumentTypes.FinanceApAgingReport,
            DocumentTypes.FinanceApCashRequirements,
            DocumentTypes.FinanceApMatchExceptionReport,
            DocumentTypes.FinanceApProcurementReconciliation,
            DocumentTypes.FinanceArAgingReport,
            DocumentTypes.FinanceArCustomerStatement
        };

        documentTypes.Should().OnlyContain(documentType =>
            DocumentsController.FinanceDocumentPolicies[documentType] == FinancePermissions.ExportFinanceReports);
    }

    private static DocumentRenderRequestDto Request(string documentType, params (string Key, string Value)[] options) => new()
    {
        DocumentType = documentType,
        Format = "pdf",
        Options = options.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase)
    };

    private static void AssertPdf(RenderedDocumentDto result, string expectedFileName, params string[] expectedText)
    {
        result.Content.Should().StartWith(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        result.ContentType.Should().Be("application/pdf");
        result.FileName.Should().Be(expectedFileName);
        var text = ExtractPdfText(result.Content);
        foreach (var value in expectedText)
            text.Should().Contain(value);
    }

    private static string ExtractPdfText(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var reader = new PdfReader(stream);
        using var pdf = new PdfDocument(reader);
        return string.Join("\n", Enumerable.Range(1, pdf.GetNumberOfPages())
            .Select(page => PdfTextExtractor.GetTextFromPage(pdf.GetPage(page))));
    }

    private static async Task<(ApplicationDbContext Context, Mock<ICurrentUserService> CurrentUser)> CreateContextAsync()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"subledger-report-documents-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var context = new ApplicationDbContext(options);
        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Code = "FINANCE-DEMO",
            Name = "Finance Demonstration & Mastery",
            BaseCurrency = "GHS"
        });
        await context.SaveChangesAsync();
        var currentUser = new Mock<ICurrentUserService>(MockBehavior.Strict);
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.UserName).Returns("finance.demo.auditor");
        return (context, currentUser);
    }
}
