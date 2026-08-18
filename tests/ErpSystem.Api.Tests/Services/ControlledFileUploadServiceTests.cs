using System.Reflection;
using ErpSystem.Api.Controllers;
using ErpSystem.Api.Services;
using ErpSystem.Api.Services.DocumentManagement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Models;
using ErpSystem.Core.Services;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class ControlledFileUploadServiceTests
{
    [Fact]
    public async Task CentralRepositoryRegistersOnlyTenantCleanControlledUpload()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var sourceRecordId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        await using var db = Database();
        db.FileUploadRecords.Add(new FileUploadRecord
        {
            Id = uploadId,
            TenantId = tenantId,
            Category = ControlledFileUploadCategories.DocumentManagement,
            FilePath = "private/document-management/evidence.pdf",
            StoredFileName = "evidence.pdf",
            OriginalFileName = "evidence.pdf",
            ContentType = "application/pdf",
            FileSize = 25,
            StorageProvider = "test",
            UploadedByUserId = actorId,
            VirusScanStatus = FileVirusScanStatus.Clean
        });
        await db.SaveChangesAsync();
        var storage = new Mock<IFileStorageService>();
        var controlled = new Mock<IControlledFileUploadService>();
        var service = new CentralDocumentRepositoryFileService(
            db, storage.Object, controlled.Object);

        var link = await service.RegisterAsync(
            new CentralDocumentRepositoryRegistration
            {
                TenantId = tenantId,
                ActorUserId = actorId,
                ActorName = "Applicant",
                FileUploadRecordId = uploadId,
                SourceModule = "Procurement",
                SourceLabel = "Supplier registration evidence",
                SourceEntityType = "BusinessPartnerRegistration",
                SourceRecordId = sourceRecordId,
                Title = "Tax clearance",
                DocumentType = "TaxClearance",
                AccessProfile = "Procurement restricted"
            });

        link.FileUploadRecordId.Should().Be(uploadId);
        var record = await db.CentralDocumentRecords.SingleAsync();
        record.Id.Should().Be(link.DocumentRecordId);
        record.TenantId.Should().Be(tenantId);
        record.SourceRecordId.Should().Be(sourceRecordId);
        record.RepositoryPath.Should().Be(
            "private/document-management/evidence.pdf");
        var version = await db.CentralDocumentVersions.SingleAsync();
        version.Id.Should().Be(link.DocumentVersionId);
        version.FileUploadRecordId.Should().Be(uploadId);

        (await service.OpenAsync(
                Guid.NewGuid(), record.Id, version.Id))
            .Should().BeNull();
        storage.Verify(item => item.DownloadFileAsync(
            It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Theory]
    [InlineData(ControlledFileUploadCategories.DocumentManagement)]
    [InlineData(ControlledFileUploadCategories.SupplierRegistrationEvidence)]
    [InlineData(ControlledFileUploadCategories.ProcurementReceiptSourceEvidence)]
    [InlineData(ControlledFileUploadCategories.QuantitySurveyBoqImport)]
    [InlineData(ControlledFileUploadCategories.QuantitySurveyTenderBoqSubmission)]
    [InlineData(ControlledFileUploadCategories.HrCandidateCv)]
    [InlineData(ControlledFileUploadCategories.HrCandidateDocuments)]
    [InlineData(ControlledFileUploadCategories.HrCandidatePhotos)]
    [InlineData(ControlledFileUploadCategories.HrLeaveAttachments)]
    [InlineData(ControlledFileUploadCategories.HrPipAttachments)]
    [InlineData(ControlledFileUploadCategories.HrDisciplineDocuments)]
    [InlineData(ControlledFileUploadCategories.HrStaffMovementAttachments)]
    [InlineData(ControlledFileUploadCategories.HrOfferLetters)]
    [InlineData(ControlledFileUploadCategories.HrMedicalExamDocuments)]
    public async Task SensitiveCategoriesAreStoredOutsideThePublicWebRoot(
        string category)
    {
        var root = Path.Combine(
            Path.GetTempPath(), $"erp-private-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var environment = new Mock<IHostEnvironment>();
            environment.SetupGet(item => item.ContentRootPath).Returns(root);
            var service = new LocalFileStorageService(
                NullLogger<LocalFileStorageService>.Instance,
                Options.Create(new StorageProviderOptions
                {
                    Local = new LocalStorageOptions
                    {
                        BasePath = "uploads",
                        PrivateBasePath = "secure-file-storage",
                        UseWebRoot = true
                    }
                }),
                environment.Object);
            await using var content = new MemoryStream([1, 2, 3]);

            var result = await service.UploadFileAsync(new FileUploadRequest
            {
                FileStream = content,
                FileName = "evidence.pdf",
                ContentType = "application/pdf",
                FileSize = content.Length,
                Category = category,
                TenantId = Guid.NewGuid().ToString()
            });

            result.Success.Should().BeTrue();
            result.FilePath.Should().StartWith("private/");
            result.PublicUrl.Should().BeEmpty();
            File.Exists(Path.Combine(
                    root,
                    "secure-file-storage",
                    result.FilePath["private/".Length..]
                        .Replace('/', Path.DirectorySeparatorChar)))
                .Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// Every declared HR category must be scan-mandatory. GetEffectivePolicyAsync
    /// defaults RequireVirusScan to false, so a category missing from
    /// SystemCleanScanRequired is silently never scanned — and because
    /// CentralDocumentRepositoryFileService.RegisterAsync rejects Skipped exactly as
    /// firmly as Infected, it would also fail DMS registration after the bytes were
    /// already stored. Reflection rather than a hard-coded list so a category added
    /// later without registering it fails here instead of in production.
    /// </summary>
    [Fact]
    public void EveryHrCategoryRequiresACleanScan()
    {
        var hrCategories = typeof(ControlledFileUploadCategories)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Where(value => value.StartsWith("hr-", StringComparison.OrdinalIgnoreCase))
            .ToList();

        hrCategories.Should().NotBeEmpty();
        hrCategories.Should().OnlyContain(
            category => ControlledFileUploadCategories
                .SystemCleanScanRequired.Contains(category));
    }

    [Fact]
    public void ModelEnforcesOneActiveSupplierApplicationPerTenantContact()
    {
        using var db = Database();
        var entity = db.Model.FindEntityType(
            typeof(ProcurementSupplierApplicantAccess));
        var index = entity!.GetIndexes().Single(candidate =>
            candidate.GetDatabaseName() ==
            "UX_ProcurementSupplierApplicantAccesses_ActiveContact");

        index.IsUnique.Should().BeTrue();
        index.GetFilter().Should()
            .Be("[IsDeleted] = 0 AND [Status] IN (0, 1, 2, 5)");
    }

    [Fact]
    public async Task CleanControlledUploadPersistsTenantMetadataAndChecksum()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = Database();
        db.FileUploadPolicies.Add(new FileUploadPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Category = "supplier-registration-evidence",
            IsEnabled = true,
            RequireVirusScan = true,
            AllowedExtensionsCsv = ".pdf",
            AllowedMimeTypesCsv = "application/pdf"
        });
        await db.SaveChangesAsync();

        var storage = Storage(result: Stored(tenantId));
        var scanner = new Mock<IFileVirusScanService>();
        scanner.Setup(item => item.ScanAsync(
                It.IsAny<FileVirusScanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileVirusScanResult
            {
                Status = FileVirusScanStatus.Clean,
                Message = "clean"
            });
        var service = Service(db, storage.Object, scanner.Object);

        var result = await service.UploadAsync(Request(tenantId, actorId));

        result.Record.TenantId.Should().Be(tenantId);
        result.Record.UploadedByUserId.Should().Be(actorId);
        result.Record.VirusScanStatus.Should().Be(FileVirusScanStatus.Clean);
        result.Record.Category.Should().Be("supplier-registration-evidence");
        result.ChecksumSha256.Should().HaveLength(64);
        (await db.FileUploadRecords.SingleAsync()).Id.Should().Be(result.Record.Id);
        scanner.Verify(item => item.ScanAsync(
            It.Is<FileVirusScanRequest>(request =>
                request.TenantId == tenantId &&
                request.Category == "supplier-registration-evidence" &&
                request.FileName == "evidence.pdf"),
            It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(item => item.UploadFileAsync(It.Is<FileUploadRequest>(
            request => request.TenantId == tenantId.ToString() &&
                       request.Category == "supplier-registration-evidence" &&
                       request.Metadata["checksumSha256"] == result.ChecksumSha256 &&
                       request.Metadata["uploadedByUserId"] == actorId.ToString())),
            Times.Once);
    }

    [Theory]
    [InlineData("*", ControlledFileUploadCategories.SupplierRegistrationEvidence)]
    [InlineData(
        ControlledFileUploadCategories.SupplierRegistrationEvidence,
        ControlledFileUploadCategories.SupplierRegistrationEvidence)]
    [InlineData("*", ControlledFileUploadCategories.DocumentManagement)]
    public async Task SystemCategoryForcesCleanScanWhenPolicyDisablesScanning(
        string policyCategory,
        string uploadCategory)
    {
        var tenantId = Guid.NewGuid();
        await using var db = Database();
        db.FileUploadPolicies.Add(new FileUploadPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Category = policyCategory,
            IsEnabled = true,
            RequireVirusScan = false
        });
        await db.SaveChangesAsync();
        var storage = Storage(result: Stored(tenantId));
        var scanner = CleanScanner();
        var service = Service(db, storage.Object, scanner.Object);

        var result = await service.UploadAsync(
            Request(tenantId, Guid.NewGuid(), uploadCategory));

        result.Record.VirusScanStatus.Should().Be(FileVirusScanStatus.Clean);
        scanner.Verify(item => item.ScanAsync(
            It.Is<FileVirusScanRequest>(request =>
                request.Category == uploadCategory),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfiguredModuleCategoryUsesTheCentralCleanScanBoundary()
    {
        const string moduleCategory = "legal-case-documents";
        var tenantId = Guid.NewGuid();
        await using var db = Database();
        var storage = Storage(result: Stored(tenantId));
        var scanner = CleanScanner();
        var service = Service(
            db,
            storage.Object,
            scanner.Object,
            requiredCleanScanCategoriesCsv: moduleCategory);

        var result = await service.UploadAsync(
            Request(tenantId, Guid.NewGuid(), moduleCategory));

        result.Record.VirusScanStatus.Should().Be(FileVirusScanStatus.Clean);
        scanner.Verify(item => item.ScanAsync(
            It.Is<FileVirusScanRequest>(request =>
                request.Category == moduleCategory),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DefaultPolicyRejectsScriptableSvgBeforeStorageWrite()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Database();
        var storage = Storage(result: Stored(tenantId));
        var service = Service(
            db, storage.Object, Mock.Of<IFileVirusScanService>());
        var content = System.Text.Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");

        var act = () => service.UploadAsync(new ControlledFileUploadRequest
        {
            TenantId = tenantId,
            ActorUserId = Guid.NewGuid(),
            ActorName = "untrusted applicant",
            Category = "supplier-registration-evidence",
            FileName = "active-content.svg",
            ContentType = "image/svg+xml",
            FileSize = content.Length,
            OpenReadStream = () => new MemoryStream(content, writable: false)
        });

        (await act.Should().ThrowAsync<ControlledFileUploadException>())
            .Which.Code.Should().Be("FILE_ACTIVE_CONTENT_NOT_ALLOWED");
        (await db.FileUploadRecords.CountAsync()).Should().Be(0);
        storage.Verify(item => item.UploadFileAsync(
            It.IsAny<FileUploadRequest>()), Times.Never);
    }

    [Fact]
    public async Task TenantPolicyCannotReenableScriptableSvg()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Database();
        db.FileUploadPolicies.Add(new FileUploadPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Category = "supplier-registration-evidence",
            IsEnabled = true,
            AllowedExtensionsCsv = ".svg",
            AllowedMimeTypesCsv = "image/svg+xml"
        });
        await db.SaveChangesAsync();
        var storage = Storage(result: Stored(tenantId));
        var service = Service(
            db, storage.Object, Mock.Of<IFileVirusScanService>());
        var content = System.Text.Encoding.UTF8.GetBytes("<svg/>");

        var act = () => service.UploadAsync(new ControlledFileUploadRequest
        {
            TenantId = tenantId,
            ActorUserId = Guid.NewGuid(),
            Category = "supplier-registration-evidence",
            FileName = "configured-active-content.svg",
            ContentType = "image/svg+xml",
            FileSize = content.Length,
            OpenReadStream = () => new MemoryStream(content, writable: false)
        });

        (await act.Should().ThrowAsync<ControlledFileUploadException>())
            .Which.Code.Should().Be("FILE_ACTIVE_CONTENT_NOT_ALLOWED");
        (await db.FileUploadRecords.CountAsync()).Should().Be(0);
        storage.Verify(item => item.UploadFileAsync(
            It.IsAny<FileUploadRequest>()), Times.Never);
    }

    [Theory]
    [InlineData(FileVirusScanStatus.Infected, "FILE_VIRUS_DETECTED")]
    [InlineData(FileVirusScanStatus.Error, "FILE_VIRUS_SCAN_INCOMPLETE")]
    [InlineData(FileVirusScanStatus.Skipped, "FILE_VIRUS_SCAN_INCOMPLETE")]
    public async Task RequiredVirusScanFailsClosed(
        FileVirusScanStatus status,
        string expectedCode)
    {
        var tenantId = Guid.NewGuid();
        await using var db = Database();
        db.FileUploadPolicies.Add(new FileUploadPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Category = "*",
            IsEnabled = true,
            RequireVirusScan = true
        });
        await db.SaveChangesAsync();
        var storage = Storage(result: Stored(tenantId));
        var scanner = new Mock<IFileVirusScanService>();
        scanner.Setup(item => item.ScanAsync(
                It.IsAny<FileVirusScanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileVirusScanResult { Status = status });
        var service = Service(db, storage.Object, scanner.Object);

        var act = () => service.UploadAsync(Request(tenantId, Guid.NewGuid()));

        var exception = await act.Should()
            .ThrowAsync<ControlledFileUploadException>();
        exception.Which.Code.Should().Be(expectedCode);
        (await db.FileUploadRecords.CountAsync()).Should().Be(0);
        storage.Verify(item => item.UploadFileAsync(
            It.IsAny<FileUploadRequest>()), Times.Never);
    }

    [Fact]
    public async Task RequiredVirusScannerExceptionFailsClosedBeforeStorageWrite()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Database();
        db.FileUploadPolicies.Add(new FileUploadPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Category = "supplier-registration-evidence",
            IsEnabled = true,
            RequireVirusScan = true
        });
        await db.SaveChangesAsync();
        var storage = Storage(result: Stored(tenantId));
        var scanner = new Mock<IFileVirusScanService>();
        scanner.Setup(item => item.ScanAsync(
                It.IsAny<FileVirusScanRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("scanner unavailable"));
        var service = Service(db, storage.Object, scanner.Object);

        var act = () => service.UploadAsync(Request(tenantId, Guid.NewGuid()));

        (await act.Should().ThrowAsync<ControlledFileUploadException>())
            .Which.Code.Should().Be("FILE_VIRUS_SCAN_INCOMPLETE");
        (await db.FileUploadRecords.CountAsync()).Should().Be(0);
        storage.Verify(item => item.UploadFileAsync(
            It.IsAny<FileUploadRequest>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StorageFailureDoesNotCreateEvidenceMetadata(bool throws)
    {
        var tenantId = Guid.NewGuid();
        await using var db = Database();
        var storage = new Mock<IFileStorageService>();
        storage.SetupGet(item => item.ProviderName).Returns("Test");
        var setup = storage.Setup(item => item.UploadFileAsync(
            It.IsAny<FileUploadRequest>()));
        if (throws)
        {
            setup.ThrowsAsync(new IOException("storage unavailable"));
        }
        else
        {
            setup.ReturnsAsync(FailedStorage(tenantId));
        }
        var service = Service(
            db, storage.Object, CleanScanner().Object);

        var act = () => service.UploadAsync(
            Request(tenantId, Guid.NewGuid()));

        var exception = await act.Should()
            .ThrowAsync<ControlledFileUploadException>();
        exception.Which.Code.Should().Be("FILE_STORAGE_FAILED");
        exception.Which.StatusCode.Should().Be(502);
        (await db.FileUploadRecords.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task TenantQuotaIsEnforcedBeforeStorageWrite()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Database();
        db.FileUploadPolicies.Add(new FileUploadPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Category = "*",
            IsEnabled = true,
            MaxTenantTotalBytes = 3
        });
        await db.SaveChangesAsync();
        var storage = Storage(result: Stored(tenantId));
        var scanner = new Mock<IFileVirusScanService>();
        var service = Service(db, storage.Object, scanner.Object);

        var act = () => service.UploadAsync(Request(tenantId, Guid.NewGuid()));

        (await act.Should().ThrowAsync<ControlledFileUploadException>())
            .Which.Code.Should().Be("FILE_TENANT_QUOTA_EXCEEDED");
        storage.Verify(item => item.UploadFileAsync(
            It.IsAny<FileUploadRequest>()), Times.Never);
    }

    [Fact]
    public async Task DeleteCannotCrossTenantBoundary()
    {
        var ownerTenantId = Guid.NewGuid();
        await using var db = Database();
        var record = new FileUploadRecord
        {
            Id = Guid.NewGuid(),
            TenantId = ownerTenantId,
            Category = "supplier-registration-evidence",
            FilePath = "supplier-registration-evidence/file.pdf",
            StoredFileName = "file.pdf",
            OriginalFileName = "evidence.pdf",
            FileSize = 4,
            StorageProvider = "Test",
            UploadedByUserId = Guid.NewGuid()
        };
        db.FileUploadRecords.Add(record);
        await db.SaveChangesAsync();
        var storage = Storage(result: Stored(ownerTenantId));
        var service = Service(
            db, storage.Object, Mock.Of<IFileVirusScanService>());

        var act = () => service.DeleteAsync(
            Guid.NewGuid(), record.Id, Guid.NewGuid());

        (await act.Should().ThrowAsync<ControlledFileUploadException>())
            .Which.Code.Should().Be("FILE_RECORD_NOT_FOUND");
        storage.Verify(item => item.DeleteFileAsync(
            It.IsAny<string>()), Times.Never);
        (await db.FileUploadRecords.IgnoreQueryFilters().SingleAsync())
            .IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteRejectsFileReferencedByActiveSupplierRegistrationEvidence()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = Database();
        var record = FileRecord(tenantId);
        db.FileUploadRecords.Add(record);
        db.BusinessPartnerRegistrationDocuments.Add(
            new BusinessPartnerRegistrationDocument
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RegistrationId = Guid.NewGuid(),
                FileUploadRecordId = record.Id,
                DocumentType = "TaxClearance",
                DocumentName = "tax-clearance.pdf",
                DocumentPath = record.FilePath
            });
        await db.SaveChangesAsync();
        var storage = Storage(result: Stored(tenantId));
        var service = Service(
            db, storage.Object, Mock.Of<IFileVirusScanService>());

        var act = () => service.DeleteAsync(tenantId, record.Id, actorId);

        (await act.Should().ThrowAsync<ControlledFileUploadException>())
            .Which.Code.Should()
            .Be("FILE_RECORD_REFERENCED_BY_REGISTRATION_EVIDENCE");
        (await db.FileUploadRecords.IgnoreQueryFilters().SingleAsync())
            .IsDeleted.Should().BeFalse();
        storage.Verify(item => item.DeleteFileAsync(
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteRejectsFileReferencedByActiveFinanceCloseEvidence()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = Database();
        var record = FileRecord(tenantId);
        record.Category = ControlledFileUploadCategories.FinanceCloseEvidence;
        db.FileUploadRecords.Add(record);
        db.FinanceCloseEvidenceAttachments.Add(new FinanceCloseEvidenceAttachment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinanceCloseCycleId = Guid.NewGuid(),
            FinanceCloseTaskId = Guid.NewGuid(),
            FileUploadRecordId = record.Id,
            EvidenceType = FinanceCloseEvidenceTypes.SupportingDocument
        });
        await db.SaveChangesAsync();
        var storage = Storage(result: Stored(tenantId));
        var service = Service(db, storage.Object, Mock.Of<IFileVirusScanService>());

        var act = () => service.DeleteAsync(tenantId, record.Id, actorId);

        (await act.Should().ThrowAsync<ControlledFileUploadException>())
            .Which.Code.Should().Be("FILE_RECORD_REFERENCED_BY_FINANCE_CLOSE_EVIDENCE");
        (await db.FileUploadRecords.IgnoreQueryFilters().SingleAsync()).IsDeleted.Should().BeFalse();
        storage.Verify(item => item.DeleteFileAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteCommitsMetadataAndSchedulesStorageCleanup()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = Database();
        var record = FileRecord(tenantId);
        db.FileUploadRecords.Add(record);
        await db.SaveChangesAsync();
        var storage = Storage(result: Stored(tenantId));
        var service = Service(
            db, storage.Object, Mock.Of<IFileVirusScanService>());

        await service.DeleteAsync(tenantId, record.Id, actorId);

        var persisted = await db.FileUploadRecords
            .IgnoreQueryFilters()
            .SingleAsync();
        persisted.IsDeleted.Should().BeTrue();
        persisted.DeletedBy.Should().Be(actorId.ToString());
        persisted.StorageDeletedAtUtc.Should().BeNull();
        persisted.StorageDeleteNextAttemptAtUtc.Should().NotBeNull();
        storage.Verify(item => item.DeleteFileAsync(
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task StorageDeleteFalseRemainsDurablyRetryable()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Database();
        var record = FileRecord(tenantId);
        record.IsDeleted = true;
        record.DeletedAt = DateTime.UtcNow.AddMinutes(-1);
        record.StorageDeleteNextAttemptAtUtc =
            DateTime.UtcNow.AddSeconds(-1);
        db.FileUploadRecords.Add(record);
        await db.SaveChangesAsync();
        var storage = Storage(result: Stored(tenantId));
        storage.Setup(item => item.DeleteFileAsync(record.FilePath))
            .ReturnsAsync(false);
        var processor = CleanupProcessor(db, storage.Object);

        var processed = await processor.ProcessPendingAsync();

        processed.Should().Be(1);
        var persisted = await db.FileUploadRecords
            .IgnoreQueryFilters()
            .SingleAsync();
        persisted.StorageDeletedAtUtc.Should().BeNull();
        persisted.StorageDeleteAttemptCount.Should().Be(1);
        persisted.StorageDeleteLastAttemptAtUtc.Should().NotBeNull();
        persisted.StorageDeleteNextAttemptAtUtc.Should()
            .BeAfter(persisted.StorageDeleteLastAttemptAtUtc!.Value);
        persisted.StorageDeleteLastError.Should().Contain(
            "returned false");
    }

    [Fact]
    public async Task StorageDeleteSuccessCompletesDurableCleanup()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Database();
        var record = FileRecord(tenantId);
        record.IsDeleted = true;
        record.DeletedAt = DateTime.UtcNow.AddMinutes(-1);
        record.StorageDeleteNextAttemptAtUtc =
            DateTime.UtcNow.AddSeconds(-1);
        db.FileUploadRecords.Add(record);
        await db.SaveChangesAsync();
        var storage = Storage(result: Stored(tenantId));
        var processor = CleanupProcessor(db, storage.Object);

        var processed = await processor.ProcessPendingAsync();

        processed.Should().Be(1);
        var persisted = await db.FileUploadRecords
            .IgnoreQueryFilters()
            .SingleAsync();
        persisted.StorageDeletedAtUtc.Should().NotBeNull();
        persisted.StorageDeleteAttemptCount.Should().Be(1);
        persisted.StorageDeleteNextAttemptAtUtc.Should().BeNull();
        persisted.StorageDeleteLastError.Should().BeNull();
    }

    private static ControlledFileUploadService Service(
        ApplicationDbContext db,
        IFileStorageService storage,
        IFileVirusScanService scanner,
        string? requiredCleanScanCategoriesCsv = null) =>
        new(
            Options.Create(new FileUploadOptions
            {
                MaxFileSizeBytes = 1024,
                RequiredCleanScanCategoriesCsv =
                    requiredCleanScanCategoriesCsv
            }),
            storage,
            scanner,
            db,
            NullLogger<ControlledFileUploadService>.Instance);

    private static FileStorageCleanupProcessor CleanupProcessor(
        ApplicationDbContext db,
        IFileStorageService storage) =>
        new(
            db,
            storage,
            Options.Create(new FileUploadOptions
            {
                StorageCleanupBatchSize = 10
            }),
            NullLogger<FileStorageCleanupProcessor>.Instance);

    private static ControlledFileUploadRequest Request(
        Guid tenantId,
        Guid actorId,
        string category =
            ControlledFileUploadCategories.SupplierRegistrationEvidence)
    {
        var content = new byte[] { 1, 2, 3, 4 };
        return new ControlledFileUploadRequest
        {
            TenantId = tenantId,
            ActorUserId = actorId,
            ActorName = "test",
            Category = category,
            FileName = "evidence.pdf",
            ContentType = "application/pdf",
            FileSize = content.Length,
            OpenReadStream = () => new MemoryStream(content, writable: false)
        };
    }

    private static Mock<IFileStorageService> Storage(FileStorageResult result)
    {
        var storage = new Mock<IFileStorageService>();
        storage.SetupGet(item => item.ProviderName).Returns("Test");
        storage.Setup(item => item.UploadFileAsync(
                It.IsAny<FileUploadRequest>()))
            .ReturnsAsync(result);
        storage.Setup(item => item.DeleteFileAsync(It.IsAny<string>()))
            .ReturnsAsync(true);
        return storage;
    }

    private static FileUploadRecord FileRecord(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Category = ControlledFileUploadCategories.SupplierRegistrationEvidence,
        FilePath = "supplier-registration-evidence/file.pdf",
        StoredFileName = "file.pdf",
        OriginalFileName = "evidence.pdf",
        FileSize = 4,
        StorageProvider = "Test",
        UploadedByUserId = Guid.NewGuid()
    };

    private static Mock<IFileVirusScanService> CleanScanner()
    {
        var scanner = new Mock<IFileVirusScanService>();
        scanner.Setup(item => item.ScanAsync(
                It.IsAny<FileVirusScanRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileVirusScanResult
            {
                Status = FileVirusScanStatus.Clean,
                Message = "clean"
            });
        return scanner;
    }

    private static FileStorageResult Stored(Guid tenantId) => new()
    {
        Success = true,
        FileName = "stored.pdf",
        OriginalFileName = "evidence.pdf",
        FilePath = "supplier-registration-evidence/stored.pdf",
        PublicUrl = "/files/stored.pdf",
        FileSize = 4,
        ContentType = "application/pdf",
        Category = "supplier-registration-evidence",
        TenantId = tenantId.ToString(),
        StorageProvider = "Test"
    };

    private static FileStorageResult FailedStorage(Guid tenantId) => new()
    {
        Success = false,
        ErrorMessage = "storage rejected upload",
        FileName = string.Empty,
        OriginalFileName = "evidence.pdf",
        FilePath = string.Empty,
        PublicUrl = string.Empty,
        FileSize = 0,
        ContentType = "application/pdf",
        Category = "supplier-registration-evidence",
        TenantId = tenantId.ToString(),
        StorageProvider = "Test"
    };

    private static ApplicationDbContext Database()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
