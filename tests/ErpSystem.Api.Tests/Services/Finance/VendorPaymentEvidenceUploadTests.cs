using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed partial class ApPaymentPostingMigrationTests
{
    [Fact]
    public async Task PaymentEvidence_UploadRegistersPaymentOwnedDmsDocumentAndIsIdempotent()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedOptionalPaymentDraftAsync(db, tenant);
        var storage = new PaymentEvidenceStorageFixture(db);
        var (service, _) = CreateService(db, tenant, workflowService: OptionalPaymentWorkflow().Object,
            approvalPolicyResolver: OptionalPaymentPolicy(RequiredPaymentEvidencePolicy()).Object,
            useRealPaymentSod: true, paymentEvidenceFiles: storage.Documents.Object,
            paymentEvidenceUploader: storage.Uploader.Object);
        var request = PaymentEvidenceUploadRequest();

        var uploaded = await service.UploadEvidenceAsync(fixture.Payment.Id, request);
        var replay = await service.UploadEvidenceAsync(fixture.Payment.Id, request);

        replay.Id.Should().Be(uploaded.Id);
        uploaded.VerificationStatus.Should().Be("NotRequired");
        uploaded.VerifiedById.Should().BeNull();
        var link = await db.Set<VendorPaymentEvidenceLink>().SingleAsync();
        link.VendorPaymentId.Should().Be(fixture.Payment.Id);
        link.TenantId.Should().Be(tenant);
        link.ClientRequestId.Should().Be(request.ClientRequestId);
        link.ChecksumSha256.Should().Be(Convert.ToHexString(SHA256.HashData(request.Content)));
        storage.Uploader.Verify(item => item.UploadAsync(It.Is<ControlledFileUploadRequest>(upload =>
            upload.TenantId == tenant && upload.ActorUserId != Guid.Empty &&
            upload.Category == ControlledFileUploadCategories.DocumentManagement &&
            upload.FileName == request.FileName && upload.FileSize == request.Content.LongLength),
            It.IsAny<CancellationToken>()), Times.Once());
        storage.Documents.Verify(item => item.RegisterAsync(It.Is<CentralDocumentRepositoryRegistration>(registration =>
            registration.TenantId == tenant && registration.SourceModule == "Finance" &&
            registration.SourceEntityType == "VendorPayment" && registration.SourceRecordId == fixture.Payment.Id &&
            registration.VersionStatus == "Submitted" && !registration.RequirePublishedGovernance),
            It.IsAny<CancellationToken>()), Times.Once());

        await using var content = await service.OpenEvidenceAsync(fixture.Payment.Id, uploaded.Id);
        content.Should().NotBeNull();
        using var downloaded = new MemoryStream();
        await content!.Content.CopyToAsync(downloaded);
        downloaded.ToArray().Should().Equal(request.Content);
        (await service.GetControlAsync(fixture.Payment.Id))!.CanSubmit.Should().BeTrue();
        await service.SubmitAsync(fixture.Payment.Id, new SubmitVendorPaymentDto());
        var posted = await service.PostAsync(fixture.Payment.Id);
        posted.Status.Should().Be(VendorPaymentStatus.Processed);
        posted.AuthorizedById.Should().BeNull();
        posted.WorkflowInstanceId.Should().BeNull();
        storage.Uploader.Verify(item => item.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never());
    }

    [Theory]
    [InlineData("wrong-policy")]
    [InlineData("active-workflow")]
    [InlineData("retained-instance")]
    [InlineData("infected")]
    [InlineData("skipped-scan")]
    [InlineData("expired")]
    public async Task PaymentEvidence_UploadRejectsWrongRouteOrUnsafeContent(string scenario)
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedOptionalPaymentDraftAsync(db, tenant);
        var storage = new PaymentEvidenceStorageFixture(db)
        {
            ScanStatus = scenario == "infected" ? FileVirusScanStatus.Infected
                : scenario == "skipped-scan" ? FileVirusScanStatus.Skipped : FileVirusScanStatus.Clean
        };
        var (service, _) = CreateService(db, tenant,
            workflowService: OptionalPaymentWorkflow(scenario == "active-workflow", scenario == "retained-instance").Object,
            approvalPolicyResolver: OptionalPaymentPolicy(RequiredPaymentEvidencePolicy()).Object,
            paymentEvidenceFiles: storage.Documents.Object, paymentEvidenceUploader: storage.Uploader.Object);
        var request = PaymentEvidenceUploadRequest();
        if (scenario == "wrong-policy") request.RequirementKey = "unconfigured-document";
        if (scenario == "expired") request.ExpiryDate = DateTime.UtcNow.AddDays(-1);

        await ((Func<Task>)(async () => await service.UploadEvidenceAsync(fixture.Payment.Id, request)))
            .Should().ThrowAsync<InvalidOperationException>();

        (await db.Set<VendorPaymentEvidenceLink>().CountAsync()).Should().Be(0);
        (await db.Set<CentralDocumentRecord>().CountAsync()).Should().Be(0);
        var unsafeUpload = scenario is "infected" or "skipped-scan";
        storage.Uploader.Verify(item => item.UploadAsync(It.IsAny<ControlledFileUploadRequest>(), It.IsAny<CancellationToken>()),
            unsafeUpload ? Times.Once() : Times.Never());
        storage.Uploader.Verify(item => item.DeleteAsync(tenant, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            unsafeUpload ? Times.Once() : Times.Never());
        storage.Documents.Verify(item => item.RegisterAsync(It.IsAny<CentralDocumentRepositoryRegistration>(),
            It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task PaymentEvidence_UploadRequestCannotBeReusedWithChangedContent()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedOptionalPaymentDraftAsync(db, tenant);
        var storage = new PaymentEvidenceStorageFixture(db);
        var (service, _) = CreateService(db, tenant, workflowService: OptionalPaymentWorkflow().Object,
            approvalPolicyResolver: OptionalPaymentPolicy(RequiredPaymentEvidencePolicy()).Object,
            paymentEvidenceFiles: storage.Documents.Object, paymentEvidenceUploader: storage.Uploader.Object);
        var request = PaymentEvidenceUploadRequest();
        var first = await service.UploadEvidenceAsync(fixture.Payment.Id, request);
        request.Content = Encoding.UTF8.GetBytes("Different bank instructions must not replace retained evidence silently.");

        await ((Func<Task>)(async () => await service.UploadEvidenceAsync(fixture.Payment.Id, request)))
            .Should().ThrowAsync<InvalidOperationException>();

        (await db.Set<VendorPaymentEvidenceLink>().SingleAsync()).Id.Should().Be(first.Id);
        storage.Uploader.Verify(item => item.UploadAsync(It.IsAny<ControlledFileUploadRequest>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PaymentEvidence_DownloadDoesNotCrossPaymentOrTenantBoundary(bool foreignTenant)
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedOptionalPaymentDraftAsync(db, tenant);
        var files = new Mock<ICentralDocumentRepositoryFileService>();
        var evidence = await SeedDirectPaymentEvidenceAsync(db, fixture.Payment, files);
        var otherPayment = new VendorPayment
        {
            Id = Guid.NewGuid(), TenantId = tenant, PaymentNumber = "VP-OTHER",
            BusinessPartnerId = fixture.Payment.BusinessPartnerId, BankAccountId = fixture.Payment.BankAccountId,
            PaymentDate = fixture.Payment.PaymentDate, CurrencyCode = "GHS", ExchangeRate = 1m, TotalAmount = 100m
        };
        db.Add(otherPayment);
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, foreignTenant ? Guid.NewGuid() : tenant,
            paymentEvidenceFiles: files.Object);

        if (foreignTenant)
            await ((Func<Task>)(async () => await service.OpenEvidenceAsync(fixture.Payment.Id, evidence.Link.Id)))
                .Should().ThrowAsync<KeyNotFoundException>();
        else
            (await service.OpenEvidenceAsync(otherPayment.Id, evidence.Link.Id)).Should().BeNull();

        files.Verify(item => item.OpenAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PaymentEvidence_FileAccessStillRequiresTheFinanceScope(bool upload)
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedOptionalPaymentDraftAsync(db, tenant);
        var files = new Mock<ICentralDocumentRepositoryFileService>();
        var evidence = await SeedDirectPaymentEvidenceAsync(db, fixture.Payment, files);
        var access = new Mock<IFinanceAccessScopeService>();
        access.Setup(item => item.EnsureBankAccountAccessAsync(It.IsAny<Guid?>(),
                It.IsAny<FinanceAccessLevel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("This user is outside the bank scope."));
        var (service, _) = CreateService(db, tenant, paymentEvidenceFiles: files.Object,
            paymentEvidenceAccess: access.Object);

        Func<Task> action = upload
            ? async () => await service.UploadEvidenceAsync(fixture.Payment.Id, PaymentEvidenceUploadRequest())
            : async () => await service.OpenEvidenceAsync(fixture.Payment.Id, evidence.Link.Id);
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        access.Verify(item => item.EnsureBankAccountAccessAsync(It.IsAny<Guid?>(),
            upload ? FinanceAccessLevel.Operate : FinanceAccessLevel.Read, It.IsAny<CancellationToken>()), Times.Once());
        files.Verify(item => item.OpenAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never());
    }

    private static VendorPaymentEvidenceUploadDto PaymentEvidenceUploadRequest() => new()
    {
        ClientRequestId = Guid.NewGuid(), RequirementKey = "bank-instruction",
        FileName = "bank-instruction.pdf", ContentType = "application/pdf",
        Content = Encoding.UTF8.GetBytes("This exact supplier payment instruction is controlled evidence."),
        ExpiryDate = DateTime.UtcNow.AddDays(10)
    };

    private sealed class PaymentEvidenceStorageFixture
    {
        public Mock<IControlledFileUploadService> Uploader { get; } = new();
        public Mock<ICentralDocumentRepositoryFileService> Documents { get; } = new();
        public FileVirusScanStatus ScanStatus { get; init; } = FileVirusScanStatus.Clean;
        private FileUploadRecord? _upload;
        private byte[] _content = [];

        public PaymentEvidenceStorageFixture(ApplicationDbContext db)
        {
            Uploader.Setup(item => item.UploadAsync(It.IsAny<ControlledFileUploadRequest>(), It.IsAny<CancellationToken>()))
                .Returns(async (ControlledFileUploadRequest request, CancellationToken ct) =>
                {
                    await using var input = request.OpenReadStream();
                    using var buffer = new MemoryStream();
                    await input.CopyToAsync(buffer, ct);
                    _content = buffer.ToArray();
                    _upload = new FileUploadRecord
                    {
                        Id = Guid.NewGuid(), TenantId = request.TenantId, Category = request.Category,
                        FilePath = "private/test/" + request.FileName, StoredFileName = request.FileName,
                        OriginalFileName = request.FileName, ContentType = request.ContentType, FileSize = _content.LongLength,
                        UploadedByUserId = request.ActorUserId, StorageProvider = "Test", VirusScanStatus = ScanStatus,
                        ScannedAtUtc = DateTime.UtcNow
                    };
                    db.Add(_upload);
                    await db.SaveChangesAsync(ct);
                    return new ControlledFileUploadResult
                    {
                        Record = _upload, ChecksumSha256 = Convert.ToHexString(SHA256.HashData(_content)), PublicUrl = string.Empty
                    };
                });
            Documents.Setup(item => item.RegisterAsync(It.IsAny<CentralDocumentRepositoryRegistration>(), It.IsAny<CancellationToken>()))
                .Returns(async (CentralDocumentRepositoryRegistration request, CancellationToken ct) =>
                {
                    var record = new CentralDocumentRecord
                    {
                        Id = Guid.NewGuid(), TenantId = request.TenantId, DocumentReference = "DMS-PAYMENT-UPLOAD",
                        Title = request.Title, SourceModule = request.SourceModule, SourceEntityType = request.SourceEntityType,
                        SourceRecordId = request.SourceRecordId, SourceRecordReference = request.SourceRecordReference,
                        SourceLabel = request.SourceLabel, RepositoryStatus = "Linked", LifecycleStatus = "Active",
                        VersionStatus = request.VersionStatus, CurrentVersion = request.VersionNumber
                    };
                    var version = new CentralDocumentVersion
                    {
                        Id = Guid.NewGuid(), TenantId = request.TenantId, DocumentRecordId = record.Id, DocumentRecord = record,
                        FileUploadRecordId = _upload!.Id, FileName = _upload.OriginalFileName, ContentType = _upload.ContentType,
                        RepositoryPath = _upload.FilePath, CreatedByUserId = request.ActorUserId,
                        FileSize = _upload.FileSize, Status = request.VersionStatus, VersionNumber = request.VersionNumber
                    };
                    db.AddRange(record, version);
                    await db.SaveChangesAsync(ct);
                    Documents.Setup(item => item.OpenAsync(request.TenantId, record.Id, version.Id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(() => new CentralDocumentRepositoryContent
                        {
                            Content = new MemoryStream(_content, writable: false), FileName = _upload.OriginalFileName,
                            ContentType = _upload.ContentType!, FileSize = _content.LongLength, UploadRecord = _upload
                        });
                    return new CentralDocumentRepositoryLink
                    {
                        DocumentRecordId = record.Id, DocumentVersionId = version.Id, FileUploadRecordId = _upload.Id,
                        DocumentReference = record.DocumentReference, VersionNumber = version.VersionNumber
                    };
                });
        }
    }
}
