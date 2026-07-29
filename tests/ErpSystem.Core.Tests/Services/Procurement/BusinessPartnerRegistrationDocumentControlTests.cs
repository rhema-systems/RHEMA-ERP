using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Procurement;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class BusinessPartnerRegistrationDocumentControlTests
{
    [Fact]
    public async Task CleanControlledUploadCanBeBoundToOwnedApplication()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.UploadDocumentAsync(
            fixture.RegistrationId,
            fixture.DocumentRequest(fixture.FileRecord),
            fixture.ActorId);

        result.FileUploadRecordId.Should().Be(fixture.FileRecord.Id);
        result.CentralDocumentRecordId.Should().Be(fixture.CentralDocumentRecordId);
        result.CentralDocumentVersionId.Should().Be(fixture.CentralDocumentVersionId);
        result.FilePath.Should().BeEmpty();
        result.DocumentPath.Should().BeNull();
        result.VirusScanStatus.Should().Be(FileVirusScanStatus.Clean);
        result.ChecksumSha256.Should().Be(fixture.Checksum);
        var persisted = await fixture.Db.BusinessPartnerRegistrationDocuments
            .SingleAsync();
        persisted.TenantId.Should().Be(fixture.TenantId);
        persisted.RegistrationId.Should().Be(fixture.RegistrationId);
        persisted.FileUploadRecordId.Should().Be(fixture.FileRecord.Id);
        persisted.CentralDocumentRecordId.Should().Be(fixture.CentralDocumentRecordId);
        persisted.CentralDocumentVersionId.Should().Be(fixture.CentralDocumentVersionId);
        persisted.DocumentPath.Should().StartWith("dms://");
    }

    [Fact]
    public async Task ExternalApplicantCannotBindEvidenceToAnotherApplication()
    {
        await using var fixture = await Fixture.CreateAsync(
            registrationOwnerId: Guid.NewGuid());

        var act = () => fixture.Service.UploadDocumentAsync(
            fixture.RegistrationId,
            fixture.DocumentRequest(fixture.FileRecord),
            fixture.ActorId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*permission to upload evidence*");
        (await fixture.Db.BusinessPartnerRegistrationDocuments.CountAsync())
            .Should().Be(0);
    }

    [Fact]
    public async Task ForeignTenantControlledUploadCannotBeBoundToApplication()
    {
        await using var fixture = await Fixture.CreateAsync(
            fileTenantId: Guid.NewGuid());

        var act = () => fixture.Service.UploadDocumentAsync(
            fixture.RegistrationId,
            fixture.DocumentRequest(fixture.FileRecord),
            fixture.ActorId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found for this tenant*");
        (await fixture.Db.BusinessPartnerRegistrationDocuments.CountAsync())
            .Should().Be(0);
    }

    [Fact]
    public async Task ControlledUploadOwnedByAnotherActorCannotBeBound()
    {
        await using var fixture = await Fixture.CreateAsync(
            fileActorId: Guid.NewGuid());

        var act = () => fixture.Service.UploadDocumentAsync(
            fixture.RegistrationId,
            fixture.DocumentRequest(fixture.FileRecord),
            fixture.ActorId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*different actor*");
        (await fixture.Db.BusinessPartnerRegistrationDocuments.CountAsync())
            .Should().Be(0);
    }

    [Fact]
    public async Task SkippedControlledUploadCannotBeBoundToApplication()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.FileRecord.VirusScanStatus = FileVirusScanStatus.Skipped;
        fixture.FileRecord.ScannedAtUtc = null;
        await fixture.Db.SaveChangesAsync();

        var act = () => fixture.Service.UploadDocumentAsync(
            fixture.RegistrationId,
            fixture.DocumentRequest(fixture.FileRecord),
            fixture.ActorId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*clean virus-scan result*");
        (await fixture.Db.BusinessPartnerRegistrationDocuments.CountAsync())
            .Should().Be(0);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid TenantId { get; }
        public Guid ActorId { get; }
        public Guid RegistrationId { get; }
        public string Checksum { get; }
        public Guid CentralDocumentRecordId { get; }
        public Guid CentralDocumentVersionId { get; }
        public ApplicationDbContext Db { get; }
        public FileUploadRecord FileRecord { get; }
        public BusinessPartnerRegistrationService Service { get; }

        private Fixture(
            Guid tenantId,
            Guid actorId,
            Guid registrationId,
            string checksum,
            Guid centralDocumentRecordId,
            Guid centralDocumentVersionId,
            ApplicationDbContext db,
            FileUploadRecord fileRecord,
            BusinessPartnerRegistrationService service)
        {
            TenantId = tenantId;
            ActorId = actorId;
            RegistrationId = registrationId;
            Checksum = checksum;
            CentralDocumentRecordId = centralDocumentRecordId;
            CentralDocumentVersionId = centralDocumentVersionId;
            Db = db;
            FileRecord = fileRecord;
            Service = service;
        }

        public static async Task<Fixture> CreateAsync(
            Guid? registrationOwnerId = null,
            Guid? fileTenantId = null,
            Guid? fileActorId = null)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            var db = new ApplicationDbContext(options);
            var tenantId = Guid.NewGuid();
            var actorId = Guid.NewGuid();
            var registrationId = Guid.NewGuid();
            var checksum = new string('a', 64);
            var fileRecord = new FileUploadRecord
            {
                Id = Guid.NewGuid(),
                TenantId = fileTenantId ?? tenantId,
                Category = "supplier-registration-evidence",
                FilePath = "supplier-registration-evidence/stored.pdf",
                StoredFileName = "stored.pdf",
                OriginalFileName = "evidence.pdf",
                ContentType = "application/pdf",
                FileSize = 4,
                StorageProvider = "Test",
                UploadedByUserId = fileActorId ?? actorId,
                VirusScanStatus = FileVirusScanStatus.Clean,
                ScannedAtUtc = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedById = fileActorId ?? actorId
            };
            var centralRecord = new CentralDocumentRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DocumentReference = "DMS-PROC-TEST",
                Title = "Evidence",
                SourceModule = "Procurement",
                SourceLabel = "Supplier registration evidence",
                SourceEntityType = "BusinessPartnerRegistration",
                SourceRecordId = registrationId,
                RepositoryStatus = "Linked",
                RepositoryPath = fileRecord.FilePath,
                CurrentVersion = "v1.0",
                VersionStatus = "Submitted"
            };
            var centralVersion = new CentralDocumentVersion
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DocumentRecordId = centralRecord.Id,
                VersionNumber = "v1.0",
                Status = "Submitted",
                RepositoryPath = fileRecord.FilePath,
                FileName = fileRecord.OriginalFileName,
                ContentType = fileRecord.ContentType,
                FileSize = fileRecord.FileSize,
                FileUploadRecordId = fileRecord.Id
            };
            db.FileUploadRecords.Add(fileRecord);
            db.CentralDocumentRecords.Add(centralRecord);
            db.CentralDocumentVersions.Add(centralVersion);
            await db.SaveChangesAsync();

            var registration = new BusinessPartnerRegistration
            {
                Id = registrationId,
                TenantId = tenantId,
                RegistrationNumber = "APP-001",
                ApplicantName = "Applicant Supplier",
                PartnerType = "Supplier",
                Status = "Draft",
                CreatedById = registrationOwnerId ?? actorId
            };
            var registrations = new Mock<IBusinessPartnerRegistrationRepository>();
            registrations.Setup(item => item.GetByIdAsync(registrationId))
                .ReturnsAsync(registration);

            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(item => item.IsAuthenticated).Returns(true);
            current.SetupGet(item => item.IsExternalUser).Returns(true);
            current.SetupGet(item => item.TenantId).Returns(tenantId);
            current.SetupGet(item => item.UserId).Returns(actorId);

            var unitOfWork = new UnitOfWork(db);
            var service = new BusinessPartnerRegistrationService(
                registrations.Object,
                new BusinessPartnerRegistrationDocumentRepository(db),
                Mock.Of<IBusinessPartnerRegistrationStatusHistoryRepository>(),
                Mock.Of<IBusinessPartnerRepository>(),
                Mock.Of<IBusinessPartnerContactRepository>(),
                Mock.Of<IBusinessPartnerFinancialRepository>(),
                Mock.Of<IBusinessPartnerDocumentRepository>(),
                Mock.Of<IBusinessPartnerLicenseRepository>(),
                unitOfWork,
                current.Object,
                Mock.Of<IAppEventBus>(),
                Mock.Of<IProcurementAccessControlService>(),
                NullLogger<BusinessPartnerRegistrationService>.Instance);

            return new Fixture(
                tenantId,
                actorId,
                registrationId,
                checksum,
                centralRecord.Id,
                centralVersion.Id,
                db,
                fileRecord,
                service);
        }

        public CreateBusinessPartnerDocumentDto DocumentRequest(
            FileUploadRecord fileRecord) =>
            new()
            {
                FileUploadRecordId = fileRecord.Id,
                CentralDocumentRecordId = CentralDocumentRecordId,
                CentralDocumentVersionId = CentralDocumentVersionId,
                DocumentType = "Tax Clearance",
                DocumentName = fileRecord.OriginalFileName,
                DocumentPath = fileRecord.FilePath,
                FilePath = fileRecord.FilePath,
                FileSize = fileRecord.FileSize,
                MimeType = fileRecord.ContentType,
                ChecksumSha256 = Checksum
            };

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
