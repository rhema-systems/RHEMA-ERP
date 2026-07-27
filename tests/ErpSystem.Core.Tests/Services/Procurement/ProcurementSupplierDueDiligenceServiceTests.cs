using System.Text.Json;
using System.Text.Json.Serialization;
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

public sealed class ProcurementSupplierDueDiligenceServiceTests
{
    [Fact]
    public async Task CreateFailsClosedWithoutOneExactEffectiveDec011Policy()
    {
        await using var fixture = new Fixture(includePolicy: false);

        var action = () => fixture.Service.CreateAsync(
            fixture.CreateRequest(),
            "missing-dec011");

        await action.Should()
            .ThrowAsync<ProcurementSupplierDueDiligenceValidationException>()
            .Where(exception =>
                exception.Code == "SUPPLIER_DUE_DILIGENCE_POLICY_UNAVAILABLE");
        (await fixture.Context.ProcurementSupplierDueDiligenceReviews.CountAsync())
            .Should().Be(0);
    }

    [Fact]
    public async Task ExactSixCurrentEvidenceChecksCanCompleteIndependentApproval()
    {
        await using var fixture = new Fixture();
        var draft = await fixture.Service.CreateAsync(
            fixture.CreateRequest(),
            "create-review");
        var updated = await fixture.Service.UpdateAsync(
            draft.Id,
            new UpdateProcurementSupplierDueDiligenceRequest
            {
                RowVersion = draft.RowVersion,
                Notes = "All authoritative checks completed.",
                Checks = CompleteChecks()
            },
            "update-review");
        var submitted = await fixture.Service.SubmitAsync(
            draft.Id,
            Lifecycle(updated.RowVersion, "Submitted for independent review."),
            "submit-review");

        fixture.WorkflowInstance.Should().NotBeNull();
        fixture.WorkflowInstance!.Status = WorkflowInstanceStatus.Completed;
        await fixture.Context.SaveChangesAsync();
        fixture.SetUser(Guid.NewGuid());

        var approved = await fixture.Service.ApproveAsync(
            draft.Id,
            Lifecycle(submitted.RowVersion, "Shared workflow approved."),
            "approve-review");
        var current = await fixture.Service.GetCurrentStateAsync(
            fixture.Supplier.Id);

        approved.Status.Should().Be(ProcurementSupplierDueDiligenceStatus.Approved);
        approved.Outcome.Should().Be(ProcurementSupplierDueDiligenceOutcome.Clear);
        approved.Checks.Should().HaveCount(6);
        approved.Checks.Should().OnlyContain(check =>
            check.Status == ProcurementSupplierDueDiligenceCheckStatus.Clear &&
            check.Evidence.Count == 1);
        current.IsCurrent.Should().BeTrue();
        current.IsClear.Should().BeTrue();
        current.PolicyDecisionId.Should().Be(fixture.Policy!.Id);
        approved.IntegrityHash.Should().HaveLength(64);
    }

    [Fact]
    public async Task CompletedWorkflowCannotBeRecordedAsRejected()
    {
        await using var fixture = new Fixture();
        var draft = await fixture.Service.CreateAsync(
            fixture.CreateRequest(),
            "create-for-reject");
        var updated = await fixture.Service.UpdateAsync(
            draft.Id,
            new UpdateProcurementSupplierDueDiligenceRequest
            {
                RowVersion = draft.RowVersion,
                Checks = CompleteChecks()
            },
            "update-for-reject");
        var submitted = await fixture.Service.SubmitAsync(
            draft.Id,
            Lifecycle(updated.RowVersion, "Submitted."),
            "submit-for-reject");
        fixture.WorkflowInstance!.Status = WorkflowInstanceStatus.Completed;
        await fixture.Context.SaveChangesAsync();
        fixture.SetUser(Guid.NewGuid());

        var action = () => fixture.Service.RejectAsync(
            draft.Id,
            Lifecycle(submitted.RowVersion, "Attempted rejection."),
            "reject-completed");

        await action.Should()
            .ThrowAsync<ProcurementSupplierDueDiligenceConflictException>()
            .Where(exception =>
                exception.Code == "SUPPLIER_DUE_DILIGENCE_WORKFLOW_NOT_REJECTED");
    }

    [Fact]
    public async Task ReviewReadsDoNotDiscloseAcrossTenants()
    {
        await using var fixture = new Fixture();
        var draft = await fixture.Service.CreateAsync(
            fixture.CreateRequest(),
            "tenant-review");
        fixture.SetTenant(Guid.NewGuid());

        var action = () => fixture.Service.GetAsync(draft.Id);

        await action.Should()
            .ThrowAsync<ProcurementSupplierDueDiligenceNotFoundException>();
    }

    private static List<SaveProcurementSupplierDueDiligenceCheckRequest> CompleteChecks()
    {
        var now = DateTime.UtcNow;
        return Enum.GetValues<ProcurementSupplierDueDiligenceCheckType>()
            .Select(type => new SaveProcurementSupplierDueDiligenceCheckRequest
            {
                CheckType = type,
                Status = ProcurementSupplierDueDiligenceCheckStatus.Clear,
                SourceName = $"{type} authoritative source",
                SourceReference = $"{type}-2026-001",
                CheckedAtUtc = now.AddDays(-1),
                ValidUntilUtc = now.AddMonths(6),
                Notes = "Reviewed against retained source evidence.",
                Evidence =
                [
                    new ProcurementControlEventEvidenceReference
                    {
                        ReferenceKind =
                            ProcurementControlEvidenceReferenceKind.ExternalReference,
                        Reference = $"{type}-EVIDENCE-001",
                        Label = $"{type} reviewer evidence",
                        RequirementKey = type.ToString()
                    }
                ]
            })
            .ToList();
    }

    private static ProcurementSupplierDueDiligenceLifecycleRequest Lifecycle(
        string rowVersion,
        string comment) =>
        new()
        {
            RowVersion = rowVersion,
            Comment = comment,
            Evidence =
            [
                new ProcurementControlEventEvidenceReference
                {
                    ReferenceKind =
                        ProcurementControlEvidenceReferenceKind.ExternalReference,
                    Reference = $"MINUTE-{Guid.NewGuid():N}",
                    Label = "Lifecycle minute",
                    RequirementKey = "SupplierDueDiligence.Lifecycle"
                }
            ]
        };

    private sealed class Fixture : IAsyncDisposable
    {
        private Guid _tenantId;
        private Guid _userId;
        private readonly UnitOfWork _unitOfWork;

        public Fixture(bool includePolicy = true)
        {
            TenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            _tenantId = TenantId;
            _userId = UserId;
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
            Supplier = new BusinessPartner
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PartnerCode = "SUP-001",
                PartnerName = "Controlled Supplier",
                PartnerType = "Supplier",
                ApprovalStatus = "Approved",
                RegistrationStatus = "Approved",
                IsActive = true
            };
            Workflow = new WorkflowDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                DefinitionKey = Guid.NewGuid(),
                Name = "Supplier due-diligence approval",
                EntityTypeId = Guid.NewGuid(),
                Version = 1,
                LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
                IsActive = true
            };
            Context.AddRange(Supplier, Workflow);

            if (includePolicy)
            {
                var profile = new ProcurementConfigurationProfile
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    ProfileKey = Guid.NewGuid(),
                    ProfileCode = "TDC-PROCUREMENT",
                    Name = "TDC Procurement",
                    Version = 1,
                    LifecycleStatus = ProcurementConfigurationProfileStatus.Published,
                    EffectiveFrom = DateTime.UtcNow.AddDays(-30),
                    IsDefault = true
                };
                var value = new ProcurementSupplierRiskDecisionValueDto
                {
                    EffectiveFrom = DateTime.UtcNow.AddDays(-30),
                    ReviewFrequencyMonths = 12,
                    ExposureWindowMonths = 12,
                    RiskDimensions =
                    [
                        "Compliance=40",
                        "DueDiligence=60"
                    ],
                    RiskBands = ["High=0-70", "Low=70-100"],
                    ConcentrationLimitPercent = 25,
                    MinimumScore = 70,
                    EligibilityAction =
                        ProcurementSupplierRiskEligibilityAction.EscalationRequired,
                    PerformanceWindowMonths = 12,
                    PerformanceDimensions =
                    [
                        "DeliveryTimeliness=15",
                        "GrnQuality=15",
                        "RejectionRate=15",
                        "PriceCompetitiveness=15",
                        "Responsiveness=10",
                        "ComplaintResolution=10",
                        "ContractCompletion=20"
                    ],
                    PerformanceBands =
                    [
                        "Unsatisfactory=0-50",
                        "ImprovementRequired=50-75",
                        "Satisfactory=75-100"
                    ],
                    MinimumPerformanceDataCoveragePercent = 60,
                    ResponseTargetHours = 48,
                    PerformanceEligibilityAction =
                        ProcurementSupplierRiskEligibilityAction.AwardHardStop
                };
                var validated = ProcurementConfigurationDecisionRegistry.Validate(
                    "DEC-011",
                    1,
                    JsonSerializer.SerializeToElement(value, new JsonSerializerOptions
                    {
                        Converters =
                        {
                            new JsonStringEnumConverter(
                                JsonNamingPolicy.CamelCase, allowIntegerValues: false)
                        }
                    }));
                validated.IsValid.Should().BeTrue(
                    string.Join("; ", validated.Errors));
                Policy = new ProcurementConfigurationDecision
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    ProfileId = profile.Id,
                    DecisionKey = "DEC-011",
                    SchemaVersion = 1,
                    OwnerGroup = "Procurement",
                    Status = ProcurementConfigurationDecisionStatus.Approved,
                    ApprovalStatus = ProcurementConfigurationApprovalStatus.Approved,
                    EvidenceStatus = ProcurementConfigurationEvidenceStatus.Verified,
                    ValueJson = validated.CanonicalJson!,
                    EffectiveFrom = value.EffectiveFrom,
                    ApprovedById = Guid.NewGuid(),
                    ApprovedAt = DateTime.UtcNow.AddDays(-2)
                };
                profile.Decisions.Add(Policy);
                Context.Add(profile);
            }
            Context.SaveChanges();

            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(item => item.TenantId).Returns(() => _tenantId);
            current.SetupGet(item => item.UserId).Returns(() => _userId);
            current.SetupGet(item => item.IsAuthenticated).Returns(true);
            current.SetupGet(item => item.IsExternalUser).Returns(false);
            current.SetupGet(item => item.Username).Returns("supplier.controls");
            current.SetupGet(item => item.FullName).Returns("Supplier Controls");
            current.SetupGet(item => item.Roles).Returns(["TenantAdmin"]);
            current.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns((string role) => role == "TenantAdmin");

            _unitOfWork = new UnitOfWork(Context);
            var access = new Mock<IProcurementAccessControlService>();
            var sod = new Mock<IProcurementSodGuardService>();
            sod.Setup(item => item.EnforceAsync(
                    It.IsAny<ProcurementSodGuardRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    ProcurementSodGuardRequest request,
                    string _,
                    CancellationToken _) => Task.FromResult(
                    new ProcurementSodGuardDecisionDto
                    {
                        Allowed = !request.ProhibitedActorUserIds.Contains(_userId)
                    }));
            var workflow = new Mock<IWorkflowInstanceService>();
            workflow.Setup(item => item.StartWorkflowAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<object?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (
                    Guid definitionId,
                    Guid entityTypeId,
                    string entityId,
                    Guid startedById,
                    object? _,
                    CancellationToken cancellationToken) =>
                {
                    WorkflowInstance = new WorkflowInstance
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        WorkflowDefinitionId = definitionId,
                        EntityTypeId = entityTypeId,
                        EntityId = Guid.Parse(entityId),
                        InitiatedById = startedById,
                        Status = WorkflowInstanceStatus.InProgress
                    };
                    Context.Add(WorkflowInstance);
                    await Context.SaveChangesAsync(cancellationToken);
                    return WorkflowInstance;
                });
            var notifications = new Mock<INotificationTopicPublisher>();
            notifications.Setup(item => item.PublishAsync(
                    It.IsAny<NotificationTopicEvent>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var events = new ProcurementControlEventService(
                _unitOfWork,
                current.Object,
                NullLogger<ProcurementControlEventService>.Instance);

            Service = new ProcurementSupplierDueDiligenceService(
                _unitOfWork,
                current.Object,
                access.Object,
                sod.Object,
                events,
                workflow.Object,
                notifications.Object,
                NullLogger<ProcurementSupplierDueDiligenceService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public ApplicationDbContext Context { get; }
        public BusinessPartner Supplier { get; }
        public WorkflowDefinition Workflow { get; }
        public ProcurementConfigurationDecision? Policy { get; }
        public WorkflowInstance? WorkflowInstance { get; private set; }
        public ProcurementSupplierDueDiligenceService Service { get; }

        public CreateProcurementSupplierDueDiligenceRequest CreateRequest() =>
            new()
            {
                BusinessPartnerId = Supplier.Id,
                ReviewType = ProcurementSupplierDueDiligenceReviewType.Initial,
                WorkflowDefinitionId = Workflow.Id,
                Notes = "Initial governed review."
            };

        public void SetTenant(Guid tenantId) => _tenantId = tenantId;
        public void SetUser(Guid userId) => _userId = userId;

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }
}
