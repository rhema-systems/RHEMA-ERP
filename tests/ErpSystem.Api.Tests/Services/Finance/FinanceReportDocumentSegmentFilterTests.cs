using ErpSystem.Api.Services.Documents.Finance;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using FluentAssertions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceReportDocumentSegmentFilterTests
{
    [Fact]
    [Trait("Category", "ReportingExport")]
    public async Task TrialBalanceDocument_UsesStructuredSegmentFilters()
    {
        var fundId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var ledger = new Mock<IGeneralLedgerService>();
        ledger
            .Setup(service => service.GenerateTrialBalanceAsync(It.IsAny<TrialBalanceRequestDto>()))
            .ReturnsAsync(new TrialBalanceDto
            {
                CompanyName = "Test Company",
                AsAtDate = new DateTime(2026, 7, 20),
                BookClassification = "IFRS",
                CurrencyCode = "GHS"
            });

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.UserName).Returns("report-tester");
        var builder = new TrialBalanceDocumentBuilder(ledger.Object, currentUser.Object);

        var result = await builder.RenderAsync(new DocumentRenderRequestDto
        {
            DocumentType = DocumentTypes.FinanceTrialBalance,
            Format = "pdf",
            Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["segmentFilters[0].segmentStructureId"] = fundId.ToString(),
                ["segmentFilters[0].segmentCode"] = "FUND",
                ["segmentFilters[0].segmentPosition"] = "1",
                ["segmentFilters[0].segmentValue"] = "001",
                ["segmentFilters[1].segmentStructureId"] = departmentId.ToString(),
                ["segmentFilters[1].segmentCode"] = "DEPT",
                ["segmentFilters[1].segmentPosition"] = "2",
                ["segmentFilters[1].segmentValue"] = "500"
            }
        });

        result.Content.Should().NotBeEmpty();
        ledger.Verify(service => service.GenerateTrialBalanceAsync(It.Is<TrialBalanceRequestDto>(request =>
            request.SegmentFilters.Count == 2
            && request.SegmentFilters[0].SegmentStructureId == fundId
            && request.SegmentFilters[0].SegmentCode == "FUND"
            && request.SegmentFilters[0].SegmentPosition == 1
            && request.SegmentFilters[0].SegmentValue == "001"
            && request.SegmentFilters[1].SegmentStructureId == departmentId
            && request.SegmentFilters[1].SegmentCode == "DEPT"
            && request.SegmentFilters[1].SegmentPosition == 2
            && request.SegmentFilters[1].SegmentValue == "500")), Times.Once);
    }

    [Fact]
    [Trait("Category", "ReportingExport")]
    public async Task CashFlowDocument_ForwardsMethodAndRetainsPresentationWarnings()
    {
        const string warning = "Review the indirect-method residual before final sign-off.";
        var ledger = new Mock<IGeneralLedgerService>();
        ledger
            .Setup(service => service.GenerateCashFlowStatementAsync(It.IsAny<CashFlowStatementRequestDto>()))
            .ReturnsAsync(new CashFlowStatementDto
            {
                CompanyName = "Test Company",
                PeriodStart = new DateTime(2026, 1, 1),
                PeriodEnd = new DateTime(2026, 7, 31),
                BookClassification = "IFRS",
                CurrencyCode = "GHS",
                Method = "Indirect",
                PresentationWarnings = { warning },
                OperatingActivities = new CashFlowSectionDto
                {
                    SectionName = "Cash Flows from Operating Activities (Indirect Method)"
                },
                InvestingActivities = new CashFlowSectionDto
                {
                    SectionName = "Cash Flows from Investing Activities"
                },
                FinancingActivities = new CashFlowSectionDto
                {
                    SectionName = "Cash Flows from Financing Activities"
                }
            });

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.UserName).Returns("report-tester");
        var builder = new CashFlowStatementDocumentBuilder(ledger.Object, currentUser.Object);

        var result = await builder.RenderAsync(new DocumentRenderRequestDto
        {
            DocumentType = DocumentTypes.FinanceCashFlowStatement,
            Format = "pdf",
            Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["periodStart"] = "2026-01-01",
                ["periodEnd"] = "2026-07-31",
                ["bookClassification"] = "IFRS",
                ["method"] = "Indirect"
            }
        });

        result.Content.Should().NotBeEmpty();
        ExtractPdfText(result.Content)
            .Should().Contain("Method: Indirect")
            .And.Contain("indirect-method residual");
        ledger.Verify(service => service.GenerateCashFlowStatementAsync(It.Is<CashFlowStatementRequestDto>(request =>
            request.PeriodStart == new DateTime(2026, 1, 1)
            && request.PeriodEnd == new DateTime(2026, 7, 31)
            && request.BookClassification == "IFRS"
            && request.Method == "Indirect")), Times.Once);
    }

    private static string ExtractPdfText(byte[] content)
    {
        using var input = new MemoryStream(content);
        using var reader = new PdfReader(input);
        using var document = new PdfDocument(reader);
        return string.Join(
            " ",
            Enumerable.Range(1, document.GetNumberOfPages())
                .Select(page => PdfTextExtractor.GetTextFromPage(document.GetPage(page))));
    }
}
