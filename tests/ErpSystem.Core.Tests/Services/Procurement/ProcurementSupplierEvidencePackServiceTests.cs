using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Services;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementSupplierEvidencePackServiceTests
{
    [Fact]
    public async Task CreateIsTenantSafeAndReplaysTheSameCorrelationWithoutDuplicate()
    {
        await using var fixture = new Fixture();
        var request = fixture.GoodsRequest();

        var created = await fixture.Service.CreateAsync(request, "create-goods");
        var replay = await fixture.Service.CreateAsync(request, "create-goods");
        fixture.SwitchTenant(Guid.NewGuid());
        var foreign = await fixture.Service.SearchAsync(new());

        replay.Id.Should().Be(created.Id);
        created.Category.Should().Be(ProcurementSupplierRegistrationCategory.Goods);
        created.Requirements.Should().ContainSingle(item =>
            item.RequirementCode == "GRA-CLEARANCE" &&
            item.ValidityMode == ProcurementSupplierEvidenceValidityMode.MinimumRemainingDays &&
            item.ApprovalStepName == "Supplier evidence review");
        foreign.TotalCount.Should().Be(0);
        (await fixture.Context.ProcurementSupplierEvidencePackVersions.CountAsync())
            .Should().Be(1);
    }

    [Fact]
    public async Task WorksPackFailsClosedWithoutMandatoryClassification()
    {
        await using var fixture = new Fixture();
        var request = fixture.GoodsRequest();
        request.Category = ProcurementSupplierRegistrationCategory.Works;

        var action = () => fixture.Service.CreateAsync(request, "works-without-classification");

        await action.Should().ThrowAsync<ProcurementSupplierEvidencePackValidationException>()
            .Where(exception => exception.Code == "SUPPLIER_EVIDENCE_WORKS_CLASSIFICATION_REQUIRED");
        (await fixture.Context.ProcurementSupplierEvidencePackVersions.CountAsync())
            .Should().Be(0);
    }

    [Fact]
    public async Task FutureReplacementPublicationKeepsCurrentPackPublishedAndEffective()
    {
        await using var fixture = new Fixture();
        var current = fixture.PublishedPack(
            "TDC-GOODS", ProcurementSupplierRegistrationCategory.Goods,
            version: 1, effectiveFrom: DateTime.UtcNow.AddDays(-30));
        var replacement = fixture.PublishedPack(
            "TDC-GOODS", ProcurementSupplierRegistrationCategory.Goods,
            version: 2, effectiveFrom: DateTime.UtcNow.AddDays(30));
        replacement.PackKey = current.PackKey;
        replacement.Status = ProcurementSupplierEvidencePackStatus.PendingApproval;
        replacement.SupersedesVersionId = current.Id;
        replacement.ChangeSummary = "Future annual replacement.";
        replacement.SubmittedById = Guid.NewGuid();
        replacement.SubmittedAtUtc = DateTime.UtcNow;
        var workflowInstance = new WorkflowInstance
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            WorkflowDefinitionId = fixture.Workflow.Id,
            EntityTypeId = fixture.Workflow.EntityTypeId,
            EntityId = replacement.Id,
            Status = WorkflowInstanceStatus.Completed,
            InitiatedById = replacement.SubmittedById.Value
        };
        replacement.WorkflowInstanceId = workflowInstance.Id;
        fixture.Context.AddRange(current, replacement, workflowInstance);
        await fixture.Context.SaveChangesAsync();

        var published = await fixture.Service.PublishAsync(
            replacement.Id,
            new ProcurementSupplierEvidencePackLifecycleRequest
            {
                RowVersion = Convert.ToBase64String(replacement.RowVersion),
                Evidence =
                [
                    new ProcurementControlEventEvidenceReference
                    {
                        ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
                        Reference = "MINUTE-SUP-001"
                    }
                ]
            },
            "publish-future");

        published.Status.Should().Be(ProcurementSupplierEvidencePackStatus.Published);
        (await fixture.Context.ProcurementSupplierEvidencePackVersions
                .SingleAsync(item => item.Id == current.Id))
            .Status.Should().Be(ProcurementSupplierEvidencePackStatus.Published);
        (await fixture.Service.GetSummaryAsync()).EffectiveByCategory[
            ProcurementSupplierRegistrationCategory.Goods].Should().Be(1);
    }

    [Fact]
    public async Task RegistrationSubmissionReadinessRequiresExactCurrentAndClassifiedEvidenceThenBindsOnce()
    {
        await using var fixture = new Fixture();
        var pack = fixture.PublishedPack(
            "TDC-WORKS", ProcurementSupplierRegistrationCategory.Works,
            version: 1, effectiveFrom: DateTime.UtcNow.AddDays(-1),
            classification: true);
        var registration = new BusinessPartnerRegistration
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            RegistrationNumber = "REG-001",
            ApplicantName = "Works Applicant",
            ApplicantEmail = "works@example.test",
            ApplicantPhone = "0200000000",
            PartnerType = "Contractor",
            RegistrationCategory = ProcurementSupplierRegistrationCategory.Works,
            Status = "Draft",
            CreatedById = fixture.UserId
        };
        fixture.Context.AddRange(pack, registration);
        await fixture.Context.SaveChangesAsync();

        var incomplete = await fixture.Service.GetRegistrationReadinessAsync(registration.Id);
        incomplete.IsReady.Should().BeFalse();

        fixture.Context.BusinessPartnerRegistrationDocuments.Add(
            new BusinessPartnerRegistrationDocument
            {
                Id = Guid.NewGuid(),
                TenantId = fixture.TenantId,
                RegistrationId = registration.Id,
                DocumentType = "Works Classification Certificate",
                DocumentName = "works.pdf",
                DocumentPath = "evidence/works.pdf",
                FileSize = 1024,
                MimeType = "application/pdf",
                EvidenceRequirementCode = "WORKS-CLASS",
                ClassificationCode = "K1",
                IssuedAtUtc = DateTime.UtcNow.AddDays(-10),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(90),
                ChecksumSha256 = new string('a', 64)
            });
        await fixture.Context.SaveChangesAsync();

        var bound = await fixture.Service.BindAndValidateRegistrationAsync(
            registration.Id, fixture.UserId, "bind-registration");
        var replay = await fixture.Service.BindAndValidateRegistrationAsync(
            registration.Id, fixture.UserId, "bind-registration");

        bound.IsReady.Should().BeTrue();
        bound.IsBound.Should().BeTrue();
        replay.PackVersionId.Should().Be(pack.Id);
        (await fixture.Context.ProcurementSupplierRegistrationEvidencePackBindings.CountAsync())
            .Should().Be(1);
    }

    [Fact]
    public async Task ExternalApplicantCannotReadAnotherApplicantsReadiness()
    {
        await using var fixture = new Fixture();
        var registration = new BusinessPartnerRegistration
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            RegistrationNumber = "REG-FOREIGN",
            ApplicantName = "Other Applicant",
            PartnerType = "Supplier",
            RegistrationCategory = ProcurementSupplierRegistrationCategory.Goods,
            Status = "Draft",
            CreatedById = Guid.NewGuid()
        };
        fixture.Context.Add(registration);
        await fixture.Context.SaveChangesAsync();
        fixture.SetExternal(true);

        var action = () => fixture.Service.GetRegistrationReadinessAsync(registration.Id);

        await action.Should()
            .ThrowAsync<ProcurementSupplierEvidencePackAuthorizationException>();
    }

    [Fact]
    public async Task ApprovedSupplierOwnerCanReadEvidenceCreatedByApplicantIdentity()
    {
        await using var fixture = new Fixture();
        var partner = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            PartnerCode = "SUP-OWNER",
            PartnerName = "Approved Supplier",
            PartnerType = "Supplier",
            UserId = fixture.UserId
        };
        var registration = new BusinessPartnerRegistration
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            RegistrationNumber = "REG-APPROVED-OWNER",
            ApplicantName = partner.PartnerName,
            PartnerType = "Supplier",
            RegistrationCategory = ProcurementSupplierRegistrationCategory.Goods,
            Status = "Approved",
            CreatedById = Guid.NewGuid(),
            BusinessPartnerId = partner.Id
        };
        fixture.Context.AddRange(partner, registration);
        await fixture.Context.SaveChangesAsync();
        fixture.SetExternal(true);

        var readiness = await fixture.Service.GetRegistrationReadinessAsync(registration.Id);

        readiness.RegistrationId.Should().Be(registration.Id);
    }

    [Fact]
    public async Task ActiveSupplierDelegateCanReadLinkedApprovedRegistrationEvidence()
    {
        await using var fixture = new Fixture();
        var partner = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            PartnerCode = "SUP-DELEGATE",
            PartnerName = "Delegated Supplier",
            PartnerType = "Supplier",
            UserId = Guid.NewGuid()
        };
        var registration = new BusinessPartnerRegistration
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            RegistrationNumber = "REG-APPROVED-DELEGATE",
            ApplicantName = partner.PartnerName,
            PartnerType = "Supplier",
            RegistrationCategory = ProcurementSupplierRegistrationCategory.Goods,
            Status = "Approved",
            CreatedById = Guid.NewGuid(),
            BusinessPartnerId = partner.Id
        };
        var delegatedUser = new BusinessPartnerUser
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            BusinessPartnerId = partner.Id,
            UserId = fixture.UserId,
            Role = "User",
            IsActive = true
        };
        fixture.Context.AddRange(partner, registration, delegatedUser);
        await fixture.Context.SaveChangesAsync();
        fixture.SetExternal(true);

        var readiness = await fixture.Service.GetRegistrationReadinessAsync(registration.Id);

        readiness.RegistrationId.Should().Be(registration.Id);
    }

    [Fact]
    public async Task ExternalApplicantBindingUsesTenantSystemAuditPrincipal()
    {
        await using var fixture = new Fixture();
        var pack = fixture.PublishedPack(
            "TDC-GOODS", ProcurementSupplierRegistrationCategory.Goods,
            version: 1, effectiveFrom: DateTime.UtcNow.AddDays(-1));
        var registration = new BusinessPartnerRegistration
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            RegistrationNumber = "REG-EXTERNAL",
            ApplicantName = "External Applicant",
            PartnerType = "Supplier",
            RegistrationCategory = ProcurementSupplierRegistrationCategory.Goods,
            Status = "Draft",
            CreatedById = fixture.UserId
        };
        var document = new BusinessPartnerRegistrationDocument
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            RegistrationId = registration.Id,
            DocumentType = "Tax Clearance Certificate",
            DocumentName = "tax-clearance.pdf",
            DocumentPath = "evidence/tax-clearance.pdf",
            FileSize = 1024,
            MimeType = "application/pdf",
            EvidenceRequirementCode = "GRA-CLEARANCE",
            IssuedAtUtc = DateTime.UtcNow.AddDays(-10),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(90),
            ChecksumSha256 = new string('a', 64)
        };
        fixture.Context.AddRange(pack, registration, document);
        await fixture.Context.SaveChangesAsync();
        fixture.SetExternal(true);

        var result = await fixture.Service.BindAndValidateRegistrationAsync(
            registration.Id, fixture.UserId, "external-bind");

        result.IsBound.Should().BeTrue();
        fixture.ControlEvents.Verify(service => service.RecordSystemAsync(
            fixture.TenantId,
            "supplier-controls@tdc.test",
            It.Is<ProcurementControlEventWriteRequest>(request =>
                request.Action == "RegistrationPackBound" &&
                request.SourceId == registration.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.ControlEvents.Verify(service => service.RecordAsync(
            It.IsAny<ProcurementControlEventWriteRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Guid _tenantId;
        private bool _external;
        private readonly UnitOfWork _unitOfWork;

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            _tenantId = TenantId;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            Context.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Code = "TDC",
                Name = "TDC",
                Status = TenantStatus.Active
            });
            Profile = new ProcurementConfigurationProfile
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ProfileCode = "TDC-PROCUREMENT",
                Name = "TDC Procurement",
                Version = 1,
                LifecycleStatus = ProcurementConfigurationProfileStatus.Published,
                EffectiveFrom = DateTime.UtcNow.AddYears(-1),
                IsDefault = true
            };
            var entityType = new WorkflowEntityType
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = "SUPPLIER_REGISTRATION",
                Name = "Supplier Registration",
                IsActive = true
            };
            Workflow = new WorkflowDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                DefinitionKey = Guid.NewGuid(),
                Name = "Supplier evidence approval",
                EntityTypeId = entityType.Id,
                Version = 1,
                LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
                IsActive = true
            };
            Workflow.Steps.Add(new WorkflowStep
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                WorkflowDefinitionId = Workflow.Id,
                Name = "Supplier evidence review",
                Order = 1,
                StepType = WorkflowStepType.Approval,
                IsRequired = true
            });
            Context.AddRange(Profile, entityType, Workflow);
            Context.SaveChanges();

            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(item => item.TenantId).Returns(() => _tenantId);
            current.SetupGet(item => item.UserId).Returns(UserId);
            current.SetupGet(item => item.IsAuthenticated).Returns(true);
            current.SetupGet(item => item.IsExternalUser).Returns(() => _external);
            current.SetupGet(item => item.Username).Returns("supplier-controls@tdc.test");
            current.SetupGet(item => item.FullName).Returns("Supplier Controls");
            current.SetupGet(item => item.Roles).Returns(() =>
                _external ? ["External"] : ["TDC_PROCUREMENT_OFFICER"]);
            current.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns((string role) => !_external &&
                    string.Equals(role, "TDC_PROCUREMENT_OFFICER",
                        StringComparison.OrdinalIgnoreCase));

            _unitOfWork = new UnitOfWork(Context);
            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Message = "Allowed"
                });
            var sod = new Mock<IProcurementSodGuardService>();
            sod.Setup(item => item.EnforceAsync(
                    It.IsAny<ProcurementSodGuardRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementSodGuardDecisionDto { Allowed = true });
            var workflow = new Mock<IWorkflowInstanceService>();
            workflow.Setup(item => item.StartWorkflowAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                    It.IsAny<Guid>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid definitionId, Guid entityTypeId, string entityId,
                    Guid userId, object? _, CancellationToken _) =>
                    new WorkflowInstance
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        WorkflowDefinitionId = definitionId,
                        EntityTypeId = entityTypeId,
                        EntityId = Guid.Parse(entityId),
                        InitiatedById = userId,
                        Status = WorkflowInstanceStatus.InProgress
                    });
            ControlEvents = new Mock<IProcurementControlEventService>();
            ControlEvents.Setup(service => service.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementControlEventDto());
            ControlEvents.Setup(service => service.RecordSystemAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(),
                    It.IsAny<ProcurementControlEventWriteRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementControlEventDto());
            Service = new ProcurementSupplierEvidencePackService(
                _unitOfWork, current.Object, access.Object, sod.Object, ControlEvents.Object,
                workflow.Object, Mock.Of<INotificationTopicPublisher>(),
                NullLogger<ProcurementSupplierEvidencePackService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public ApplicationDbContext Context { get; }
        public ProcurementConfigurationProfile Profile { get; }
        public WorkflowDefinition Workflow { get; }
        public Mock<IProcurementControlEventService> ControlEvents { get; }
        public ProcurementSupplierEvidencePackService Service { get; }

        public void SwitchTenant(Guid tenantId) => _tenantId = tenantId;
        public void SetExternal(bool value) => _external = value;

        public SaveProcurementSupplierEvidencePackRequest GoodsRequest() => new()
        {
            PackCode = "TDC-GOODS",
            Name = "TDC Goods evidence",
            Category = ProcurementSupplierRegistrationCategory.Goods,
            EffectiveFromUtc = DateTime.UtcNow.AddDays(1),
            SourceConfigurationProfileId = Profile.Id,
            WorkflowDefinitionId = Workflow.Id,
            Requirements =
            [
                new SaveProcurementSupplierEvidenceRequirementRequest
                {
                    RequirementCode = "GRA-CLEARANCE",
                    Name = "GRA tax clearance",
                    Kind = ProcurementSupplierEvidenceRequirementKind.Document,
                    DocumentType = "Tax Clearance Certificate",
                    IsMandatory = true,
                    ValidityMode = ProcurementSupplierEvidenceValidityMode.MinimumRemainingDays,
                    MinimumRemainingDays = 30,
                    ApprovalStepOrder = 1,
                    ApprovalStepName = "Supplier evidence review",
                    AllowedMimeTypes = ["application/pdf"]
                }
            ]
        };

        public ProcurementSupplierEvidencePackVersion PublishedPack(
            string code,
            ProcurementSupplierRegistrationCategory category,
            int version,
            DateTime effectiveFrom,
            bool classification = false)
        {
            var pack = new ProcurementSupplierEvidencePackVersion
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PackKey = Guid.NewGuid(),
                PackCode = code,
                Name = code,
                Category = category,
                Version = version,
                Status = ProcurementSupplierEvidencePackStatus.Published,
                EffectiveFromUtc = effectiveFrom,
                SourceConfigurationProfileId = Profile.Id,
                SourceConfigurationProfileCode = Profile.ProfileCode,
                SourceConfigurationProfileVersion = Profile.Version,
                CreationCorrelationId = $"seed-{Guid.NewGuid():N}",
                LastOperation = "Published",
                LastOperationCorrelationId = $"publish-{Guid.NewGuid():N}",
                WorkflowDefinitionId = Workflow.Id,
                SubmittedById = Guid.NewGuid(),
                SubmittedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                PublishedById = Guid.NewGuid(),
                PublishedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                LifecycleSnapshotJson = "{}",
                IntegrityHash = Hash("{}"),
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            pack.Requirements.Add(new ProcurementSupplierEvidenceRequirement
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PackVersionId = pack.Id,
                RequirementCode = classification ? "WORKS-CLASS" : "GRA-CLEARANCE",
                Name = classification ? "Works classification" : "GRA tax clearance",
                Kind = classification
                    ? ProcurementSupplierEvidenceRequirementKind.DocumentAndClassification
                    : ProcurementSupplierEvidenceRequirementKind.Document,
                DocumentType = classification
                    ? "Works Classification Certificate"
                    : "Tax Clearance Certificate",
                IsMandatory = true,
                ClassificationScheme = classification ? "MWH" : null,
                AllowedClassificationsJson = classification ? "[\"K1\",\"K2\"]" : null,
                ValidityMode = ProcurementSupplierEvidenceValidityMode.MinimumRemainingDays,
                MinimumRemainingDays = 30,
                ApprovalStepOrder = 1,
                ApprovalStepName = "Supplier evidence review",
                MaxFileSizeBytes = 10 * 1024 * 1024,
                AllowedMimeTypesJson = "[\"application/pdf\"]",
                IntegrityHash = new string('b', 64)
            });
            return pack;
        }

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }

        private static string Hash(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
                .ToLowerInvariant();
    }
}
