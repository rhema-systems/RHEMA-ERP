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

public sealed class ProcurementRequisitionBudgetControlServiceTests
{
    [Fact]
    public async Task ApprovedAvailableBudgetIsReservedWithSnapshotAndImmutableAuditLineage()
    {
        await using var fixture = new Fixture();
        var budget = fixture.NewBudget(1_000m);
        var requisition = fixture.NewRequisition(budget, 400m);
        fixture.Context.AddRange(budget, requisition);
        await fixture.Context.SaveChangesAsync();

        var readiness = await fixture.Service.ReserveAsync(requisition, "trace-budget-reserve");

        readiness.CanReserve.Should().BeTrue();
        readiness.DecisionCode.Should().Be("PR_BUDGET_AVAILABLE");
        readiness.CommitmentStatus.Should().Be("Reserved");
        readiness.AvailableAmount.Should().Be(600m);
        budget.CommittedAmount.Should().Be(400m);
        budget.RemainingAmount.Should().Be(600m);
        requisition.BudgetValidated.Should().BeTrue();
        var commitment = await fixture.Context.ProcurementBudgetCommitments.SingleAsync();
        commitment.BudgetAvailableBefore.Should().Be(1_000m);
        commitment.BudgetAvailableAfter.Should().Be(600m);
        commitment.CorrelationId.Should().Be("trace-budget-reserve");
        var history = await fixture.Service.GetHistoryAsync(requisition.Id);
        history.Should().ContainSingle(item => item.Action == "BudgetCommitmentReserved" && item.IntegrityHash.Length == 64);
    }

    [Fact]
    public async Task InsufficientBudgetBlocksWithoutCreatingACommitmentOrMutatingFinanceTotals()
    {
        await using var fixture = new Fixture();
        var budget = fixture.NewBudget(100m);
        var requisition = fixture.NewRequisition(budget, 125m);
        fixture.Context.AddRange(budget, requisition);
        await fixture.Context.SaveChangesAsync();

        var readiness = await fixture.Service.ReserveAsync(requisition, "trace-budget-blocked");

        readiness.CanReserve.Should().BeFalse();
        readiness.DecisionCode.Should().Be("PR_BUDGET_INSUFFICIENT");
        readiness.ShortfallAmount.Should().Be(25m);
        budget.CommittedAmount.Should().Be(0m);
        requisition.BudgetValidated.Should().BeFalse();
        (await fixture.Context.ProcurementBudgetCommitments.CountAsync()).Should().Be(0);
        (await fixture.Service.GetHistoryAsync(requisition.Id)).Should()
            .ContainSingle(item => item.Action == "BudgetReservationBlocked" && item.Result == "Denied");
    }

    [Fact]
    public async Task ASecondReservationAttemptReusesTheActiveCommitmentWithoutDoubleSpending()
    {
        await using var fixture = new Fixture();
        var budget = fixture.NewBudget(500m);
        var requisition = fixture.NewRequisition(budget, 200m);
        fixture.Context.AddRange(budget, requisition);
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.ReserveAsync(requisition, "trace-first");
        var retry = await fixture.Service.ReserveAsync(requisition, "trace-retry");

        retry.Basis.Should().Be("ExistingCommitment");
        retry.CommitmentStatus.Should().Be("Reserved");
        budget.CommittedAmount.Should().Be(200m);
        (await fixture.Context.ProcurementBudgetCommitments.CountAsync()).Should().Be(1);
        (await fixture.Service.GetHistoryAsync(requisition.Id)).Should()
            .Contain(item => item.Action == "BudgetReservationReused");
    }

    [Fact]
    public async Task CompetingRequisitionsSeeTheLatestCommittedBalanceAndCannotOverspend()
    {
        await using var fixture = new Fixture();
        var budget = fixture.NewBudget(100m);
        var first = fixture.NewRequisition(budget, 70m);
        var second = fixture.NewRequisition(budget, 50m);
        fixture.Context.AddRange(budget, first, second);
        await fixture.Context.SaveChangesAsync();

        var firstResult = await fixture.Service.ReserveAsync(first, "trace-first-pr");
        var secondResult = await fixture.Service.ReserveAsync(second, "trace-second-pr");

        firstResult.CanReserve.Should().BeTrue();
        secondResult.CanReserve.Should().BeFalse();
        secondResult.ShortfallAmount.Should().Be(20m);
        budget.CommittedAmount.Should().Be(70m);
        (await fixture.Context.ProcurementBudgetCommitments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ReleaseRestoresAvailabilityAndResubmissionAdvancesTheReservationSequence()
    {
        await using var fixture = new Fixture();
        var budget = fixture.NewBudget(300m);
        var requisition = fixture.NewRequisition(budget, 120m);
        fixture.Context.AddRange(budget, requisition);
        await fixture.Context.SaveChangesAsync();
        await fixture.Service.ReserveAsync(requisition, "trace-reserve");

        var release = await fixture.Service.ReleaseAsync(
            requisition, "Rejected by the assigned checker.", "procurement.requisition.approve", "trace-release");
        var reserveAgain = await fixture.Service.ReserveAsync(requisition, "trace-resubmit");

        release.Released.Should().BeTrue();
        release.ReleasedAmount.Should().Be(120m);
        reserveAgain.ReservationSequence.Should().Be(2);
        budget.CommittedAmount.Should().Be(120m);
        var commitment = await fixture.Context.ProcurementBudgetCommitments.SingleAsync();
        commitment.Status.Should().Be(ProcurementBudgetCommitmentStatus.Reserved);
        commitment.ReleaseReason.Should().BeNull();
        (await fixture.Service.GetHistoryAsync(requisition.Id)).Select(item => item.Action).Should()
            .Contain(["BudgetCommitmentReleased", "BudgetCommitmentReserved"]);
    }

    [Fact]
    public async Task FutureOrUnapprovedBudgetStateIsRejectedAsStaleFinanceData()
    {
        await using var fixture = new Fixture();
        var budget = fixture.NewBudget(500m);
        budget.EffectiveDate = DateTime.UtcNow.AddDays(1);
        var requisition = fixture.NewRequisition(budget, 100m);
        fixture.Context.AddRange(budget, requisition);
        await fixture.Context.SaveChangesAsync();

        var readiness = await fixture.Service.GetReadinessAsync(requisition.Id);

        readiness.CanReserve.Should().BeFalse();
        readiness.DecisionCode.Should().Be("PR_BUDGET_NOT_EFFECTIVE");
    }

    [Fact]
    public async Task CompletedBudgetExceptionWithEvidenceAllowsAControlledShortfallAndRecordsDecision006()
    {
        await using var fixture = new Fixture();
        var budget = fixture.NewBudget(100m);
        var requisition = fixture.NewRequisition(budget, 150m);
        var exception = fixture.SeedBudgetOverride(requisition.Id);
        requisition.Justification = "Critical continuity purchase with Finance-approved shortfall.";
        requisition.ApprovedExceptionRuleId = exception.Rule.Id;
        requisition.ApprovedExceptionRuleCode = exception.Rule.RuleCode;
        requisition.ExceptionWorkflowInstanceId = exception.Workflow.Id;
        requisition.ExceptionApprovalReference = "FIN-OVERRIDE-105";
        requisition.ExceptionEvidenceReference = "evidence://finance/override-105";
        requisition.ExceptionApprovedAtUtc = exception.Workflow.CompletedDate;
        fixture.Context.AddRange(budget, requisition);
        await fixture.Context.SaveChangesAsync();

        var readiness = await fixture.Service.ReserveAsync(requisition, "trace-budget-override");

        readiness.CanReserve.Should().BeTrue();
        readiness.IsOverride.Should().BeTrue();
        readiness.DecisionCode.Should().Be("PR_BUDGET_OVERRIDE_APPROVED");
        budget.RemainingAmount.Should().Be(-50m);
        var commitment = await fixture.Context.ProcurementBudgetCommitments.SingleAsync();
        commitment.OverrideApprovalReference.Should().Be("FIN-OVERRIDE-105");
        var audit = await fixture.Context.ProcurementControlEvents
            .Include(item => item.EvidenceLinks).SingleAsync(item => item.SourceId == requisition.Id);
        audit.DecisionKeysJson.Should().Contain("DEC-006");
        audit.EvidenceLinks.Select(item => item.Reference).Should().Contain("evidence://finance/override-105");
    }

    [Fact]
    public async Task ExpiredOrForeignSubjectOverrideDoesNotAuthorizeTheShortfall()
    {
        await using var fixture = new Fixture();
        var budget = fixture.NewBudget(100m);
        var requisition = fixture.NewRequisition(budget, 150m);
        var exception = fixture.SeedBudgetOverride(Guid.NewGuid(), completedAt: DateTime.UtcNow.AddDays(-40));
        requisition.Justification = "Attempted stale override.";
        requisition.ApprovedExceptionRuleId = exception.Rule.Id;
        requisition.ExceptionWorkflowInstanceId = exception.Workflow.Id;
        requisition.ExceptionApprovalReference = "STALE-OVERRIDE";
        requisition.ExceptionEvidenceReference = "evidence://stale";
        requisition.ExceptionApprovedAtUtc = exception.Workflow.CompletedDate;
        fixture.Context.AddRange(budget, requisition);
        await fixture.Context.SaveChangesAsync();

        var readiness = await fixture.Service.GetReadinessAsync(requisition.Id);

        readiness.CanReserve.Should().BeFalse();
        readiness.DecisionCode.Should().Be("PR_BUDGET_OVERRIDE_WORKFLOW_NOT_FOUND");
    }

    [Fact]
    public async Task ExpiredBudgetOverrideForTheSameRequisitionDoesNotAuthorizeTheShortfall()
    {
        await using var fixture = new Fixture();
        var budget = fixture.NewBudget(100m);
        var requisition = fixture.NewRequisition(budget, 150m);
        var exception = fixture.SeedBudgetOverride(requisition.Id, completedAt: DateTime.UtcNow.AddDays(-40));
        requisition.Justification = "Attempted expired override.";
        requisition.ApprovedExceptionRuleId = exception.Rule.Id;
        requisition.ExceptionWorkflowInstanceId = exception.Workflow.Id;
        requisition.ExceptionApprovalReference = "EXPIRED-OVERRIDE";
        requisition.ExceptionEvidenceReference = "evidence://expired";
        requisition.ExceptionApprovedAtUtc = exception.Workflow.CompletedDate;
        fixture.Context.AddRange(budget, requisition);
        await fixture.Context.SaveChangesAsync();

        var readiness = await fixture.Service.GetReadinessAsync(requisition.Id);

        readiness.CanReserve.Should().BeFalse();
        readiness.DecisionCode.Should().Be("PR_BUDGET_OVERRIDE_EXPIRED");
    }

    [Fact]
    public async Task RejectedBudgetOverrideWorkflowDoesNotAuthorizeTheShortfall()
    {
        await using var fixture = new Fixture();
        var budget = fixture.NewBudget(100m);
        var requisition = fixture.NewRequisition(budget, 150m);
        var exception = fixture.SeedBudgetOverride(requisition.Id);
        exception.Workflow.Status = WorkflowInstanceStatus.Cancelled;
        requisition.Justification = "Attempted rejected override.";
        requisition.ApprovedExceptionRuleId = exception.Rule.Id;
        requisition.ExceptionWorkflowInstanceId = exception.Workflow.Id;
        requisition.ExceptionApprovalReference = "REJECTED-OVERRIDE";
        requisition.ExceptionEvidenceReference = "evidence://rejected";
        requisition.ExceptionApprovedAtUtc = exception.Workflow.CompletedDate;
        fixture.Context.AddRange(budget, requisition);
        await fixture.Context.SaveChangesAsync();

        var readiness = await fixture.Service.GetReadinessAsync(requisition.Id);

        readiness.CanReserve.Should().BeFalse();
        readiness.DecisionCode.Should().Be("PR_BUDGET_OVERRIDE_WORKFLOW_NOT_APPROVED");
    }

    [Fact]
    public async Task CrossTenantBudgetCannotBeResolvedThroughARequisitionSnapshot()
    {
        await using var fixture = new Fixture();
        var budget = fixture.NewBudget(500m);
        budget.TenantId = fixture.ForeignTenantId;
        var requisition = fixture.NewRequisition(budget, 100m);
        requisition.TenantId = fixture.TenantId;
        fixture.Context.AddRange(budget, requisition);
        await fixture.Context.SaveChangesAsync();

        var readiness = await fixture.Service.GetReadinessAsync(requisition.Id);

        readiness.CanReserve.Should().BeFalse();
        readiness.DecisionCode.Should().Be("PR_BUDGET_NOT_FOUND");
    }

    [Fact]
    public async Task NonAdministratorReservationRequiresTheRequisitionCreateCapability()
    {
        await using var fixture = new Fixture();
        fixture.SwitchRoles("TDC_REQUISITIONER");
        fixture.Access.Setup(service => service.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
            {
                Allowed = false, Code = "ACCESS_PERMISSION_DENIED", Message = "No active requisition responsibility."
            });
        var budget = fixture.NewBudget(500m);
        var requisition = fixture.NewRequisition(budget, 100m);
        fixture.Context.AddRange(budget, requisition);
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.ReserveAsync(requisition, "trace-capability-denied");

        await action.Should().ThrowAsync<ProcurementRequisitionBudgetAuthorizationException>();
        fixture.Access.Verify(service => service.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request => request.PermissionCode == "procurement.requisition.create"),
            "trace-capability-denied", It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class Fixture : IAsyncDisposable
    {
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
            _currentUser.SetupGet(item => item.Username).Returns("budget.controller@tdc.test");
            _currentUser.SetupGet(item => item.FullName).Returns("Budget Controller");
            _currentUser.SetupGet(item => item.Roles).Returns(_roles);
            _currentUser.Setup(item => item.HasRole(It.IsAny<string>())).Returns((string role) => _roles.Contains(role));

            _unitOfWork = new UnitOfWork(Context);
            Access = new Mock<IProcurementAccessControlService>();
            Access.Setup(service => service.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true, Code = "ACCESS_ALLOWED", Message = "Allowed" });
            var controlEvents = new ProcurementControlEventService(_unitOfWork, _currentUser.Object,
                NullLogger<ProcurementControlEventService>.Instance);
            Service = new ProcurementRequisitionBudgetControlService(
                _unitOfWork, _currentUser.Object, Access.Object, controlEvents,
                new ProcurementBudgetReservationStore(Context));
        }

        public Guid TenantId { get; }
        public Guid ForeignTenantId { get; }
        public Guid UserId { get; }
        public ApplicationDbContext Context { get; }
        public Mock<IProcurementAccessControlService> Access { get; }
        public ProcurementRequisitionBudgetControlService Service { get; }

        public ProcurementBudget NewBudget(decimal allocated) => new()
        {
            Id = Guid.NewGuid(), TenantId = TenantId, BudgetCode = $"BUD-105-{Guid.NewGuid():N}"[..20],
            Title = "Approved operational budget", DepartmentId = Guid.NewGuid(), FiscalYear = DateTime.UtcNow.Year,
            AllocatedAmount = allocated, UtilizedAmount = 0m, CommittedAmount = 0m, RemainingAmount = allocated,
            Currency = "GHS", Status = "Approved", ControlLevel = "Strict",
            EffectiveDate = DateTime.UtcNow.AddDays(-1), ExpiryDate = DateTime.UtcNow.AddYears(1),
            ApprovedById = UserId, ApprovedDate = DateTime.UtcNow.AddDays(-1), CreatedAt = DateTime.UtcNow
        };

        public PurchaseRequisition NewRequisition(ProcurementBudget budget, decimal amount) => new()
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            RequisitionNumber = $"PR-2026-{Random.Shared.Next(1000, 9999)}",
            RequestedById = UserId, Status = "Draft", ProcurementCategory = ProcurementCategoryClass.Goods,
            BudgetId = budget.Id, BudgetCode = budget.BudgetCode, Currency = budget.Currency,
            TotalAmount = amount, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        public OverrideReferences SeedBudgetOverride(Guid requisitionId, DateTime? completedAt = null)
        {
            var completed = completedAt ?? DateTime.UtcNow.AddHours(-1);
            var policy = new ProcurementPolicySet
            {
                Id = Guid.NewGuid(), TenantId = TenantId, PolicyKey = Guid.NewGuid(), Code = "TDC-POLICY-105",
                Name = "TDC Finance budget exception policy", Version = 1,
                LifecycleStatus = ProcurementPolicyLifecycleStatus.Published,
                SourceConfigurationProfileId = Guid.NewGuid(), EffectiveFrom = DateTime.UtcNow.AddDays(-60),
                PublishedAt = DateTime.UtcNow.AddDays(-60)
            };
            var entityType = new WorkflowEntityType
            {
                Id = Guid.NewGuid(), TenantId = TenantId, Code = "PROCUREMENT_EXCEPTION", Name = "Procurement Exception"
            };
            var definition = new WorkflowDefinition
            {
                Id = Guid.NewGuid(), TenantId = TenantId, Name = "Budget Exception Approval",
                EntityTypeId = entityType.Id, Version = 1, IsActive = true
            };
            var rule = new ProcurementPolicyExceptionRule
            {
                Id = Guid.NewGuid(), TenantId = TenantId, PolicySetId = policy.Id, RuleCode = "EX-BUDGET-105",
                ExceptionName = "Finance-approved budget shortfall", ExceptionType = "BudgetShortfall",
                Category = ProcurementCategoryClass.Goods, Disposition = ProcurementExceptionDisposition.ApprovalRequired,
                ApproverRole = "TDC_FINANCE_MANAGER", WorkflowDefinitionId = definition.Id,
                JustificationRequired = true, EvidenceRequired = true, MaximumDurationDays = 30,
                IsEnabled = true, EffectiveFrom = DateTime.UtcNow.AddDays(-60), SourceDecisionKey = "DEC-006"
            };
            var workflow = new WorkflowInstance
            {
                Id = Guid.NewGuid(), TenantId = TenantId, WorkflowDefinitionId = definition.Id,
                EntityTypeId = entityType.Id, EntityId = requisitionId, InitiatedById = UserId,
                Status = WorkflowInstanceStatus.Completed, CompletedDate = completed
            };
            Context.AddRange(policy, entityType, definition, rule, workflow);
            Context.SaveChanges();
            return new OverrideReferences(rule, workflow);
        }

        public void SwitchRoles(params string[] roles)
        {
            _roles.Clear();
            foreach (var role in roles) _roles.Add(role);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }

    private sealed record OverrideReferences(
        ProcurementPolicyExceptionRule Rule,
        WorkflowInstance Workflow);
}
