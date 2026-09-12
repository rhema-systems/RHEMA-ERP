using System.Security.Claims;
using System.Text.Json;
using ErpSystem.Api.Controllers.DocumentManagement;
using ErpSystem.Api.Services.DocumentManagement;
using ErpSystem.Api.Services.Notifications;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procedures;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.DocumentManagement;

public sealed class PaymentDocumentScopeTests : IDisposable
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _paymentId = Guid.NewGuid();
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
    private readonly Mock<ICurrentUserService> _current = new();
    private readonly Mock<IVendorPaymentService> _payments = new();
    private readonly Mock<IAuthorizationService> _authorization = new();
    private readonly Mock<IFileStorageService> _storage = new();
    private readonly Mock<ICentralDocumentRenditionService> _renditions = new();

    public PaymentDocumentScopeTests()
    {
        _current.SetupGet(item => item.TenantId).Returns(_tenant);
        _current.SetupGet(item => item.UserId).Returns(Guid.NewGuid().ToString());
        _current.SetupGet(item => item.UserName).Returns("reader");
        _current.Setup(item => item.IsInRole(It.IsAny<string>())).Returns(true);
        _authorization.Setup(item => item.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), FinancePermissions.ViewFinance))
            .ReturnsAsync(AuthorizationResult.Success());
        _payments.Setup(item => item.GetByIdAsync(_paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VendorPaymentDto { Id = _paymentId });
    }

    [Theory]
    [InlineData("Accounts Receivable")]
    [InlineData("Finance Officer")]
    [InlineData("Records Officer")]
    [InlineData("SuperAdmin")]
    public async Task Source_roles_and_DMS_administrators_do_not_bypass_Finance_permission(string role)
    {
        var record = await SeedRecord();
        _current.Setup(item => item.IsInRole(It.IsAny<string>())).Returns((string value) => value == role);
        _authorization.Setup(item => item.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), FinancePermissions.ViewFinance))
            .ReturnsAsync(AuthorizationResult.Failed());

        (await Controller().GetRecord(record.Id, CancellationToken.None)).Should().BeOfType<ForbidResult>();

        _payments.Verify(item => item.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Finance_permission_does_not_bypass_the_payment_owners_bank_read_scope()
    {
        var record = await SeedRecord();
        _payments.Setup(item => item.GetByIdAsync(_paymentId, It.IsAny<CancellationToken>())).ReturnsAsync((VendorPaymentDto?)null);

        (await Controller().GetRecord(record.Id, CancellationToken.None)).Should().BeOfType<ForbidResult>();
        var list = (await Controller().GetRecords(cancellationToken: CancellationToken.None)).Should().BeOfType<OkObjectResult>().Subject;
        JsonSerializer.Serialize(list.Value).Should().NotContain(record.Title);
    }

    [Fact]
    public async Task Scoped_reader_uses_exact_payment_and_request_context()
    {
        var record = await SeedRecord();
        using var cancellation = new CancellationTokenSource();
        var controller = Controller();

        (await controller.GetRecord(record.Id, cancellation.Token)).Should().BeOfType<OkObjectResult>();

        _authorization.Verify(item => item.AuthorizeAsync(controller.User, null, FinancePermissions.ViewFinance), Times.Once);
        _payments.Verify(item => item.GetByIdAsync(_paymentId, cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task Out_of_scope_record_and_version_content_never_open_physical_storage()
    {
        var record = await SeedRecord();
        var version = record.Versions.Single();
        _payments.Setup(item => item.GetByIdAsync(_paymentId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Outside bank scope."));
        var controller = Controller();

        (await controller.GetRecordContent(record.Id, CancellationToken.None)).Should().BeOfType<ForbidResult>();
        (await controller.GetVersionContent(record.Id, version.Id, CancellationToken.None)).Should().BeOfType<ForbidResult>();
        (await controller.DownloadVersionFile(record.Id, version.Id, "pdf", CancellationToken.None)).Should().BeOfType<ForbidResult>();

        _storage.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Foreign_tenant_record_is_not_found_before_payment_lookup()
    {
        var record = await SeedRecord(Guid.NewGuid());

        (await Controller().GetRecord(record.Id, CancellationToken.None)).Should().BeOfType<NotFoundObjectResult>();

        _payments.Verify(item => item.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Payment_source_with_missing_dependencies_fails_closed()
    {
        var record = await SeedRecord();

        (await Controller(includePaymentDependencies: false).GetRecord(record.Id, CancellationToken.None))
            .Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Other_document_sources_retain_existing_module_role_access()
    {
        var record = await SeedRecord();
        record.SourceEntityType = "VendorInvoice";
        record.AccessProfile = "Module restricted";
        await _db.SaveChangesAsync();

        (await Controller(includePaymentDependencies: false).GetRecord(record.Id, CancellationToken.None))
            .Should().BeOfType<OkObjectResult>();

        _payments.Verify(item => item.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Even_scoped_DMS_admin_cannot_replace_archive_annotate_or_republish_payment_documents()
    {
        var record = await SeedRecord();
        var version = record.Versions.Single();
        var controller = Controller();
        using var content = new MemoryStream([1, 2, 3]);
        var file = new FormFile(content, 0, content.Length, "file", "replacement.pdf");

        (await controller.UploadVersionFile(record.Id, file, "v2", "Submitted", null, null, CancellationToken.None))
            .Should().BeOfType<ForbidResult>();
        (await controller.UpdateVersionStatus(record.Id, version.Id, new("Current", null), CancellationToken.None))
            .Should().BeOfType<ForbidResult>();
        (await controller.UpdateMetadataValues(record.Id, new(null), CancellationToken.None)).Should().BeOfType<ForbidResult>();
        (await controller.AddAnnotationReview(record.Id, new(version.Id, "Review", "Closed", null, null, null, null, null), CancellationToken.None))
            .Should().BeOfType<ForbidResult>();
        (await controller.DeleteRecord(record.Id, CancellationToken.None)).Should().BeOfType<ForbidResult>();
        (await controller.UpdateGeneratedDocumentWorkflow(record.Id, new("approve", null, null, null, null, null), CancellationToken.None))
            .Should().BeOfType<ForbidResult>();

        record.VersionStatus.Should().Be("Submitted");
        record.IsDeleted.Should().BeFalse();
        _storage.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Persisted_payment_link_prevents_relabeling_from_unlocking_a_document()
    {
        var record = await SeedRecord();
        await AddPaymentLink(record);
        record.SourceEntityType = "Other";
        record.AccessProfile = "Module restricted";
        await _db.SaveChangesAsync();

        (await Controller().GetRecord(record.Id, CancellationToken.None)).Should().BeOfType<ForbidResult>();
        (await Controller().DeleteRecord(record.Id, CancellationToken.None)).Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Relinking_the_DMS_header_to_a_different_payment_is_not_a_read_grant()
    {
        var record = await SeedRecord();
        await AddPaymentLink(record);
        record.SourceRecordId = Guid.NewGuid();
        await _db.SaveChangesAsync();

        (await Controller().GetRecord(record.Id, CancellationToken.None)).Should().BeOfType<ForbidResult>();

        _payments.Verify(item => item.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Generic_source_handoff_cannot_create_or_rewrite_payment_owned_records()
    {
        var record = await SeedRecord();
        var controller = Controller();
        var direct = new SourceDocumentHandoffRequest("Replacement", "Finance", null, "VendorPayment", null,
            _paymentId, null, null, null, null);
        (await controller.RegisterSourceHandoff(direct, CancellationToken.None)).Should().BeOfType<ForbidResult>();
        // Omitting the type still resolves the existing source and must encounter the immutable owner guard.
        (await controller.RegisterSourceHandoff(direct with { SourceEntityType = null }, CancellationToken.None))
            .Should().BeOfType<ForbidResult>();
        record.Title.Should().Be("Private payment support");
    }

    [Theory]
    [InlineData("record")]
    [InlineData("version")]
    [InlineData("download")]
    public async Task Generic_payment_content_delegates_exact_version_to_the_validated_payment_owner(string route)
    {
        var record = await SeedRecord();
        var link = await AddPaymentLink(record);
        record.Versions.Single().RenditionPath = "uploads/untrusted-cached-rendition.pdf";
        await _db.SaveChangesAsync();
        var bytes = new MemoryStream([1, 2, 3]);
        _payments.Setup(item => item.OpenEvidenceAsync(_paymentId, link.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Content(link, bytes));
        using var cancellation = new CancellationTokenSource();

        var result = await ReadContent(Controller(), record, route, cancellation.Token);

        var file = result.Should().BeOfType<FileStreamResult>().Subject;
        file.FileStream.Should().BeSameAs(bytes);
        file.ContentType.Should().Be("application/pdf");
        _payments.Verify(item => item.OpenEvidenceAsync(_paymentId, link.Id, cancellation.Token), Times.Once);
        _storage.Invocations.Should().BeEmpty();
        _renditions.Invocations.Should().BeEmpty();
        await file.FileStream.DisposeAsync();
    }

    [Theory]
    [InlineData("record")]
    [InlineData("version")]
    [InlineData("download")]
    public async Task Unsafe_or_checksum_changed_payment_file_cannot_fall_back_to_generic_storage(string route)
    {
        var record = await SeedRecord();
        var link = await AddPaymentLink(record);
        record.RepositoryPath = record.Versions.Single().RepositoryPath = "uploads/tampered-original.pdf";
        record.Versions.Single().RenditionPath = "uploads/untrusted-cached-rendition.pdf";
        await _db.SaveChangesAsync();
        _payments.Setup(item => item.OpenEvidenceAsync(_paymentId, link.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Checksum or scan status changed."));

        var result = await ReadContent(Controller(), record, route, CancellationToken.None);

        var problem = result.Should().BeOfType<BadRequestObjectResult>().Subject.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be("AP_PAYMENT_EVIDENCE_UNAVAILABLE");
        _storage.Invocations.Should().BeEmpty();
        _renditions.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Unlinked_DMS_version_is_not_a_payment_attachment_download()
    {
        var record = await SeedRecord();
        await AddPaymentLink(record);
        var differentVersion = new CentralDocumentVersion { TenantId = _tenant, DocumentRecordId = record.Id,
            VersionNumber = "v2", FileName = "unlinked.pdf", ContentType = "application/pdf", RepositoryPath = "uploads/unlinked.pdf" };
        _db.CentralDocumentVersions.Add(differentVersion);
        await _db.SaveChangesAsync();

        (await Controller().GetVersionContent(record.Id, differentVersion.Id, CancellationToken.None))
            .Should().BeOfType<NotFoundObjectResult>();

        _payments.Verify(item => item.OpenEvidenceAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _storage.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Payment_PDF_conversion_does_not_create_or_open_an_unverified_rendition()
    {
        var record = await SeedRecord();
        var link = await AddPaymentLink(record);
        var bytes = new MemoryStream([1, 2, 3]);
        _payments.Setup(item => item.OpenEvidenceAsync(_paymentId, link.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Content(link, bytes, "proof.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"));

        (await Controller().DownloadVersionFile(record.Id, record.Versions.Single().Id, "pdf", CancellationToken.None))
            .Should().BeOfType<BadRequestObjectResult>();

        bytes.CanRead.Should().BeFalse();
        _storage.Invocations.Should().BeEmpty();
        _renditions.Invocations.Should().BeEmpty();
        record.Versions.Single().RenditionPath.Should().BeNull();
    }

    [Fact]
    public async Task Original_Word_attachment_can_be_downloaded_only_after_payment_validation()
    {
        var record = await SeedRecord();
        var link = await AddPaymentLink(record);
        var bytes = new MemoryStream([1, 2, 3]);
        const string wordType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        _payments.Setup(item => item.OpenEvidenceAsync(_paymentId, link.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Content(link, bytes, "proof.docx", wordType));

        var result = await Controller().DownloadVersionFile(record.Id, record.Versions.Single().Id, "word", CancellationToken.None);

        var file = result.Should().BeOfType<FileStreamResult>().Subject;
        file.ContentType.Should().Be(wordType);
        file.FileDownloadName.Should().Be("proof.docx");
        _storage.Invocations.Should().BeEmpty();
        _renditions.Invocations.Should().BeEmpty();
        await file.FileStream.DisposeAsync();
    }

    [Fact]
    public async Task Nonpayment_content_retains_its_existing_DMS_rendition_path()
    {
        var record = await SeedRecord();
        record.SourceEntityType = "VendorInvoice";
        record.AccessProfile = "Module restricted";
        record.Versions.Single().RenditionPath = "uploads/ordinary.pdf";
        await _db.SaveChangesAsync();
        var bytes = new MemoryStream([1, 2, 3]);
        _storage.Setup(item => item.DownloadFileAsync(It.IsAny<string>(), Guid.Empty)).ReturnsAsync(bytes);

        var result = await Controller().GetRecordContent(record.Id, CancellationToken.None);

        var file = result.Should().BeOfType<FileStreamResult>().Subject;
        file.FileStream.Should().BeSameAs(bytes);
        _payments.Verify(item => item.OpenEvidenceAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _storage.Verify(item => item.DownloadFileAsync(It.IsAny<string>(), Guid.Empty), Times.Once);
        await file.FileStream.DisposeAsync();
    }

    private static Task<IActionResult> ReadContent(DocumentManagementController controller, CentralDocumentRecord record,
        string route, CancellationToken ct) => route switch
        {
            "record" => controller.GetRecordContent(record.Id, ct),
            "version" => controller.GetVersionContent(record.Id, record.Versions.Single().Id, ct),
            _ => controller.DownloadVersionFile(record.Id, record.Versions.Single().Id, "pdf", ct)
        };

    private static CentralDocumentRepositoryContent Content(VendorPaymentEvidenceLink link, Stream stream,
        string fileName = "proof.pdf", string contentType = "application/pdf") => new()
        {
            Content = stream, FileName = fileName, ContentType = contentType, FileSize = 3,
            UploadRecord = new FileUploadRecord { Id = link.FileUploadRecordId, TenantId = link.TenantId }
        };

    private async Task<CentralDocumentRecord> SeedRecord(Guid? tenant = null)
    {
        var record = new CentralDocumentRecord
        {
            TenantId = tenant ?? _tenant, DocumentReference = "DMS-PAYMENT-TEST", Title = "Private payment support",
            SourceModule = "Finance", SourceLabel = "AP payment evidence", SourceEntityType = nameof(VendorPayment),
            SourceRecordId = _paymentId, SourceRecordReference = "VP-1", AccessProfile = "Finance AP payment restricted",
            CurrentVersion = "v1.0", VersionStatus = "Submitted", LifecycleStatus = "Active"
        };
        record.Versions.Add(new CentralDocumentVersion { TenantId = record.TenantId, DocumentRecordId = record.Id,
            VersionNumber = "v1.0", Status = "Submitted", FileName = "proof.pdf", ContentType = "application/pdf" });
        _db.CentralDocumentRecords.Add(record);
        await _db.SaveChangesAsync();
        return record;
    }

    private async Task<VendorPaymentEvidenceLink> AddPaymentLink(CentralDocumentRecord record)
    {
        var uploadId = Guid.NewGuid();
        record.Versions.Single().FileUploadRecordId = uploadId;
        var link = new VendorPaymentEvidenceLink
        {
            TenantId = _tenant, VendorPaymentId = _paymentId, CentralDocumentRecordId = record.Id,
            CentralDocumentVersionId = record.Versions.Single().Id, FileUploadRecordId = uploadId,
            ClientRequestId = Guid.NewGuid(), RequirementKey = "payment-support", FileName = "proof.pdf", ContentType = "application/pdf",
            FileSize = 3, ChecksumSha256 = new string('a', 64), RequestHash = new string('b', 64)
        };
        _db.Set<VendorPaymentEvidenceLink>().Add(link);
        await _db.SaveChangesAsync();
        return link;
    }

    private DocumentManagementController Controller(bool includePaymentDependencies = true)
        => new(Mock.Of<ICentralDocumentManagementService>(), _db, _current.Object, _storage.Object,
            Mock.Of<IControlledFileUploadService>(), _renditions.Object,
            Mock.Of<ICentralDocumentPdfSigningService>(), Mock.Of<INotificationService>(), Mock.Of<IProcedureCaseService>(),
            NullLogger<DocumentManagementController>.Instance,
            includePaymentDependencies ? _payments.Object : null, includePaymentDependencies ? _authorization.Object : null)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, _current.Object.UserId!)], "test"))
            } }
        };

    public void Dispose() => _db.Dispose();
}
