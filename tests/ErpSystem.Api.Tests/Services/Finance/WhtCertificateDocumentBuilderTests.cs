using ErpSystem.Api.Controllers;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Documents.Finance;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Models;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class WhtCertificateDocumentBuilderTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-WhtRetainedCertificates")]
    public async Task Issue_ShouldRetainExactOriginalAndReasonBackedReplacement()
    {
        var fixture = await CreateFixtureAsync();
        await using var db = fixture.Context;
        var issueService = new FinanceControlledDocumentIssueService(
            db, fixture.CurrentUser.Object, fixture.Audit, fixture.Storage.Object);
        var builder = new WhtCertificateDocumentBuilder(db, fixture.CurrentUser.Object, issueService);

        var original = await builder.RenderAsync(Request(fixture.Certificate.Id, ControlledDocumentCopyTypes.Original));

        original.Content.Should().StartWith([0x25, 0x50, 0x44, 0x46]);
        original.FileName.Should().Be("WHT-2025-0001-v1-original.pdf");
        ExtractText(original.Content).Should().Contain("WITHHOLDING TAX CERTIFICATE")
            .And.Contain("Tema Engineering Services Ltd")
            .And.Contain("GHS 15,000.00");
        var summary = await issueService.GetSummaryAsync(DocumentTypes.FinanceTaxWhtCertificate, fixture.Certificate.Id);
        summary.OriginalIssued.Should().BeTrue();
        summary.Issues.Should().ContainSingle(item => item.Retained);
        summary.Issues.Single().RetainUntilUtc.Should().BeAfter(DateTime.UtcNow.AddYears(6));
        var retainedOriginal = await issueService.GetRetainedAsync(summary.Issues.Single().Id);
        retainedOriginal.Content.Should().Equal(original.Content);

        var duplicateOriginal = () => builder.RenderAsync(Request(fixture.Certificate.Id, ControlledDocumentCopyTypes.Original));
        await duplicateOriginal.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*original has already been issued*");

        const string reason = "Supplier requested a replacement after losing the original.";
        var replacement = await builder.RenderAsync(Request(
            fixture.Certificate.Id,
            ControlledDocumentCopyTypes.Replacement,
            reason));
        ExtractText(replacement.Content).Should().Contain("REPLACEMENT COPY").And.Contain(reason);
        var issues = await db.FinanceControlledDocumentIssues.AsNoTracking()
            .OrderBy(item => item.CopyNumber).ToListAsync();
        issues.Should().HaveCount(2);
        issues[1].CopyType.Should().Be(ControlledDocumentCopyTypes.Replacement);
        issues[1].ReplacementReason.Should().Be(reason);
        issues.Should().OnlyContain(item => item.StoragePath != null && item.FileSize > 0 && item.RetainUntilUtc != null);
        fixture.Audit.Events.Should().Contain(item => item.EventType == FinanceAuditEvents.WithholdingCertificatePdfIssued);
        fixture.Audit.Events.Should().Contain(item => item.EventType == FinanceAuditEvents.WithholdingCertificatePdfReplacementIssued);

        // Later statutory lifecycle changes do not rewrite or hide bytes that were actually
        // issued while this immutable certificate version was current.
        var certificateToCancel = await db.WithholdingTaxCertificates.SingleAsync(item => item.Id == fixture.Certificate.Id);
        certificateToCancel.Status = WhtCertificateStatus.Cancelled;
        await db.SaveChangesAsync();
        (await issueService.GetRetainedAsync(summary.Issues.Single().Id)).Content.Should().Equal(original.Content);

        var qaOutput = Environment.GetEnvironmentVariable("TDC_WHT_CERTIFICATE_QA_OUTPUT");
        if (!string.IsNullOrWhiteSpace(qaOutput))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(qaOutput))!);
            await File.WriteAllBytesAsync(qaOutput, original.Content);
        }
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-WhtRetainedCertificates")]
    public async Task Retrieval_ShouldRejectTamperedPrivateBytes()
    {
        var fixture = await CreateFixtureAsync();
        await using var db = fixture.Context;
        var issueService = new FinanceControlledDocumentIssueService(
            db, fixture.CurrentUser.Object, fixture.Audit, fixture.Storage.Object);
        var builder = new WhtCertificateDocumentBuilder(db, fixture.CurrentUser.Object, issueService);
        await builder.RenderAsync(Request(fixture.Certificate.Id, ControlledDocumentCopyTypes.Original));
        var issue = await db.FinanceControlledDocumentIssues.AsNoTracking().SingleAsync();

        fixture.Stored[issue.StoragePath!] = "tampered"u8.ToArray();
        var retrieve = () => issueService.GetRetainedAsync(issue.Id);
        await retrieve.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SHA-256 integrity check*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-WhtRetainedCertificates")]
    public async Task Issue_ShouldFailClosedForNonIssuedOrUnpostedCertificate()
    {
        var fixture = await CreateFixtureAsync();
        await using var db = fixture.Context;
        var issueService = new FinanceControlledDocumentIssueService(
            db, fixture.CurrentUser.Object, fixture.Audit, fixture.Storage.Object);
        var builder = new WhtCertificateDocumentBuilder(db, fixture.CurrentUser.Object, issueService);
        fixture.Certificate.Status = WhtCertificateStatus.Cancelled;
        await db.SaveChangesAsync();

        var render = () => builder.RenderAsync(Request(fixture.Certificate.Id, ControlledDocumentCopyTypes.Original));
        await render.Should().ThrowAsync<InvalidOperationException>().WithMessage("Only the currently issued*");
        (await db.FinanceControlledDocumentIssues.CountAsync()).Should().Be(0);
        fixture.Stored.Should().BeEmpty();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-WhtRetainedCertificates")]
    public void DocumentType_ShouldRequireTaxLifecyclePermission()
        => DocumentsController.FinanceDocumentPolicies[DocumentTypes.FinanceTaxWhtCertificate]
            .Should().Be(FinancePermissions.ManageTaxConfiguration);

    [Fact]
    [Trait("Batch", "FinanceGoLive-WhtRetainedCertificates")]
    public void LegacyBrowserPrint_ShouldFailClosedWithControlledPdfDirection()
    {
        var controller = new WithholdingTaxCertificatesController(Mock.Of<IWithholdingTaxCertificateService>());

#pragma warning disable CS0618 // The test proves the legacy endpoint remains deliberately retired.
        var result = controller.GetApCertificatePrintView(Guid.NewGuid(), null, CancellationToken.None);
#pragma warning restore CS0618

        var gone = result.Should().BeOfType<ObjectResult>().Subject;
        gone.StatusCode.Should().Be(StatusCodes.Status410Gone);
        gone.Value.Should().BeOfType<ProblemDetails>().Which.Detail.Should().Contain("retained controlled WHT certificate PDF");
    }

    private static DocumentRenderRequestDto Request(Guid certificateId, string copyType, string? reason = null)
        => new()
        {
            DocumentType = DocumentTypes.FinanceTaxWhtCertificate,
            EntityId = certificateId,
            Format = "pdf",
            CopyType = copyType,
            Options = reason == null ? null : new Dictionary<string, string> { ["replacementReason"] = reason }
        };

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"wht-controlled-pdf-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var journal = new JournalEntry
        {
            Id = Guid.NewGuid(), TenantId = tenantId, JournalEntryNumber = "JE-WHT-2025-001",
            JournalType = "AP Payment", EntryDate = new DateTime(2025, 1, 15),
            Description = "Posted supplier payment", PostingStatus = "Posted", IsBalanced = true,
            TotalDebitAmount = 150_000m, TotalCreditAmount = 150_000m,
            BookClassification = "IFRS", FiscalPeriodId = Guid.NewGuid()
        };
        var certificate = new WithholdingTaxCertificate
        {
            Id = Guid.NewGuid(), TenantId = tenantId, VendorPaymentId = Guid.NewGuid(),
            CertificateNumber = "WHT-2025-0001", VersionNumber = 1, Status = WhtCertificateStatus.Issued,
            IssueDate = new DateTime(2025, 1, 15), IssuedAtUtc = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc),
            IssuedByName = "Ama Tax Controller", PaymentNumber = "VP-2025-0001",
            SupplierId = Guid.NewGuid(), SupplierName = "Tema Engineering Services Ltd", SupplierTin = "C0001234567",
            PaymentDate = new DateTime(2025, 1, 15), CurrencyCode = "GHS", TaxCode = "WHT-SERV",
            TaxName = "Withholding Tax - Services", TaxRate = 10m, TaxableBase = 150_000m,
            WithholdingAmount = 15_000m, NetPaidAmount = 135_000m,
            TaxAccountNumber = "2205", TaxAccountName = "WHT Payable", JournalEntryId = journal.Id
        };
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Finance Demonstration & Mastery", Code = "FINANCE-DEMO", BaseCurrency = "GHS" });
        db.Users.Add(new ApplicationUser
        {
            Id = userId, TenantId = tenantId, UserName = "finance.demo.controller",
            NormalizedUserName = "FINANCE.DEMO.CONTROLLER", FirstName = "Ama", LastName = "Controller"
        });
        db.JournalEntries.Add(journal);
        db.WithholdingTaxCertificates.Add(certificate);
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.UserId).Returns(userId.ToString());
        currentUser.SetupGet(service => service.UserName).Returns("finance.demo.controller");
        var stored = new Dictionary<string, byte[]>();
        var storage = new Mock<IFileStorageService>();
        storage.Setup(service => service.UploadFileAsync(It.IsAny<FileUploadRequest>()))
            .ReturnsAsync((FileUploadRequest request) =>
            {
                using var buffer = new MemoryStream();
                request.FileStream.CopyTo(buffer);
                var path = $"private/{Guid.NewGuid():N}/{request.FileName}";
                stored[path] = buffer.ToArray();
                return new FileStorageResult
                {
                    Success = true, FileName = request.FileName, OriginalFileName = request.FileName,
                    FilePath = path, PublicUrl = string.Empty, FileSize = request.FileSize,
                    ContentType = request.ContentType, Category = request.Category,
                    TenantId = request.TenantId, StorageProvider = "TestPrivateStorage"
                };
            });
        storage.Setup(service => service.DownloadFileAsync(It.IsAny<string>(), It.IsAny<Guid>()))
            .ReturnsAsync((string path, Guid _) => new MemoryStream(stored[path], writable: false));
        storage.Setup(service => service.DeleteFileAsync(It.IsAny<string>()))
            .ReturnsAsync((string path) => stored.Remove(path));
        return new Fixture(db, currentUser, storage, stored, new CapturingAuditService(), certificate);
    }

    private static string ExtractText(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var reader = new PdfReader(stream);
        using var pdf = new PdfDocument(reader);
        return string.Join(" ", Enumerable.Range(1, pdf.GetNumberOfPages())
            .Select(page => PdfTextExtractor.GetTextFromPage(pdf.GetPage(page))));
    }

    private sealed record Fixture(
        ApplicationDbContext Context,
        Mock<ICurrentUserService> CurrentUser,
        Mock<IFileStorageService> Storage,
        Dictionary<string, byte[]> Stored,
        CapturingAuditService Audit,
        WithholdingTaxCertificate Certificate);

    private sealed class CapturingAuditService : ErpSystem.Core.Interfaces.Finance.IFinanceAuditService
    {
        public List<ErpSystem.Core.DTOs.Finance.FinanceAuditEventDto> Events { get; } = [];
        public Task<AuditLog> RecordAsync(ErpSystem.Core.DTOs.Finance.FinanceAuditEventDto auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.FromResult(new AuditLog { Id = Guid.NewGuid() });
        }
        public Task<IReadOnlyList<AuditLog>> GetAuditTrailAsync(Guid tenantId, string resource, string resourceId, int limit = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditLog>>([]);
    }
}
