using ErpSystem.Api.Controllers;
using ErpSystem.Api.Services.Documents.Finance;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
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

public sealed class ManagementReportDocumentBuilderTests
{
    [Fact]
    public async Task CashPosition_ShouldLabelCurrencyGroupsAsFunctionalBaseEquivalents()
    {
        var (context, currentUser) = await CreateContextAsync();
        var reports = new Mock<ICashPositionReportService>(MockBehavior.Strict);
        reports.Setup(service => service.GetCurrentPositionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CashPositionSummaryDto
            {
                AsOfDate = new DateTime(2025, 1, 31),
                Currency = "GHS",
                TotalBalance = 1_250_000m,
                AccountCount = 2,
                ByCurrency =
                {
                    new CashPositionByCurrencyDto { Currency = "GHS", Count = 1, Balance = 750_000m },
                    new CashPositionByCurrencyDto { Currency = "USD", Count = 1, Balance = 500_000m }
                }
            });
        var builder = new CashPositionReportDocumentBuilder(reports.Object, context, currentUser.Object);

        var result = await builder.RenderAsync(Request(DocumentTypes.FinanceCashPositionReport));

        AssertPdf(result, "cash-position-2025-01-31.pdf", "Current posted cash and bank balances", "GHS",
            "Balance (GHS base eq.)", "USD", "500,000.00");
        var qaOutputPath = Environment.GetEnvironmentVariable("TDC_MANAGEMENT_REPORT_QA_PDF");
        if (!string.IsNullOrWhiteSpace(qaOutputPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(qaOutputPath))!);
            await File.WriteAllBytesAsync(qaOutputPath, result.Content);
        }
        reports.VerifyAll();
    }

    [Fact]
    public async Task TaxDocuments_ShouldUseCanonicalSnapshotAndWithholdingServices()
    {
        var (context, currentUser) = await CreateContextAsync();
        var settings = new Mock<ITenantSettingsService>(MockBehavior.Strict);
        settings.Setup(service => service.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
        var reports = new Mock<ITaxReportingService>(MockBehavior.Strict);
        reports.Setup(service => service.GetInputTaxReportAsync(
                It.Is<TaxReportRequestDto>(filter =>
                    filter.FromDate == new DateTime(2025, 1, 1) && filter.ToDate == new DateTime(2025, 1, 31)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GhanaTaxSnapshotReportDto
            {
                FromDate = new DateTime(2025, 1, 1),
                ToDate = new DateTime(2025, 1, 31),
                Totals = new TaxReportTotalsDto { TaxableAmount = 100_000m, TotalTaxAmount = 20_000m },
                Lines =
                {
                    new GhanaTaxSnapshotLineDto
                    {
                        SourceDocumentDate = new DateTime(2025, 1, 15),
                        SourceDocumentType = "VendorInvoice",
                        SourceDocumentNumber = "VI-2025-001",
                        CounterpartyName = "Tema Engineering Services Ltd",
                        TaxCode = "VAT",
                        TaxName = "Value Added Tax",
                        BaseAmount = 100_000m,
                        TaxableAmount = 100_000m,
                        TaxRate = 15m,
                        TaxAmount = 15_000m,
                        TaxAccountNumber = "2200"
                    }
                }
            });
        reports.Setup(service => service.GetWhtPayableReportAsync(
                It.IsAny<TaxReportRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GhanaTaxWithholdingReportDto
            {
                FromDate = new DateTime(2025, 1, 1),
                ToDate = new DateTime(2025, 1, 31),
                Totals = new TaxReportTotalsDto { TaxableBase = 50_000m, TotalWithholdingAmount = 3_750m },
                Lines =
                {
                    new GhanaTaxWithholdingLineDto
                    {
                        SourceDocumentDate = new DateTime(2025, 1, 20),
                        SourceDocumentType = "VendorPayment",
                        SourceDocumentNumber = "VP-001",
                        CounterpartyName = "Tema Engineering Services Ltd",
                        TaxCode = "WHT-SERVICES",
                        TaxRate = 7.5m,
                        TaxableBase = 50_000m,
                        WithholdingAmount = 3_750m,
                        CertificateStatus = "Issued",
                        CertificateNumber = "WHT-001"
                    }
                }
            });

        var request = Request(DocumentTypes.FinanceTaxInputRegister,
            ("fromDate", "2025-01-01"), ("toDate", "2025-01-31"));
        var input = await new InputTaxRegisterDocumentBuilder(
                reports.Object, context, currentUser.Object, settings.Object)
            .RenderAsync(request);
        var wht = await new WhtPayableReportDocumentBuilder(
                reports.Object, context, currentUser.Object, settings.Object)
            .RenderAsync(Request(DocumentTypes.FinanceTaxWhtPayable,
                ("fromDate", "2025-01-01"), ("toDate", "2025-01-31")));

        AssertPdf(input, "input-tax-register-2025-01-01-2025-01-31.pdf", "Input Tax Register", "VI-2025-001");
        AssertPdf(wht, "wht-payable-2025-01-01-2025-01-31.pdf", "Withholding Tax Payable", "WHT-001");
        reports.VerifyAll();
        settings.VerifyAll();
    }

    [Fact]
    public async Task WhtRegisters_ShouldPreserveCertificateRemittanceAndCurrencyEvidence()
    {
        var (context, currentUser) = await CreateContextAsync();
        var settings = new Mock<ITenantSettingsService>(MockBehavior.Strict);
        var certificates = new Mock<IWithholdingTaxCertificateService>(MockBehavior.Strict);
        certificates.Setup(service => service.GetApCertificatesAsync(
                It.Is<WhtCertificateQueryDto>(query =>
                    query.Page == 1 && query.PageSize == 200
                    && query.FromDate == new DateTime(2025, 1, 1)
                    && query.ToDate == new DateTime(2025, 1, 31)
                    && query.Status == "Issued" && query.SearchTerm == "Tema"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<WhtCertificateDto>
            {
                Page = 1,
                PageSize = 200,
                TotalCount = 1,
                Items = new[]
                {
                    new WhtCertificateDto
                    {
                        VendorPaymentId = Guid.NewGuid(),
                        PaymentNumber = "VP-2025-001",
                        SupplierName = "Tema Engineering Services Ltd",
                        SupplierTin = "C000123",
                        PaymentDate = new DateTime(2025, 1, 20),
                        CurrencyCode = "USD",
                        TaxCode = "WHT-SERVICES",
                        TaxRate = 7.5m,
                        TaxableBase = 10_000m,
                        WithholdingAmount = 750m,
                        CertificateNumber = "WHT-2025-001",
                        CertificateStatus = "Issued",
                        VersionNumber = 1,
                        RemittanceNumber = "WHT-REM-2025-001",
                        RemittanceStatus = "Paid"
                    }
                }
            });
        certificates.Setup(service => service.GetRemittancesAsync(
                It.Is<WhtRemittanceQueryDto>(query =>
                    query.Page == 1 && query.PageSize == 200
                    && query.FromDate == new DateTime(2025, 1, 1)
                    && query.ToDate == new DateTime(2025, 1, 31)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<WhtRemittanceDto>
            {
                Page = 1,
                PageSize = 200,
                TotalCount = 1,
                Items = new[]
                {
                    new WhtRemittanceDto
                    {
                        Id = Guid.NewGuid(),
                        RemittanceNumber = "WHT-REM-2025-001",
                        PeriodFrom = new DateTime(2025, 1, 1),
                        PeriodTo = new DateTime(2025, 1, 31),
                        DueDate = new DateTime(2025, 2, 15),
                        CurrencyCode = "USD",
                        Status = "Paid",
                        TotalWithholdingAmount = 750m,
                        LineCount = 1,
                        SubmissionReference = "GRA-SUB-001",
                        PaymentReference = "PAY-001",
                        AuthorityReceiptReference = "GRA-RCT-001",
                        Lines =
                        {
                            new WhtRemittanceLineDto
                            {
                                Id = Guid.NewGuid(),
                                VendorPaymentId = Guid.NewGuid(),
                                PaymentNumber = "VP-2025-001",
                                PaymentDate = new DateTime(2025, 1, 20),
                                BusinessPartnerId = Guid.NewGuid(),
                                SupplierName = "Tema Engineering Services Ltd",
                                SupplierTin = "C000123",
                                TaxCode = "WHT-SERVICES",
                                TaxableBase = 10_000m,
                                WithholdingAmount = 750m
                            }
                        }
                    }
                }
            });

        var certificateResult = await new WhtCertificateRegisterDocumentBuilder(
                certificates.Object, context, currentUser.Object, settings.Object)
            .RenderAsync(Request(DocumentTypes.FinanceTaxWhtCertificateRegister,
                ("fromDate", "2025-01-01"), ("toDate", "2025-01-31"),
                ("status", "Issued"), ("searchTerm", "Tema")));
        var remittanceResult = await new WhtRemittanceRegisterDocumentBuilder(
                certificates.Object, context, currentUser.Object, settings.Object)
            .RenderAsync(Request(DocumentTypes.FinanceTaxWhtRemittanceRegister,
                ("fromDate", "2025-01-01"), ("toDate", "2025-01-31")));

        AssertPdf(certificateResult,
            "wht-statutory-certificate-register-2025-01-01-2025-01-31.pdf",
            "USD", "WHT-2025-001", "WHT-REM-2025-001");
        AssertPdf(remittanceResult,
            "wht-remittance-register-2025-01-01-2025-01-31.pdf",
            "USD", "GRA-SUB-001", "GRA-RCT-001", "VP-2025-001");

        var qaOutputPath = Environment.GetEnvironmentVariable("TDC_WHT_REGISTER_QA_PDF");
        if (!string.IsNullOrWhiteSpace(qaOutputPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(qaOutputPath))!);
            await File.WriteAllBytesAsync(qaOutputPath, remittanceResult.Content);
        }
        certificates.VerifyAll();
    }

    [Fact]
    public async Task ConsolidatedBudget_ShouldPreserveScenarioScopeAndValidationEvidence()
    {
        var scenarioId = Guid.NewGuid();
        var (context, currentUser) = await CreateContextAsync();
        var budgets = new Mock<IBudgetService>(MockBehavior.Strict);
        budgets.Setup(service => service.GetConsolidatedViewAsync(scenarioId, true))
            .ReturnsAsync(new ConsolidatedBudgetViewDto
            {
                ScenarioId = scenarioId,
                ScenarioName = "FY2025 Adopted Budget",
                FiscalYearName = "FY2025",
                ScenarioStatus = "Approved",
                ApprovedOnly = true,
                CurrencyCode = "GHS",
                BookClassification = "IFRS",
                TotalReturnCount = 1,
                IncludedReturnCount = 1,
                ApprovedReturnCount = 1,
                ReadyForSubmission = true,
                TotalExpenseBudget = 120_000m,
                TotalExpenseActual = 100_000m,
                NetBudget = -120_000m,
                NetActual = -100_000m,
                Lines = new[]
                {
                    new BudgetReportLineDto
                    {
                        AccountCode = "6000",
                        AccountName = "Operating Expense",
                        AccountType = "Expense",
                        PeriodCode = "2025-01",
                        BudgetAmount = 120_000m,
                        ActualAmount = 100_000m,
                        VarianceAmount = -20_000m,
                        Favorability = "Favorable"
                    }
                }
            });
        var builder = new ConsolidatedBudgetDocumentBuilder(budgets.Object, context, currentUser.Object);

        var result = await builder.RenderAsync(Request(DocumentTypes.FinanceBudgetConsolidated,
            ("scenarioId", scenarioId.ToString()), ("approvedOnly", "true")));

        AssertPdf(result, "consolidated-budget-fy2025-adopted-budget-approved.pdf",
            "Consolidated Budget", "FY2025 Adopted Budget", "6000", "120,000.00", "Approved returns only");
        budgets.VerifyAll();
    }

    [Fact]
    public async Task BudgetScenarioComparison_ShouldPreserveBothApprovedScenarioIdentities()
    {
        var baseScenarioId = Guid.NewGuid();
        var comparisonScenarioId = Guid.NewGuid();
        var (context, currentUser) = await CreateContextAsync();
        var budgets = new Mock<IBudgetService>(MockBehavior.Strict);
        budgets.Setup(service => service.CompareScenariosAsync(baseScenarioId, comparisonScenarioId))
            .ReturnsAsync(new BudgetScenarioComparisonDto
            {
                BaseScenarioId = baseScenarioId,
                BaseScenarioName = "FY2025 Adopted Budget",
                ComparisonScenarioId = comparisonScenarioId,
                ComparisonScenarioName = "FY2025 Revised Budget",
                CurrencyCode = "GHS",
                BaseTotal = 120_000m,
                ComparisonTotal = 135_000m,
                DifferenceTotal = 15_000m,
                Lines = new[]
                {
                    new BudgetScenarioComparisonLineDto
                    {
                        AccountCode = "6000",
                        AccountName = "Operating Expense",
                        AccountType = "Expense",
                        PeriodCode = "2025-01",
                        BaseAmount = 120_000m,
                        ComparisonAmount = 135_000m,
                        DifferenceAmount = 15_000m,
                        DifferencePercent = 12.5m
                    }
                }
            });
        var builder = new BudgetScenarioComparisonDocumentBuilder(budgets.Object, context, currentUser.Object);

        var result = await builder.RenderAsync(Request(DocumentTypes.FinanceBudgetScenarioComparison,
            ("baseScenarioId", baseScenarioId.ToString()),
            ("comparisonScenarioId", comparisonScenarioId.ToString())));

        AssertPdf(result,
            "budget-scenario-comparison-fy2025-adopted-budget-versus-fy2025-revised-budget.pdf",
            "Budget Scenario Comparison", "FY2025 Adopted Budget", "FY2025 Revised Budget", "15,000.00", "12.5%");
        budgets.VerifyAll();
    }

    [Fact]
    public void ManagementReportTypes_ShouldRequireFinanceExportPermission()
    {
        var documentTypes = new[]
        {
            DocumentTypes.FinanceCashPositionReport,
            DocumentTypes.FinanceTaxInputRegister,
            DocumentTypes.FinanceTaxOutputRegister,
            DocumentTypes.FinanceTaxVatReconciliation,
            DocumentTypes.FinanceTaxWhtPayable,
            DocumentTypes.FinanceTaxWhtCertificateRegister,
            DocumentTypes.FinanceTaxWhtRemittanceRegister,
            DocumentTypes.FinanceBudgetConsolidated,
            DocumentTypes.FinanceBudgetScenarioComparison
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
            .UseInMemoryDatabase($"management-report-documents-{Guid.NewGuid():N}")
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
