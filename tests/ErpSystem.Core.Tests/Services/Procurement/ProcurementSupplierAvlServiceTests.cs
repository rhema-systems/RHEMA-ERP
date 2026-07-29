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

public sealed class ProcurementSupplierAvlServiceTests
{
    [Fact]
    public async Task CreateFailsClosedWithoutOneExactEffectiveDec011Policy()
    {
        await using var fixture = new Fixture(includePolicy: false);

        var action = () => fixture.Service.CreateAsync(
            fixture.CreateRequest(DateTime.UtcNow.AddDays(-1)),
            "missing-avl-policy");

        await action.Should()
            .ThrowAsync<ProcurementSupplierAvlValidationException>()
            .Where(exception => exception.Code == "SUPPLIER_AVL_POLICY_UNAVAILABLE");
        (await fixture.Context.ProcurementSupplierAvlRegisters.CountAsync())
            .Should().Be(0);
    }

    [Fact]
    public async Task CompletedWorkflowPublishesAnnualRegisterAndSupportsEvidencedStatusHistory()
    {
        await using var fixture = new Fixture();
        var published = await fixture.PublishAsync(
            DateTime.UtcNow.AddDays(-1), "annual");

        fixture.LastEligibilityRequest.Should().NotBeNull();
        fixture.LastEligibilityRequest!.SkipFormalAvlMembership.Should().BeTrue();
        published.Status.Should().Be(ProcurementSupplierAvlRegisterStatus.Published);
        published.Entries.Should().ContainSingle(
            "a published AVL must retain its approved supplier entry");
        published.PublicationSnapshots.Should().ContainSingle(
            "publication must append an immutable publication snapshot");
        published.IntegrityHash.Should().HaveLength(64);

        var entry = published.Entries.Single();
        var suspended = await fixture.Service.SuspendEntryAsync(
            published.Id,
            entry.Id,
            EntryLifecycle(entry.RowVersion, "Sanctions status requires review."),
            "suspend-supplier");
        var suspendedEntry = suspended.Entries.Single();

        suspendedEntry.Status.Should().Be(ProcurementSupplierAvlEntryStatus.Suspended);
        suspendedEntry.StatusHistory.Should().ContainSingle(item =>
                item.Action == ProcurementSupplierAvlEntryAction.Suspended,
            "suspension must append an evidenced status-history row");

        var reinstated = await fixture.Service.ReinstateEntryAsync(
            published.Id,
            entry.Id,
            EntryLifecycle(suspendedEntry.RowVersion, "Clear revalidation evidence retained."),
            "reinstate-supplier");

        reinstated.Entries.Single().Status.Should()
            .Be(ProcurementSupplierAvlEntryStatus.Active);
        reinstated.Entries.Single().StatusHistory.Should().HaveCount(2);
    }

    [Fact]
    public async Task FutureReplacementKeepsCurrentPublicationUntilReplacementEffectiveDate()
    {
        await using var fixture = new Fixture();
        var current = await fixture.PublishAsync(
            DateTime.UtcNow.AddDays(-2), "current");
        var replacementEffective = DateTime.UtcNow.AddDays(10);
        var replacement = await fixture.PublishAsync(
            replacementEffective, "replacement");

        replacement.Version.Should().Be(current.Version + 1);
        replacement.Status.Should().Be(ProcurementSupplierAvlRegisterStatus.Published);
        var retainedCurrent = await fixture.Service.GetAsync(current.Id);
        retainedCurrent.Status.Should().Be(ProcurementSupplierAvlRegisterStatus.Published);
        retainedCurrent.ScheduledRetirementAtUtc.Should().Be(replacementEffective);

        var state = await fixture.Service.GetCurrentStateAsync(fixture.Supplier.Id);
        state.IsCurrent.Should().BeTrue();
        state.RegisterId.Should().Be(current.Id);
    }

    [Fact]
    public async Task CompletedWorkflowCannotBeRecordedAsRejected()
    {
        await using var fixture = new Fixture();
        var draft = await fixture.CreateAndAddAsync(
            DateTime.UtcNow.AddDays(-1), "reject");
        var submitted = await fixture.Service.SubmitAsync(
            draft.Id,
            Lifecycle(draft.RowVersion, "Submitted."),
            "submit-reject");
        fixture.WorkflowInstance!.Status = WorkflowInstanceStatus.Completed;
        await fixture.Context.SaveChangesAsync();
        fixture.SetUser(Guid.NewGuid());

        var action = () => fixture.Service.RejectAsync(
            draft.Id,
            Lifecycle(submitted.RowVersion, "Attempted rejection."),
            "reject-completed");

        await action.Should()
            .ThrowAsync<ProcurementSupplierAvlConflictException>()
            .Where(exception => exception.Code == "SUPPLIER_AVL_WORKFLOW_NOT_REJECTED");
    }

    [Fact]
    public async Task RegisterReadsDoNotDiscloseAcrossTenants()
    {
        await using var fixture = new Fixture();
        var draft = await fixture.Service.CreateAsync(
            fixture.CreateRequest(DateTime.UtcNow.AddDays(-1)),
            "tenant-avl");
        fixture.SetTenant(Guid.NewGuid());

        var action = () => fixture.Service.GetAsync(draft.Id);

        await action.Should().ThrowAsync<ProcurementSupplierAvlNotFoundException>();
    }

    private static ProcurementSupplierAvlLifecycleRequest Lifecycle(
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
                    Reference = $"AVL-MINUTE-{Guid.NewGuid():N}",
                    Label = "AVL lifecycle minute",
                    RequirementKey = "SupplierAVL.Lifecycle"
                }
            ]
        };

    private static ProcurementSupplierAvlEntryLifecycleRequest EntryLifecycle(
        string rowVersion,
        string reason) =>
        new()
        {
            RowVersion = rowVersion,
            Reason = reason,
            Evidence =
            [
                new ProcurementControlEventEvidenceReference
                {
                    ReferenceKind =
                        ProcurementControlEvidenceReferenceKind.ExternalReference,
                    Reference = $"AVL-STATUS-{Guid.NewGuid():N}",
                    Label = "AVL supplier status evidence",
                    RequirementKey = "SupplierAVL.EntryStatus"
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
                PartnerCode = "SUP-AVL-001",
                PartnerName = "Controlled AVL Supplier",
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
                Name = "Supplier AVL approval",
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
            current.SetupGet(item => item.Username).Returns("supplier.avl");
            current.SetupGet(item => item.FullName).Returns("Supplier AVL");
            current.SetupGet(item => item.Roles).Returns(["TenantAdmin"]);
            current.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns((string role) => role == "TenantAdmin");

            var supplierValidation = new Mock<ISupplierValidationService>();
            supplierValidation.Setup(item => item.EvaluateEligibilityAsync(
                    It.IsAny<SupplierEligibilityEvaluationRequest>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    SupplierEligibilityEvaluationRequest request,
                    CancellationToken _) =>
                {
                    LastEligibilityRequest = request;
                    return Task.FromResult(new SupplierValidationResult
                    {
                        IsValid = true,
                        ValidationCode = "OK",
                        TenantId = TenantId,
                        BusinessPartnerId = Supplier.Id,
                        PartnerCode = Supplier.PartnerCode,
                        PartnerName = Supplier.PartnerName,
                        PartnerType = Supplier.PartnerType,
                        Boundary = request.Boundary,
                        EvaluatedAtUtc = DateTime.UtcNow,
                        DueDiligenceCurrent = true,
                        DueDiligenceReviewId = DueDiligenceReviewId,
                        DecisionHash = new string('A', 64),
                        DecisionKeys = Enumerable.Range(1, 14)
                            .Select(number => $"DEC-{number:000}").ToList()
                    });
                });

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

            Service = new ProcurementSupplierAvlService(
                _unitOfWork,
                current.Object,
                supplierValidation.Object,
                access.Object,
                sod.Object,
                events,
                workflow.Object,
                notifications.Object,
                NullLogger<ProcurementSupplierAvlService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public Guid DueDiligenceReviewId { get; } = Guid.NewGuid();
        public ApplicationDbContext Context { get; }
        public BusinessPartner Supplier { get; }
        public WorkflowDefinition Workflow { get; }
        public ProcurementConfigurationDecision? Policy { get; }
        public WorkflowInstance? WorkflowInstance { get; private set; }
        public SupplierEligibilityEvaluationRequest? LastEligibilityRequest { get; private set; }
        public ProcurementSupplierAvlService Service { get; }

        public CreateProcurementSupplierAvlRequest CreateRequest(DateTime effectiveFrom) =>
            new()
            {
                ReviewYear = effectiveFrom.Year,
                EffectiveFromUtc = effectiveFrom,
                WorkflowDefinitionId = Workflow.Id,
                Notes = "Annual governed approved vendor review."
            };

        public async Task<ProcurementSupplierAvlDto> CreateAndAddAsync(
            DateTime effectiveFrom,
            string suffix)
        {
            var draft = await Service.CreateAsync(
                CreateRequest(effectiveFrom),
                $"create-{suffix}");
            return await Service.AddEntryAsync(
                draft.Id,
                new AddProcurementSupplierAvlEntryRequest
                {
                    BusinessPartnerId = Supplier.Id,
                    RegisterRowVersion = draft.RowVersion
                },
                $"add-{suffix}");
        }

        public async Task<ProcurementSupplierAvlDto> PublishAsync(
            DateTime effectiveFrom,
            string suffix)
        {
            var draft = await CreateAndAddAsync(effectiveFrom, suffix);
            var submitted = await Service.SubmitAsync(
                draft.Id,
                Lifecycle(draft.RowVersion, "Submitted for independent review."),
                $"submit-{suffix}");
            WorkflowInstance!.Status = WorkflowInstanceStatus.Completed;
            await Context.SaveChangesAsync();
            SetUser(Guid.NewGuid());
            var approved = await Service.ApproveAsync(
                draft.Id,
                Lifecycle(submitted.RowVersion, "Shared workflow approved."),
                $"approve-{suffix}");
            return await Service.PublishAsync(
                draft.Id,
                Lifecycle(approved.RowVersion, "Published with retained evidence."),
                $"publish-{suffix}");
        }

        public void SetTenant(Guid tenantId) => _tenantId = tenantId;
        public void SetUser(Guid userId) => _userId = userId;

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }
}
