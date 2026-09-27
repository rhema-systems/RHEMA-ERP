using ErpSystem.Api.Services.Documents.Finance;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using FluentAssertions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class BaseDeltaReportDocumentBuilderTests
{
    [Fact]
    [Trait("Category", "ReportingExport")]
    public async Task RenderAsync_ProducesGovernedBaseDeltaPdfFromSelectedDeltaLayer()
    {
        var deltaId = Guid.NewGuid();
        var books = new Mock<IAccountingBookService>();
        books.Setup(service => service.GetDeltaCombinedReportAsync(
                deltaId, new DateTime(2026, 9, 21), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeltaBookCombinedReportDto
            {
                DeltaAccountingBookId = deltaId,
                DeltaAccountingBookCode = "IFRS_CONSOL_ADJ",
                BaseAccountingBookId = Guid.NewGuid(),
                BaseAccountingBookCode = "IFRS",
                FunctionalCurrencyCode = "GHS",
                AsOfDate = new DateTime(2026, 9, 21),
                BaseTotal = 0m,
                DeltaTotal = 0m,
                CombinedTotal = 0m,
                Lines = new[]
                {
                    new DeltaBookCombinedReportLineDto
                    {
                        AccountId = Guid.NewGuid(), AccountNumber = "1200", AccountName = "Accounts Receivable",
                        AccountType = "Asset", BaseSignedBalance = 150_000m,
                        DeltaSignedBalance = -50_000m, CombinedSignedBalance = 100_000m
                    }
                }
            });
        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(service => service.GetCompanyNameAsync()).ReturnsAsync("Rhema Test Company");
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.UserName).Returns("controller");
        var builder = new BaseDeltaReportDocumentBuilder(books.Object, tenantSettings.Object, currentUser.Object);

        var result = await builder.RenderAsync(new DocumentRenderRequestDto
        {
            DocumentType = DocumentTypes.FinanceBaseDeltaReport,
            Format = "pdf",
            Options = new Dictionary<string, string>
            {
                ["deltaAccountingBookId"] = deltaId.ToString(),
                ["asOfDate"] = "2026-09-21"
            }
        });

        result.Content.Should().NotBeEmpty();
        result.ContentType.Should().Be("application/pdf");
        result.FileName.Should().Contain("ifrs-ifrs_consol_adj-20260921");
        var text = ExtractPdfText(result.Content);
        text.Should().Contain("Base + Delta report");
        text.Should().Contain("IFRS_CONSOL_ADJ");
        text.Should().Contain("Accounts Receivable");
        text.Should().Contain("150,000.00 Dr");
        text.Should().Contain("50,000.00 Cr");
        text.Should().Contain("100,000.00 Dr");
    }

    private static string ExtractPdfText(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var reader = new PdfReader(stream);
        using var document = new PdfDocument(reader);
        return string.Join("\n", Enumerable.Range(1, document.GetNumberOfPages())
            .Select(page => PdfTextExtractor.GetTextFromPage(document.GetPage(page))));
    }
}
