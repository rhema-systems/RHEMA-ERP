using ErpSystem.Api.Services.DocumentManagement;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Procurement;

public sealed class ProcurementCentralDmsAdoptionTests
{
    [Fact]
    public async Task GovernedRegistrationPersistsMetadataAccessRetentionAndAuditLineage()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        await using var db = Database();
        SeedGovernance(db, tenantId, actorId, uploadId, published: true);
        await db.SaveChangesAsync();
        var service = Service(db);

        var link = await service.RegisterAsync(Request(tenantId, actorId, uploadId));

        var record = await db.CentralDocumentRecords.SingleAsync();
        record.Id.Should().Be(link.DocumentRecordId);
        record.TenantId.Should().Be(tenantId);
        record.MetadataTemplateCode.Should().Be("TDC-PROC-TENDER");
        record.AccessProfile.Should().Be("Procurement tender restricted");
        record.RetentionStatus.Should().Be("Current");
        (await db.CentralDocumentMetadataValues
                .Where(item => item.DocumentRecordId == record.Id)
                .Select(item => item.FieldKey)
                .ToListAsync())
            .Should().BeEquivalentTo(
                "sourcereference", "documentfamily", "classification",
                "sourcestatus", "uploadedby", "checksumsha256");
        var version = await db.CentralDocumentVersions.SingleAsync();
        version.FileUploadRecordId.Should().Be(uploadId);
        version.DocumentRecordId.Should().Be(record.Id);
        (await db.AuditLogs.SingleAsync()).Action
            .Should().Be("CentralDmsDocumentRegistered");
    }

    [Fact]
    public async Task GovernedRegistrationFailsClosedWhenRequiredMetadataIsMissing()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        await using var db = Database();
        SeedGovernance(db, tenantId, actorId, uploadId, published: true);
        await db.SaveChangesAsync();
        var request = Request(tenantId, actorId, uploadId, includeChecksum: false);

        var action = () => Service(db).RegisterAsync(request);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*checksumSha256*");
        (await db.CentralDocumentRecords.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GovernedRegistrationRejectsUnpublishedTenantTemplate()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        await using var db = Database();
        SeedGovernance(db, tenantId, actorId, uploadId, published: false);
        await db.SaveChangesAsync();

        var action = () => Service(db).RegisterAsync(Request(tenantId, actorId, uploadId));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Published Central DMS metadata template*");
    }

    [Fact]
    public void ProcurementCatalogueUsesControlledSourcesAndClassificationsForEveryRequiredFamily()
    {
        ProcurementDocumentManagementCatalog.Families.Should().HaveCount(10);
        ProcurementDocumentManagementCatalog.Families
            .Should().OnlyContain(item =>
                !string.IsNullOrWhiteSpace(item.TemplateCode) &&
                !string.IsNullOrWhiteSpace(item.AccessProfile) &&
                !string.IsNullOrWhiteSpace(item.ReadPermission) &&
                item.UploadPermissions.Count > 0 &&
                item.Classifications.Count > 0);
        ProcurementDocumentManagementCatalog.Families
            .Select(item => item.Code)
            .Should().OnlyHaveUniqueItems();

        var requisition = ProcurementDocumentManagementCatalog.Find(
            ErpSystem.Core.DTOs.Procurement.ProcurementDocumentFamily.Requisition);
        requisition.Should().NotBeNull();
        requisition!.SourceEntityType.Should().Be("PurchaseRequisition");
        requisition.TemplateCode.Should().Be("TDC-PROC-REQUISITION");
        requisition.UploadPermissions.Should().Contain("procurement.requisition.create");
        requisition.Classifications.Should().Contain(["Specification", "Supporting document"]);
    }

    [Fact]
    public async Task DraftRequisitionDocumentRemovalIsTenantScopedAndAudited()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var requisitionId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        await using var db = Database();
        SeedRequisitionDocument(db, tenantId, actorId, requisitionId, documentId, "Draft");
        await db.SaveChangesAsync();
        var centralDocuments = new Mock<ICentralDocumentRepositoryFileService>();
        centralDocuments
            .Setup(service => service.DeleteAsync(tenantId, documentId, actorId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var controller = DocumentController(db, tenantId, actorId, centralDocuments);

        var result = await controller.RemoveRequisitionDocument(documentId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        centralDocuments.Verify(service => service.DeleteAsync(
            tenantId, documentId, actorId, It.IsAny<CancellationToken>()), Times.Once);
        var audit = await db.AuditLogs.SingleAsync();
        audit.Action.Should().Be("PurchaseRequisitionDocumentRemoved");
        audit.Resource.Should().Be("PurchaseRequisition");
        audit.ResourceId.Should().Be(requisitionId.ToString());
    }

    [Fact]
    public async Task SubmittedRequisitionDocumentRemovalFailsClosed()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        await using var db = Database();
        SeedRequisitionDocument(db, tenantId, actorId, Guid.NewGuid(), documentId, "Submitted");
        await db.SaveChangesAsync();
        var centralDocuments = new Mock<ICentralDocumentRepositoryFileService>(MockBehavior.Strict);
        var controller = DocumentController(db, tenantId, actorId, centralDocuments);

        var result = await controller.RemoveRequisitionDocument(documentId, CancellationToken.None);

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        var problem = conflict.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be("PR_DOCUMENTS_DRAFT_ONLY");
        centralDocuments.Verify(service => service.DeleteAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ProcurementAttachmentModelRequiresUniqueTenantCentralDocumentLineage()
    {
        using var db = Database();
        foreach (var entityType in new[]
                 {
                     typeof(ErpSystem.Core.Entities.Procurement.TenderDocument),
                     typeof(ErpSystem.Core.Entities.Procurement.TenderBidDocument),
                     typeof(ErpSystem.Core.Entities.Procurement.TenderAwardVerificationItemDocument)
                 })
        {
            var entity = db.Model.FindEntityType(entityType)!;
            var index = entity.GetIndexes().Single(item =>
                item.Properties.Select(property => property.Name)
                    .SequenceEqual(new[] { "TenantId", "CentralDocumentRecordId" }));
            index.IsUnique.Should().BeTrue();
            entity.GetForeignKeys().Should().Contain(item =>
                item.Properties.Single().Name == "FileUploadRecordId" &&
                item.PrincipalEntityType.ClrType == typeof(FileUploadRecord));
            entity.GetForeignKeys().Should().Contain(item =>
                item.Properties.Single().Name == "CentralDocumentRecordId" &&
                item.PrincipalEntityType.ClrType == typeof(CentralDocumentRecord));
            entity.GetForeignKeys().Should().Contain(item =>
                item.Properties.Single().Name == "CentralDocumentVersionId" &&
                item.PrincipalEntityType.ClrType == typeof(CentralDocumentVersion));
        }
    }

    private static CentralDocumentRepositoryFileService Service(ApplicationDbContext db) =>
        new(db, Mock.Of<IFileStorageService>(), Mock.Of<IControlledFileUploadService>());

    private static ProcurementDocumentManagementController DocumentController(
        ApplicationDbContext db,
        Guid tenantId,
        Guid actorId,
        Mock<ICentralDocumentRepositoryFileService> centralDocuments)
    {
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(provider => provider.TenantId).Returns(tenantId);
        currentUser.SetupGet(provider => provider.UserId).Returns(actorId);
        currentUser.SetupGet(provider => provider.Username).Returns("procurement.officer");
        currentUser.SetupGet(provider => provider.FullName).Returns("Procurement Officer");
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<System.Security.Claims.ClaimsPrincipal>(),
                It.IsAny<object?>(),
                It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());

        return new ProcurementDocumentManagementController(
            db,
            currentUser.Object,
            authorization.Object,
            Mock.Of<IControlledFileUploadService>(),
            centralDocuments.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static void SeedRequisitionDocument(
        ApplicationDbContext db,
        Guid tenantId,
        Guid actorId,
        Guid requisitionId,
        Guid documentId,
        string status)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Test tenant",
            Code = $"T{tenantId:N}"[..20]
        });
        var actor = new ApplicationUser
        {
            Id = actorId,
            TenantId = tenantId,
            UserName = "procurement.officer",
            NormalizedUserName = "PROCUREMENT.OFFICER",
            FirstName = "Procurement",
            LastName = "Officer"
        };
        db.Users.Add(actor);
        db.PurchaseRequisitions.Add(new ErpSystem.Core.Entities.Procurement.PurchaseRequisition
        {
            Id = requisitionId,
            TenantId = tenantId,
            RequisitionNumber = "PR-TEST-001",
            RequestedById = actorId,
            RequestedBy = actor,
            Status = status
        });
        db.CentralDocumentRecords.Add(new CentralDocumentRecord
        {
            Id = documentId,
            TenantId = tenantId,
            DocumentReference = "DMS-PR-001",
            Title = "Specification.pdf",
            SourceModule = ProcurementDocumentManagementCatalog.SourceModule,
            SourceLabel = "Purchase requisition supporting documents",
            SourceEntityType = "PurchaseRequisition",
            SourceRecordId = requisitionId,
            SourceRecordReference = "PR-TEST-001",
            MetadataTemplateCode = "TDC-PROC-REQUISITION",
            AccessProfile = "Procurement requisition restricted",
            RetentionStatus = "Current"
        });
    }

    private static CentralDocumentRepositoryRegistration Request(
        Guid tenantId,
        Guid actorId,
        Guid uploadId,
        bool includeChecksum = true) => new()
    {
        TenantId = tenantId,
        ActorUserId = actorId,
        ActorName = "Procurement Officer",
        FileUploadRecordId = uploadId,
        SourceModule = "Procurement",
        SourceLabel = "Procurement / Tender documents",
        SourceEntityType = "Tender",
        SourceRecordId = Guid.NewGuid(),
        SourceRecordReference = "TND-001",
        Title = "Specification.pdf",
        DocumentType = "TenderDocument",
        MetadataTemplateCode = "TDC-PROC-TENDER",
        AccessProfile = "Procurement tender restricted",
        RequirePublishedGovernance = true,
        MetadataValues = Metadata(includeChecksum)
    };

    private static IReadOnlyList<CentralDocumentMetadataRegistrationValue> Metadata(
        bool includeChecksum)
    {
        var values = new List<CentralDocumentMetadataRegistrationValue>
        {
            new("sourceReference", "Source reference", "TND-001"),
            new("documentFamily", "Document family", "Tender"),
            new("classification", "Classification", "Specification"),
            new("sourceStatus", "Source status", "Published"),
            new("uploadedBy", "Uploaded by", "Procurement Officer")
        };
        if (includeChecksum)
        {
            values.Add(new("checksumSha256", "Checksum SHA-256", new string('a', 64)));
        }

        return values;
    }

    private static void SeedGovernance(
        ApplicationDbContext db,
        Guid tenantId,
        Guid actorId,
        Guid uploadId,
        bool published)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Test tenant",
            Code = $"T{tenantId:N}"[..20]
        });
        db.Users.Add(new ApplicationUser
        {
            Id = actorId,
            TenantId = tenantId,
            UserName = "procurement.officer",
            NormalizedUserName = "PROCUREMENT.OFFICER",
            FirstName = "Procurement",
            LastName = "Officer"
        });
        db.FileUploadRecords.Add(new FileUploadRecord
        {
            Id = uploadId,
            TenantId = tenantId,
            Category = ControlledFileUploadCategories.DocumentManagement,
            FilePath = "private/document-management/specification.pdf",
            StoredFileName = "specification.pdf",
            OriginalFileName = "Specification.pdf",
            ContentType = "application/pdf",
            FileSize = 100,
            StorageProvider = "test",
            UploadedByUserId = actorId,
            VirusScanStatus = FileVirusScanStatus.Clean
        });
        db.CentralDocumentMetadataTemplates.Add(
            new ErpSystem.Core.Entities.DocumentManagement.CentralDocumentMetadataTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Module = "Procurement",
            DocumentType = "TenderDocument",
            TemplateCode = "TDC-PROC-TENDER",
            SourceLabel = "Procurement tender evidence",
            RequiredFieldsJson = "[\"sourceReference\",\"documentFamily\",\"classification\",\"sourceStatus\",\"uploadedBy\",\"checksumSha256\"]",
            RelationshipsJson = "[\"Tender\"]",
            RetentionRule = "TDC-PROC-RET-7Y",
            AccessProfile = "Procurement tender restricted",
            IsActive = true,
            PublishedAt = published ? DateTime.UtcNow : null
        });
        db.CentralDocumentAccessRules.Add(new CentralDocumentAccessRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccessProfile = "Procurement tender restricted",
            Module = "Procurement",
            PermissionKey = "procurement.records.read",
            CanView = true,
            IsActive = true
        });
        db.CentralDocumentRetentionPolicies.Add(new CentralDocumentRetentionPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PolicyCode = "TDC-PROC-RET-7Y",
            Name = "Procurement retention",
            Module = "Procurement",
            RetentionDays = 2555,
            RequiresLegalHoldReview = true,
            AllowArchive = true,
            AllowDestruction = false,
            IsActive = true
        });
    }

    private static ApplicationDbContext Database()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"procurement-central-dms-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
