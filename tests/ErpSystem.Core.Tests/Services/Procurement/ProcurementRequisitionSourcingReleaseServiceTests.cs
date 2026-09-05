using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Procurement;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementRequisitionSourcingReleaseServiceTests
{
    [Theory]
    [InlineData("status", "PR_NOT_APPROVED")]
    [InlineData("mandatory", "PR_SOURCING_FIELDS_INCOMPLETE")]
    [InlineData("commitment", "PR_BUDGET_COMMITMENT_RELEASED")]
    public async Task EachPrerequisiteFailsClosedWithStableActionableDecision(
        string blocker,
        string expectedCode)
    {
        await using var fixture = new Fixture();
        fixture.ApplyBlocker(blocker);
        await fixture.Context.SaveChangesAsync();

        var readiness = await fixture.Service.GetReadinessAsync(fixture.Requisition.Id);

        readiness.IsCompliant.Should().BeFalse();
        readiness.CanRelease.Should().BeFalse();
        readiness.IsReleased.Should().BeFalse();
        readiness.DecisionCode.Should().Be(expectedCode);
        readiness.RequiredActions.Should().NotBeEmpty();
        (await fixture.Context.ProcurementRequisitionSourcingReleases.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CompleteControlLineageCreatesOneImmutableIdempotentReleaseAndAuditHistory()
    {
        await using var fixture = new Fixture();

        var readiness = await fixture.Service.GetReadinessAsync(fixture.Requisition.Id);
        var first = await fixture.Service.ReleaseAsync(
            fixture.Requisition.Id, "Release the approved requisition to sourcing.", "trace-sourcing-release");
        var retry = await fixture.Service.ReleaseAsync(
            fixture.Requisition.Id, "Retry the same approved sourcing release.", "trace-sourcing-retry");

        readiness.IsCompliant.Should().BeTrue();
        readiness.CanRelease.Should().BeTrue();
        readiness.Requirements.Should().OnlyContain(item => item.Satisfied);
        first.Id.Should().Be(retry.Id);
        first.ReleaseReference.Should().Be("SRL-PR-SOURCING-0001-A1");
        first.ControlFingerprint.Should().HaveLength(64);
        first.IntegrityHash.Should().HaveLength(64);
        (await fixture.Context.ProcurementRequisitionSourcingReleases.CountAsync()).Should().Be(1);
        var stored = await fixture.Context.ProcurementRequisitionSourcingReleases.SingleAsync();
        stored.SnapshotJson.Should().Contain("tdc.pr-sourcing-release.v2");
        stored.BudgetCommitmentId.Should().Be(fixture.BudgetCommitmentId);
        stored.AuthorityRouteId.Should().Be(fixture.Route.Id);
        stored.AuthorityRouteReference.Should().Be(fixture.Route.RouteReference);
        stored.WorkflowInstanceId.Should().Be(fixture.Workflow.Id);
        var actions = await fixture.Context.ProcurementControlEvents
            .Where(item => item.SourceId == fixture.Requisition.Id)
            .OrderBy(item => item.OccurredAtUtc)
            .Select(item => item.Action)
            .ToListAsync();
        actions.Should().ContainInOrder("SourcingReleased", "SourcingReleaseReused");
        (await fixture.Service.GetHistoryAsync(fixture.Requisition.Id)).Should().ContainSingle();
    }

    [Fact]
    public async Task CapturedAuthorityRouteWithInvalidIntegrityFailsClosed()
    {
        await using var fixture = new Fixture();
        fixture.Route.IntegrityHash = new string('0', 64);
        await fixture.Context.SaveChangesAsync();

        var readiness = await fixture.Service.GetReadinessAsync(fixture.Requisition.Id);

        readiness.IsCompliant.Should().BeFalse();
        readiness.CanRelease.Should().BeFalse();
        readiness.DecisionCode.Should().Be("PR_SOURCING_AUTHORITY_ROUTE_INVALID");
        readiness.AuthorityRouteId.Should().BeNull();
        (await fixture.Context.ProcurementRequisitionSourcingReleases.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ApprovedOutcomeDoesNotRequireASecondAuthorityWorkflowGateAtSourcing()
    {
        await using var fixture = new Fixture();
        fixture.Workflow.Status = WorkflowInstanceStatus.InProgress;
        fixture.Workflow.CompletedDate = null;
        await fixture.Context.SaveChangesAsync();

        var release = await fixture.Service.ReleaseAsync(
            fixture.Requisition.Id, "Attempt sourcing before approval completes.", "trace-workflow-blocked");

        release.Should().NotBeNull();
        (await fixture.Context.ProcurementRequisitionSourcingReleases.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ReleaseDoesNotDuplicateTheMakerCheckerDecisionAlreadyEnforcedAtApproval()
    {
        await using var fixture = new Fixture();
        var step = new WorkflowStepInstance
        {
            TenantId = fixture.TenantId,
            WorkflowInstanceId = fixture.Workflow.Id,
            WorkflowStepId = Guid.NewGuid(),
            Status = WorkflowStepInstanceStatus.Completed,
            WorkflowInstance = fixture.Workflow
        };
        fixture.Context.WorkflowApprovals.Add(new WorkflowApproval
        {
            TenantId = fixture.TenantId,
            StepInstanceId = step.Id,
            StepInstance = step,
            Status = WorkflowApprovalStatus.Approved,
            ProcessedById = fixture.Requisition.RequestedById,
            ProcessedDate = DateTime.UtcNow
        });
        await fixture.Context.SaveChangesAsync();

        var readiness = await fixture.Service.GetReadinessAsync(fixture.Requisition.Id);

        readiness.IsCompliant.Should().BeTrue();
        readiness.Requirements.Should().NotContain(item => item.Key == "SOD");
    }

    [Fact]
    public async Task ChangedEligibleRequisitionAutomaticallyAppendsReleaseOnSourcingEntry()
    {
        await using var fixture = new Fixture();
        await fixture.Service.ReleaseAsync(
            fixture.Requisition.Id, "Release the initial approved control state.", "trace-initial-release");
        fixture.Item.Quantity = 3;
        fixture.Item.LineTotal = 300m;
        await fixture.Context.SaveChangesAsync();

        var readiness = await fixture.Service.GetReadinessAsync(fixture.Requisition.Id);
        var release = await fixture.Service.EnforceSourcingAsync(
            fixture.Requisition.Id, "Tender", "TND-STALE-001", "trace-stale-source");

        readiness.IsCompliant.Should().BeTrue();
        readiness.HasStaleRelease.Should().BeTrue();
        readiness.IsReleased.Should().BeFalse();
        readiness.CanRelease.Should().BeTrue();
        release.AttemptNumber.Should().Be(2);
        (await fixture.Context.ProcurementRequisitionSourcingReleases.CountAsync()).Should().Be(2);
        (await fixture.Context.ProcurementControlEvents.CountAsync(item =>
            item.SourceId == fixture.Requisition.Id && item.Action == "SourcingEntryAllowed")).Should().Be(1);
    }

    [Fact]
    public async Task EligibleSourcingEntryCreatesTheReleaseAuditRecordAutomatically()
    {
        await using var fixture = new Fixture();

        var release = await fixture.Service.EnforceSourcingAsync(
            fixture.Requisition.Id, "RequestForQuotation", "RFQ-AUTO-001", "trace-auto-source");

        release.AttemptNumber.Should().Be(1);
        release.ReleaseReason.Should().Be("System-generated release for RequestForQuotation RFQ-AUTO-001.");
        (await fixture.Context.ProcurementRequisitionSourcingReleases.CountAsync()).Should().Be(1);
        var actions = await fixture.Context.ProcurementControlEvents
            .Where(item => item.SourceId == fixture.Requisition.Id)
            .Select(item => item.Action)
            .ToListAsync();
        actions.Should().Contain(["SourcingReleased", "SourcingEntryAllowed"]);
    }

    [Fact]
    public async Task TamperedReleaseSnapshotFailsIntegrityRevalidation()
    {
        await using var fixture = new Fixture();
        await fixture.Service.ReleaseAsync(
            fixture.Requisition.Id, "Release the approved immutable snapshot.", "trace-integrity-release");
        var stored = await fixture.Context.ProcurementRequisitionSourcingReleases.SingleAsync();
        stored.SnapshotJson = "{\"tampered\":true}";
        await fixture.Context.SaveChangesAsync();

        var readiness = await fixture.Service.GetReadinessAsync(fixture.Requisition.Id);

        readiness.IsCompliant.Should().BeFalse();
        readiness.IsReleased.Should().BeFalse();
        readiness.DecisionCode.Should().Be("PR_SOURCING_RELEASE_INTEGRITY_INVALID");
        readiness.Requirements.Should().Contain(item => item.Key == "RELEASE_INTEGRITY" && !item.Satisfied);
    }

    [Fact]
    public async Task TenantIsolationAndReaderAuthorizationFailClosed()
    {
        await using var fixture = new Fixture();

        await fixture.Service.Invoking(service => service.GetReadinessAsync(Guid.NewGuid()))
            .Should().ThrowAsync<ProcurementRequisitionSourcingNotFoundException>()
            .Where(exception => exception.Code == "PR_NOT_FOUND");

        fixture.SwitchRoles();
        await fixture.Service.Invoking(service => service.GetReadinessAsync(fixture.Requisition.Id))
            .Should().ThrowAsync<ProcurementRequisitionSourcingAuthorizationException>();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private static readonly DateTime Moment = new(2026, 7, 22, 8, 0, 0, DateTimeKind.Utc);
        private readonly UnitOfWork _unitOfWork;
        private readonly Mock<ICurrentUserProvider> _currentUser = new();
        private readonly HashSet<string> _roles = new(StringComparer.OrdinalIgnoreCase)
        {
            "TDC_PROCUREMENT_OFFICER"
        };

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            AppSubmissionId = Guid.NewGuid();
            BudgetCommitmentId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            Context.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Code = "TDC",
                Name = "TDC",
                Status = TenantStatus.Active
            });

            var planId = Guid.NewGuid();
            var planItemId = Guid.NewGuid();
            Template = new ProcurementSpecificationTemplate
            {
                TenantId = TenantId,
                TemplateCode = "SPEC-GOODS",
                Name = "Goods specification",
                Kind = ProcurementSpecificationTemplateKind.Goods,
                Version = 2,
                Status = ProcurementSpecificationTemplateStatus.Published,
                EffectiveFromUtc = Moment.AddMonths(-1),
                PublishedAtUtc = Moment.AddDays(-2),
                Purpose = "Supply operational equipment.",
                FunctionalAndPerformanceRequirements = "Meet the stated performance levels.",
                ProcessAndMaterialsRequirements = "Use compliant materials and processes.",
                DimensionsAndMarkingRequirements = "Apply approved dimensions and markings.",
                TestingAndInspectionRequirements = "Complete inspection and acceptance tests.",
                ApplicableStandards = "Applicable Ghana standards.",
                Deliverables = "Equipment, manuals, and certificates.",
                AcceptanceCriteria = "All tests and inspections pass."
            };
            Requisition = new PurchaseRequisition
            {
                TenantId = TenantId,
                RequisitionNumber = "PR-SOURCING-0001",
                RequisitionDate = Moment.AddDays(-4),
                RequestedById = Guid.NewGuid(),
                RequiredDate = Moment.AddMonths(1),
                Status = "Approved",
                Department = "Operations",
                CostCenter = "OPS-001",
                Justification = "Approved operational requirement.",
                RequisitionType = PurchaseRequisitionType.StockReplenishment,
                BudgetId = Guid.NewGuid(),
                SourcePlanId = planId,
                SourcePlanItemId = planItemId,
                ProcurementCategory = ProcurementCategoryClass.Goods,
                SpecificationTemplateId = Template.Id,
                SpecificationTemplateCode = Template.TemplateCode,
                SpecificationTemplateName = Template.Name,
                SpecificationTemplateVersion = Template.Version,
                Currency = "GHS",
                TotalAmount = 200m,
                ApprovedAt = Moment.AddHours(-1),
                ApprovedById = Guid.NewGuid()
            };
            Item = new PurchaseRequisitionItem
            {
                TenantId = TenantId,
                RequisitionId = Requisition.Id,
                Requisition = Requisition,
                ItemDescription = "Operational equipment",
                Quantity = 2,
                UnitOfMeasure = "EA",
                EstimatedUnitPrice = 100m,
                LineTotal = 200m
            };
            Route = new ProcurementRequisitionAuthorityRoute
            {
                TenantId = TenantId,
                PurchaseRequisitionId = Requisition.Id,
                AttemptNumber = 1,
                RouteReference = "ARR-PR-SOURCING-0001-A1",
                Category = ProcurementCategoryClass.Goods,
                Amount = 200m,
                CurrencyCode = "GHS",
                PolicySetId = Guid.NewGuid(),
                PolicyCode = "TDC-SOURCING",
                PolicyVersion = 3,
                SourceConfigurationProfileId = Guid.NewGuid(),
                WorkflowDefinitionId = Guid.NewGuid(),
                WorkflowVersion = 1,
                CapturedAtUtc = Moment.AddHours(-2),
                SnapshotJson = "route-snapshot"
            };
            Route.IntegrityHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(Route.SnapshotJson))).ToLowerInvariant();
            var sourceProfile = new ProcurementConfigurationProfile
            {
                Id = Route.SourceConfigurationProfileId,
                TenantId = TenantId,
                ProfileCode = "TDC-SOURCING",
                Name = "TDC sourcing controls",
                Version = 3,
                LifecycleStatus = ProcurementConfigurationProfileStatus.Published,
                EffectiveFrom = Moment.AddMonths(-1),
                PublishedAt = Moment.AddDays(-2)
            };
            var routeStep = new ProcurementRequisitionAuthorityRouteStep
            {
                TenantId = TenantId,
                AuthorityRouteId = Route.Id,
                AuthorityRoute = Route,
                Sequence = 1,
                AuthorityRuleId = Guid.NewGuid(),
                RulePolicySetId = Route.PolicySetId,
                RulePolicyVersion = Route.PolicyVersion,
                RuleCode = "AUTH-SOURCING",
                SourceDecisionKey = "DEC-002",
                AuthorityRole = "TDC_HEAD_OF_PROCUREMENT",
                Quorum = 1,
                WorkflowDefinitionId = Route.WorkflowDefinitionId,
                WorkflowStepId = Guid.NewGuid(),
                WorkflowStepOrder = 1
            };
            Route.Steps.Add(routeStep);
            Workflow = new WorkflowInstance
            {
                TenantId = TenantId,
                WorkflowDefinitionId = Route.WorkflowDefinitionId,
                EntityId = Requisition.Id,
                EntityTypeId = Guid.NewGuid(),
                Status = WorkflowInstanceStatus.Completed,
                InitiatedById = Requisition.RequestedById,
                CompletedDate = Moment.AddHours(-1)
            };
            Context.AddRange(Template, Requisition, Item, sourceProfile, Route, Workflow);
            Context.SaveChanges();

            _currentUser.SetupGet(item => item.TenantId).Returns(TenantId);
            _currentUser.SetupGet(item => item.UserId).Returns(UserId);
            _currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            _currentUser.SetupGet(item => item.Username).Returns("sourcing.controller@tdc.test");
            _currentUser.SetupGet(item => item.FullName).Returns("Sourcing Controller");
            _currentUser.SetupGet(item => item.Roles).Returns(_roles);
            _currentUser.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns((string role) => _roles.Contains(role));

            SubmissionReadiness = new PurchaseRequisitionSubmissionReadinessDto
            {
                    RequisitionId = Requisition.Id,
                    RequisitionNumber = Requisition.RequisitionNumber,
                    Status = Requisition.Status,
                    IsCompliant = true,
                    CanSubmit = false,
                    DecisionCode = "PR_APP_ACKNOWLEDGED",
                    Message = "The current APP attempt is acknowledged.",
                    Basis = "AcknowledgedAPP",
                    SourcePlanId = planId,
                    SourcePlanItemId = planItemId,
                    AppSubmissionId = AppSubmissionId,
                    AppSubmissionAttemptNumber = 2,
                    AppSubmissionStatus = "Acknowledged",
                    AppAcknowledgementReference = "PPA-ACK-001",
                AppAcknowledgedAtUtc = Moment.AddDays(-1)
            };
            Submission.Setup(service => service.GetReadinessAsync(
                    Requisition.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(SubmissionReadiness);
            BudgetReadiness = new PurchaseRequisitionBudgetReadinessDto
            {
                    RequisitionId = Requisition.Id,
                    RequisitionNumber = Requisition.RequisitionNumber,
                    Status = Requisition.Status,
                    IsCompliant = true,
                    CanReserve = true,
                    DecisionCode = "PR_BUDGET_COMMITMENT_CURRENT",
                    Message = "The Finance commitment is reserved.",
                    CommitmentId = BudgetCommitmentId,
                    CommitmentReference = "BCR-PR-SOURCING-0001",
                    CommitmentStatus = ProcurementBudgetCommitmentStatus.Reserved.ToString(),
                    ReservationSequence = 1,
                ReservedAtUtc = Moment.AddHours(-3)
            };
            Budget.Setup(service => service.GetReadinessAsync(
                    Requisition.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(BudgetReadiness);
            Budget.Setup(service => service.GetLinkedControlReadinessAsync(
                    Requisition.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(BudgetReadiness);
            AuthorityReadiness = new PurchaseRequisitionAuthorityReadinessDto
            {
                    RequisitionId = Requisition.Id,
                    RequisitionNumber = Requisition.RequisitionNumber,
                    Status = Requisition.Status,
                    IsCompliant = true,
                    DecisionCode = "PR_AUTHORITY_ROUTE_CURRENT",
                    Message = "The authority route is current.",
                    Category = ProcurementCategoryClass.Goods,
                    Amount = Requisition.TotalAmount,
                    CurrencyCode = "GHS",
                    AuthorityRouteId = Route.Id,
                    RouteReference = Route.RouteReference,
                    WorkflowDefinitionId = Route.WorkflowDefinitionId,
                    CapturedAtUtc = Route.CapturedAtUtc,
                IntegrityHash = Route.IntegrityHash
            };
            Authority.Setup(service => service.GetReadinessAsync(
                    Requisition.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(AuthorityReadiness);
            PolicyDecision = new ProcurementAuthorityRouteDecisionDto
            {
                IsReady = true,
                DecisionCode = "PR_AUTHORITY_ROUTE_READY",
                Category = ProcurementCategoryClass.Goods,
                Amount = Requisition.TotalAmount,
                CurrencyCode = "GHS",
                Policy = new ProcurementCompliancePolicySelectionDto
                {
                    PolicySetId = Route.PolicySetId,
                    Version = Route.PolicyVersion,
                    SourceConfigurationProfileId = Route.SourceConfigurationProfileId
                },
                Workflow = new ProcurementAuthorityWorkflowSelectionDto
                {
                    WorkflowDefinitionId = Route.WorkflowDefinitionId,
                    Version = 1
                },
                Steps =
                [
                    new ProcurementAuthorityRouteStepDecisionDto
                    {
                        Sequence = routeStep.Sequence,
                        RuleId = routeStep.AuthorityRuleId,
                        RulePolicySetId = routeStep.RulePolicySetId,
                        RulePolicyVersion = routeStep.RulePolicyVersion,
                        RuleCode = routeStep.RuleCode,
                        SourceDecisionKey = routeStep.SourceDecisionKey,
                        AuthorityRole = routeStep.AuthorityRole,
                        Quorum = routeStep.Quorum,
                        WorkflowDefinitionId = routeStep.WorkflowDefinitionId,
                        WorkflowStepId = routeStep.WorkflowStepId,
                        WorkflowStepOrder = routeStep.WorkflowStepOrder
                    }
                ]
            };
            Compliance.Setup(service => service.EvaluateAuthorityRouteAsync(
                    It.IsAny<ProcurementAuthorityRouteDecisionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PolicyDecision);
            Access.Setup(service => service.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Code = "ACCESS_ALLOWED",
                    Message = "Allowed by the focused test fixture."
                });

            _unitOfWork = new UnitOfWork(Context);
            var events = new ProcurementControlEventService(
                _unitOfWork, _currentUser.Object, NullLogger<ProcurementControlEventService>.Instance);
            Service = new ProcurementRequisitionSourcingReleaseService(
                _unitOfWork,
                _currentUser.Object,
                Compliance.Object,
                Access.Object,
                events,
                Submission.Object,
                Budget.Object,
                Authority.Object,
                new ProcurementRequisitionSourcingReleaseStore(Context));
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public Guid AppSubmissionId { get; }
        public Guid BudgetCommitmentId { get; }
        public ApplicationDbContext Context { get; }
        public PurchaseRequisition Requisition { get; }
        public PurchaseRequisitionItem Item { get; }
        public ProcurementSpecificationTemplate Template { get; }
        public ProcurementRequisitionAuthorityRoute Route { get; }
        public WorkflowInstance Workflow { get; }
        public PurchaseRequisitionSubmissionReadinessDto SubmissionReadiness { get; }
        public PurchaseRequisitionBudgetReadinessDto BudgetReadiness { get; }
        public PurchaseRequisitionAuthorityReadinessDto AuthorityReadiness { get; }
        public ProcurementAuthorityRouteDecisionDto PolicyDecision { get; }
        public Mock<IProcurementRequisitionSubmissionControlService> Submission { get; } = new();
        public Mock<IProcurementRequisitionBudgetControlService> Budget { get; } = new();
        public Mock<IProcurementRequisitionAuthorityRouteService> Authority { get; } = new();
        public Mock<IProcurementComplianceDecisionService> Compliance { get; } = new();
        public Mock<IProcurementAccessControlService> Access { get; } = new();
        public ProcurementRequisitionSourcingReleaseService Service { get; }

        public void SwitchRoles(params string[] roles)
        {
            _roles.Clear();
            foreach (var role in roles) _roles.Add(role);
        }

        public void ApplyBlocker(string blocker)
        {
            switch (blocker)
            {
                case "status":
                    Requisition.Status = "Pending Approval";
                    Requisition.ApprovedAt = null;
                    Requisition.ApprovedById = null;
                    break;
                case "mandatory":
                    Requisition.Department = null;
                    break;
                case "plan":
                    SubmissionReadiness.SourcePlanItemId = Guid.NewGuid();
                    break;
                case "app":
                    SubmissionReadiness.IsCompliant = false;
                    SubmissionReadiness.DecisionCode = "PR_APP_OR_EXCEPTION_REQUIRED";
                    SubmissionReadiness.Message = "Acknowledged APP linkage or an approved exception is required.";
                    break;
                case "specification":
                    Template.ApplicableStandards = string.Empty;
                    break;
                case "commitment":
                    BudgetReadiness.IsCompliant = false;
                    BudgetReadiness.CanReserve = false;
                    BudgetReadiness.CommitmentStatus = ProcurementBudgetCommitmentStatus.Released.ToString();
                    BudgetReadiness.DecisionCode = "PR_BUDGET_COMMITMENT_RELEASED";
                    BudgetReadiness.Message = "The Finance commitment has been released.";
                    break;
                case "policy":
                    Compliance.Setup(service => service.EvaluateAuthorityRouteAsync(
                            It.IsAny<ProcurementAuthorityRouteDecisionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new ProcurementAuthorityRouteDecisionDto
                        {
                            IsReady = false,
                            DecisionCode = "PR_AUTHORITY_POLICY_NOT_FOUND",
                            Message = "The captured policy is no longer effective."
                        });
                    break;
                case "authority":
                    AuthorityReadiness.IsCompliant = false;
                    AuthorityReadiness.DecisionCode = "PR_AUTHORITY_ROUTE_MISSING";
                    AuthorityReadiness.Message = "The immutable authority route is missing.";
                    break;
                case "workflow":
                    Workflow.Status = WorkflowInstanceStatus.Cancelled;
                    Workflow.CompletedDate = null;
                    Workflow.CancelledDate = Moment;
                    break;
                case "evidence":
                    BudgetReadiness.IsOverride = true;
                    BudgetReadiness.OverrideApprovalReference = null;
                    BudgetReadiness.OverrideEvidenceReference = null;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(blocker), blocker, "Unknown test blocker.");
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }
}
