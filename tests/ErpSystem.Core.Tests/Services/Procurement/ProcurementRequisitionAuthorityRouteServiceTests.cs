using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
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

public sealed class ProcurementRequisitionAuthorityRouteServiceTests
{
    [Fact]
    public async Task ReadyDecisionCapturesImmutablePolicyWorkflowAndRuleSnapshotWithAuditHistory()
    {
        await using var fixture = new Fixture();
        var seed = fixture.SeedRouteContext();
        await fixture.Context.SaveChangesAsync();
        fixture.Compliance.Setup(service => service.EvaluateAuthorityRouteAsync(
                It.IsAny<ProcurementAuthorityRouteDecisionRequest>(), "trace-authority-capture", It.IsAny<CancellationToken>()))
            .ReturnsAsync(seed.Decision);

        var decision = await fixture.Service.EnforceSubmissionAsync(
            seed.Requisition, "trace-authority-capture");
        var route = await fixture.Service.CaptureAsync(
            seed.Requisition, decision, "trace-authority-capture");
        await fixture.Context.SaveChangesAsync();

        route.AttemptNumber.Should().Be(1);
        route.RouteReference.Should().Be($"ARR-{seed.Requisition.RequisitionNumber}-A1");
        route.PolicySetId.Should().Be(seed.Policy.Id);
        route.WorkflowDefinitionId.Should().Be(seed.Workflow.Id);
        route.IntegrityHash.Should().HaveLength(64);
        route.SnapshotJson.Should().Contain("tdc.pr-authority-route.v1");
        route.Steps.Should().ContainSingle(item =>
            item.AuthorityRuleId == seed.AuthorityRule.Id &&
            item.WorkflowStepId == seed.WorkflowStep.Id &&
            item.SourceDecisionKey == "DEC-002");

        seed.Policy.Name = "Changed source policy name";
        seed.Workflow.Name = "Changed source workflow name";
        seed.AuthorityRule.AuthorityName = "Changed source authority";
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        var history = await fixture.Service.GetHistoryAsync(seed.Requisition.Id);
        history.Should().ContainSingle();
        history[0].PolicyName.Should().Be("TDC procurement policy");
        history[0].WorkflowName.Should().Be("TDC Purchase Requisition Approval");
        history[0].Steps.Should().ContainSingle(item => item.AuthorityName == "Entity Tender Committee");
        var events = await fixture.Context.ProcurementControlEvents
            .Where(item => item.SourceId == seed.Requisition.Id)
            .OrderBy(item => item.OccurredAtUtc)
            .ToListAsync();
        events.Select(item => item.Action).Should().Contain(["AuthorityRouteResolved", "AuthorityRouteCaptured"]);
        events.Should().OnlyContain(item => item.IntegrityHash.Length == 64);
    }

    [Fact]
    public async Task BlockedEvaluationStopsBeforeCaptureAndRecordsDeniedControlEvidence()
    {
        await using var fixture = new Fixture();
        var seed = fixture.SeedRouteContext();
        await fixture.Context.SaveChangesAsync();
        var blocked = new ProcurementAuthorityRouteDecisionDto
        {
            EvaluationId = Guid.NewGuid(),
            EvaluatedAtUtc = seed.Decision.EvaluatedAtUtc,
            PolicyDateUtc = seed.Decision.PolicyDateUtc,
            CorrelationId = "trace-authority-blocked",
            IsReady = false,
            DecisionCode = "PR_AUTHORITY_COVERAGE_GAP",
            Message = "No amount band covers the requisition.",
            Policy = seed.Decision.Policy,
            Category = seed.Decision.Category,
            Amount = seed.Decision.Amount,
            CurrencyCode = seed.Decision.CurrencyCode,
            Workflow = null,
            Steps = Array.Empty<ProcurementAuthorityRouteStepDecisionDto>(),
            Findings =
            [
                new ProcurementComplianceFindingDto
                {
                    Code = "PR_AUTHORITY_COVERAGE_GAP",
                    Message = "No amount band covers the requisition.",
                    Severity = ProcurementComplianceFindingSeverity.HardStop
                }
            ]
        };
        fixture.Compliance.Setup(service => service.EvaluateAuthorityRouteAsync(
                It.IsAny<ProcurementAuthorityRouteDecisionRequest>(), "trace-authority-blocked", It.IsAny<CancellationToken>()))
            .ReturnsAsync(blocked);

        var action = () => fixture.Service.EnforceSubmissionAsync(
            seed.Requisition, "trace-authority-blocked");

        var exception = await action.Should().ThrowAsync<ProcurementRequisitionAuthorityBlockedException>();
        exception.Which.Readiness.DecisionCode.Should().Be("PR_AUTHORITY_COVERAGE_GAP");
        (await fixture.Context.ProcurementRequisitionAuthorityRoutes.CountAsync()).Should().Be(0);
        var audit = await fixture.Context.ProcurementControlEvents.SingleAsync(
            item => item.SourceId == seed.Requisition.Id);
        audit.Action.Should().Be("AuthorityRouteBlocked");
        audit.Result.Should().Be(ProcurementControlEventResult.Denied);
        audit.IntegrityHash.Should().HaveLength(64);
    }

    [Fact]
    public async Task ApprovalGuardRequiresCapturedWorkflowAndIndependentApprover()
    {
        await using var fixture = new Fixture();
        var seed = fixture.SeedRouteContext();
        await fixture.Context.SaveChangesAsync();
        await fixture.CaptureAsync(seed, "trace-authority-approval-seed");
        seed.Requisition.Status = "Pending Approval";
        fixture.Context.WorkflowInstances.Add(new WorkflowInstance
        {
            TenantId = fixture.TenantId,
            WorkflowDefinitionId = seed.Workflow.Id,
            EntityId = seed.Requisition.Id,
            EntityTypeId = seed.EntityType.Id,
            Status = WorkflowInstanceStatus.InProgress,
            InitiatedById = seed.Requisition.RequestedById
        });
        await fixture.Context.SaveChangesAsync();
        fixture.Workflow.Setup(service => service.GetCurrentWorkflowStepAsync(
                "PurchaseRequisition", seed.Requisition.Id))
            .ReturnsAsync(new WorkflowStepInfo { StepName = seed.WorkflowStep.Name, Status = "Pending", StepOrder = 10 });
        fixture.Sod.Setup(service => service.EnforceAsync(
                It.Is<ProcurementSodGuardRequest>(request =>
                    request.ControlCode == "SOD-INITIATOR-APPROVER" &&
                    request.ProhibitedActorUserIds.Contains(seed.Requisition.RequestedById)),
                "trace-authority-approval", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSodGuardDecisionDto
            {
                Allowed = true,
                Code = "SOD_ALLOWED",
                Message = "Independent approver confirmed."
            });

        var readiness = await fixture.Service.EnforceApprovalAsync(
            seed.Requisition, "trace-authority-approval");

        readiness.IsCompliant.Should().BeTrue();
        readiness.DecisionCode.Should().Be("PR_AUTHORITY_APPROVAL_ALLOWED");
        readiness.CurrentWorkflowStage.Should().Be(seed.WorkflowStep.Name);
        readiness.WorkflowDefinitionId.Should().Be(seed.Workflow.Id);
        fixture.Sod.Verify(service => service.EnforceAsync(
            It.Is<ProcurementSodGuardRequest>(request =>
                request.ControlCode == "SOD-INITIATOR-APPROVER" &&
                request.ProhibitedActorUserIds.Contains(seed.Requisition.RequestedById)),
            "trace-authority-approval", It.IsAny<CancellationToken>()), Times.Once);

        fixture.Sod.Setup(service => service.EnforceAsync(
                It.IsAny<ProcurementSodGuardRequest>(), "trace-authority-sod-blocked", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSodGuardDecisionDto
            {
                Allowed = false,
                IsHardStop = true,
                Code = "SOD_INITIATOR_APPROVER_CONFLICT",
                Message = "The requisition initiator cannot approve this requisition."
            });
        var blocked = () => fixture.Service.EnforceApprovalAsync(
            seed.Requisition, "trace-authority-sod-blocked");
        (await blocked.Should().ThrowAsync<ProcurementRequisitionAuthorityBlockedException>())
            .Which.Readiness.DecisionCode.Should().Be("SOD_INITIATOR_APPROVER_CONFLICT");
    }

    [Fact]
    public async Task ApprovalGuardFailsClosedWhenActiveWorkflowDoesNotMatchCapturedDefinition()
    {
        await using var fixture = new Fixture();
        var seed = fixture.SeedRouteContext();
        await fixture.Context.SaveChangesAsync();
        await fixture.CaptureAsync(seed, "trace-authority-mismatch-seed");
        seed.Requisition.Status = "Pending Approval";
        fixture.Context.WorkflowInstances.Add(new WorkflowInstance
        {
            TenantId = fixture.TenantId,
            WorkflowDefinitionId = Guid.NewGuid(),
            EntityId = seed.Requisition.Id,
            EntityTypeId = seed.EntityType.Id,
            Status = WorkflowInstanceStatus.InProgress,
            InitiatedById = seed.Requisition.RequestedById
        });
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.EnforceApprovalAsync(
            seed.Requisition, "trace-authority-mismatch");

        var exception = await action.Should().ThrowAsync<ProcurementRequisitionAuthorityBlockedException>();
        exception.Which.Readiness.DecisionCode.Should().Be("PR_AUTHORITY_WORKFLOW_INSTANCE_MISMATCH");
        fixture.Sod.Verify(service => service.EnforceAsync(
            It.IsAny<ProcurementSodGuardRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TenantIsolationAndCapabilityAuthorizationAreAppliedBeforeAuthorityEvaluation()
    {
        await using var fixture = new Fixture();
        var seed = fixture.SeedRouteContext();
        await fixture.Context.SaveChangesAsync();
        seed.Requisition.TenantId = fixture.ForeignTenantId;

        await fixture.Service.Invoking(service => service.EnforceSubmissionAsync(
                seed.Requisition, "trace-authority-foreign"))
            .Should().ThrowAsync<ProcurementRequisitionAuthorityNotFoundException>()
            .Where(exception => exception.Code == "PR_NOT_FOUND");

        seed.Requisition.TenantId = fixture.TenantId;
        fixture.SwitchRoles("TDC_REQUISITIONER");
        fixture.Access.Setup(service => service.EnforceCapabilityAsync(
                It.Is<ProcurementAccessCapabilityRequest>(request =>
                    request.PermissionCode == "procurement.requisition.create"),
                "trace-authority-denied", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
            {
                Allowed = false,
                Code = "ACCESS_PERMISSION_DENIED",
                Message = "No active requisition responsibility."
            });

        await fixture.Service.Invoking(service => service.EnforceSubmissionAsync(
                seed.Requisition, "trace-authority-denied"))
            .Should().ThrowAsync<ProcurementRequisitionAuthorityAuthorizationException>();
        fixture.Compliance.Verify(service => service.EvaluateAuthorityRouteAsync(
            It.IsAny<ProcurementAuthorityRouteDecisionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private static readonly DateTime Moment = new(2026, 7, 22, 5, 0, 0, DateTimeKind.Utc);
        private readonly UnitOfWork _unitOfWork;
        private readonly Mock<ICurrentUserProvider> _currentUser;
        private readonly HashSet<string> _roles = new(StringComparer.OrdinalIgnoreCase) { "TenantAdmin" };

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            ForeignTenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            Context.Tenants.AddRange(
                new Tenant { Id = TenantId, Code = "TDC", Name = "TDC", Status = TenantStatus.Active },
                new Tenant { Id = ForeignTenantId, Code = "OTHER", Name = "Other", Status = TenantStatus.Active });
            Context.SaveChanges();

            _currentUser = new Mock<ICurrentUserProvider>();
            _currentUser.SetupGet(item => item.TenantId).Returns(TenantId);
            _currentUser.SetupGet(item => item.UserId).Returns(UserId);
            _currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            _currentUser.SetupGet(item => item.Username).Returns("authority.controller@tdc.test");
            _currentUser.SetupGet(item => item.FullName).Returns("Authority Controller");
            _currentUser.SetupGet(item => item.Roles).Returns(_roles);
            _currentUser.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns((string role) => _roles.Contains(role));

            _unitOfWork = new UnitOfWork(Context);
            Compliance = new Mock<IProcurementComplianceDecisionService>();
            Access = new Mock<IProcurementAccessControlService>();
            Sod = new Mock<IProcurementSodGuardService>();
            Workflow = new Mock<IWorkflowService>();
            Access.Setup(service => service.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Code = "ACCESS_ALLOWED",
                    Message = "Allowed"
                });
            Sod.Setup(service => service.EnforceAsync(
                    It.IsAny<ProcurementSodGuardRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementSodGuardDecisionDto
                {
                    Allowed = true,
                    Code = "SOD_ALLOWED",
                    Message = "Allowed"
                });
            var events = new ProcurementControlEventService(
                _unitOfWork, _currentUser.Object, NullLogger<ProcurementControlEventService>.Instance);
            Service = new ProcurementRequisitionAuthorityRouteService(
                _unitOfWork, _currentUser.Object, Compliance.Object, Access.Object,
                Sod.Object, events, Workflow.Object);
        }

        public Guid TenantId { get; }
        public Guid ForeignTenantId { get; }
        public Guid UserId { get; }
        public ApplicationDbContext Context { get; }
        public Mock<IProcurementComplianceDecisionService> Compliance { get; }
        public Mock<IProcurementAccessControlService> Access { get; }
        public Mock<IProcurementSodGuardService> Sod { get; }
        public Mock<IWorkflowService> Workflow { get; }
        public ProcurementRequisitionAuthorityRouteService Service { get; }

        public void SwitchRoles(params string[] roles)
        {
            _roles.Clear();
            foreach (var role in roles) _roles.Add(role);
        }

        public RouteSeed SeedRouteContext()
        {
            var entityType = new WorkflowEntityType
            {
                TenantId = TenantId,
                Code = "PURCHASE_REQUISITION",
                Name = "Purchase Requisition",
                IsActive = true
            };
            var workflow = new WorkflowDefinition
            {
                TenantId = TenantId,
                DefinitionKey = Guid.NewGuid(),
                Name = "TDC Purchase Requisition Approval",
                EntityTypeId = entityType.Id,
                EntityType = entityType,
                Version = 3,
                LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
                IsActive = true,
                PublishedAt = Moment.AddDays(-1)
            };
            var workflowStep = new WorkflowStep
            {
                TenantId = TenantId,
                WorkflowDefinitionId = workflow.Id,
                Name = "Tender Committee approval",
                StepType = WorkflowStepType.Approval,
                Order = 10,
                RequiredRole = "TenderCommittee"
            };
            workflow.Steps.Add(workflowStep);
            var policy = new ProcurementPolicySet
            {
                TenantId = TenantId,
                PolicyKey = Guid.NewGuid(),
                Code = "TDC-AUTHORITY",
                Name = "TDC procurement policy",
                Version = 4,
                LifecycleStatus = ProcurementPolicyLifecycleStatus.Published,
                ScopeType = ProcurementPolicyScopeType.TenantBaseline,
                SourceConfigurationProfileId = Guid.NewGuid(),
                DefaultCurrencyCode = "GHS",
                EffectiveFrom = Moment.AddMonths(-1),
                EffectiveTo = Moment.AddMonths(1),
                IsDefault = true,
                PublishedAt = Moment.AddDays(-1)
            };
            var authorityRule = new ProcurementPolicyAuthorityRule
            {
                TenantId = TenantId,
                PolicySetId = policy.Id,
                PolicySet = policy,
                RuleCode = "AUTH-ETC",
                AuthorityName = "Entity Tender Committee",
                AuthorityRole = "TenderCommittee",
                Category = ProcurementCategoryClass.Goods,
                CurrencyCode = "GHS",
                LowerBound = 0m,
                UpperBound = 100000m,
                Sequence = 1,
                Quorum = 3,
                WorkflowDefinitionId = workflow.Id,
                IsEnabled = true,
                EffectiveFrom = Moment.AddMonths(-1),
                EffectiveTo = Moment.AddMonths(1),
                SourceDecisionKey = "DEC-002"
            };
            var requisition = new PurchaseRequisition
            {
                TenantId = TenantId,
                RequisitionNumber = "PR-AUTHORITY-0001",
                RequestedById = Guid.NewGuid(),
                Status = "Draft",
                ProcurementCategory = ProcurementCategoryClass.Goods,
                TotalAmount = 2500m,
                Currency = "GHS"
            };
            Context.AddRange(entityType, workflow, workflowStep, policy, authorityRule, requisition);

            var decision = new ProcurementAuthorityRouteDecisionDto
            {
                EvaluationId = Guid.NewGuid(),
                EvaluatedAtUtc = Moment,
                PolicyDateUtc = Moment,
                CorrelationId = "trace-authority-capture",
                IsReady = true,
                DecisionCode = "PR_AUTHORITY_ROUTE_READY",
                Message = "Resolved the configured route.",
                Policy = new ProcurementCompliancePolicySelectionDto
                {
                    PolicySetId = policy.Id,
                    PolicyKey = policy.PolicyKey,
                    PolicyCode = policy.Code,
                    PolicyName = policy.Name,
                    Version = policy.Version,
                    ScopeType = policy.ScopeType,
                    SourceConfigurationProfileId = policy.SourceConfigurationProfileId,
                    CurrencyCode = policy.DefaultCurrencyCode,
                    EffectiveFrom = policy.EffectiveFrom,
                    EffectiveTo = policy.EffectiveTo,
                    IsDefault = true,
                    SelectionReason = "Selected as the tenant default."
                },
                Category = ProcurementCategoryClass.Goods,
                Amount = 2500m,
                CurrencyCode = "GHS",
                Workflow = new ProcurementAuthorityWorkflowSelectionDto
                {
                    WorkflowDefinitionId = workflow.Id,
                    DefinitionKey = workflow.DefinitionKey,
                    Name = workflow.Name,
                    Version = workflow.Version,
                    EntityTypeCode = entityType.Code,
                    EntityTypeName = entityType.Name,
                    PublishedAt = workflow.PublishedAt
                },
                Steps =
                [
                    new ProcurementAuthorityRouteStepDecisionDto
                    {
                        Sequence = 1,
                        RuleId = authorityRule.Id,
                        RulePolicySetId = policy.Id,
                        RulePolicyCode = policy.Code,
                        RulePolicyVersion = policy.Version,
                        RuleCode = authorityRule.RuleCode,
                        SourceDecisionKey = authorityRule.SourceDecisionKey,
                        AuthorityName = authorityRule.AuthorityName,
                        AuthorityRole = authorityRule.AuthorityRole,
                        CurrencyCode = "GHS",
                        LowerBound = 0m,
                        UpperBound = 100000m,
                        LowerInclusive = true,
                        UpperInclusive = true,
                        Quorum = 3,
                        WorkflowDefinitionId = workflow.Id,
                        WorkflowStepId = workflowStep.Id,
                        WorkflowStepName = workflowStep.Name,
                        WorkflowStepOrder = workflowStep.Order
                    }
                ]
            };
            return new RouteSeed(requisition, policy, authorityRule, entityType, workflow, workflowStep, decision);
        }

        public async Task<ProcurementRequisitionAuthorityRoute> CaptureAsync(RouteSeed seed, string correlationId)
        {
            Compliance.Setup(service => service.EvaluateAuthorityRouteAsync(
                    It.IsAny<ProcurementAuthorityRouteDecisionRequest>(), correlationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(seed.Decision);
            var decision = await Service.EnforceSubmissionAsync(seed.Requisition, correlationId);
            var route = await Service.CaptureAsync(seed.Requisition, decision, correlationId);
            await Context.SaveChangesAsync();
            return route;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }

    private sealed record RouteSeed(
        PurchaseRequisition Requisition,
        ProcurementPolicySet Policy,
        ProcurementPolicyAuthorityRule AuthorityRule,
        WorkflowEntityType EntityType,
        WorkflowDefinition Workflow,
        WorkflowStep WorkflowStep,
        ProcurementAuthorityRouteDecisionDto Decision);
}
