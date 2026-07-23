using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementComplianceDecisionServiceTests
{
    private static readonly DateTime Moment = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task EffectivePolicyResolvesAllOutputsWithExplainableLineageAndDoesNotMutateState()
    {
        await using var fixture = new DecisionFixture();
        var policy = await fixture.AddCompletePolicyAsync();
        var before = await fixture.TotalPolicyRowsAsync();
        fixture.Context.ChangeTracker.Clear();

        var result = await fixture.Service.EvaluateAsync(Request(policy.Id, evidence: new[] { "SPEC-KEY" }), "trace-complete");

        result.EvaluationOnly.Should().BeTrue();
        result.Outcome.Should().Be(ProcurementComplianceOutcome.ReviewRequired);
        result.CanProceed.Should().BeTrue();
        result.SelectedMethod.Should().Be(ProcurementMethodType.RequestForQuotation);
        result.RequiredAuthorities.Should().ContainSingle(item => item.AuthorityRole == "TenderCommittee");
        result.RequiredEvidence.Should().ContainSingle(item => item.SharedRequirementKey == "SPEC-KEY");
        result.Route.Should().Contain(item => item.StepType == ProcurementComplianceRouteStepType.Authority);
        result.MatchedRules.Should().Contain(item => item.RuleKind == ProcurementPolicyRuleKind.Threshold && item.SourceDecisionKey == "DEC-001");
        result.Trace.Select(item => item.Stage).Should().Contain(new[] { "Category", "Method and threshold", "Authority route", "Evidence", "Exception", "Segregation of duties", "Outcome" });
        (await fixture.TotalPolicyRowsAsync()).Should().Be(before);
        fixture.Context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task MissingSharedEvidenceAndIncompleteExceptionBecomeExplainableHardStops()
    {
        await using var fixture = new DecisionFixture();
        var policy = await fixture.AddCompletePolicyAsync();
        var request = Request(policy.Id);
        request.ExceptionType = "Emergency";

        var result = await fixture.Service.EvaluateAsync(request, "trace-evidence");

        result.Outcome.Should().Be(ProcurementComplianceOutcome.Blocked);
        result.HardStops.Should().Contain(item => item.Code == "MANDATORY_EVIDENCE_MISSING" && item.RuleCode == "EVIDENCE-SPEC");
        result.HardStops.Should().Contain(item => item.Code == "EXCEPTION_JUSTIFICATION_REQUIRED");
        result.HardStops.Should().Contain(item => item.Code == "EXCEPTION_APPROVAL_REQUIRED");
        result.SelectedException.Should().NotBeNull();
        result.MatchedRules.Should().Contain(item => item.RuleKind == ProcurementPolicyRuleKind.Exception && item.SourceDecisionKey == "DEC-006");
    }

    [Fact]
    public async Task SodConflictIsReturnedAsDeclarativeHardStopWithoutRuntimeEnforcement()
    {
        await using var fixture = new DecisionFixture();
        var policy = await fixture.AddCompletePolicyAsync();
        var request = Request(policy.Id, evidence: new[] { "SPEC-KEY" });
        request.EntityType = "PurchaseRequisition";
        request.Action = "Approve";
        request.ActorRoles = new() { "Approver" };
        request.SourceOwnerRoles = new() { "Requester" };
        request.SourceOwnerUserId = fixture.UserId;

        var result = await fixture.Service.EvaluateAsync(request, "trace-sod");

        result.Outcome.Should().Be(ProcurementComplianceOutcome.Blocked);
        result.HardStops.Should().ContainSingle(item => item.Code == "SOD_CONFLICT" && item.RuleCode == "SOD-OWN-APPROVAL");
        result.Trace.Single(item => item.Stage == "Segregation of duties").Result.Should().Contain("reusable SOD guard");
    }

    [Fact]
    public async Task TenantIsolationAndEffectiveDatesAreAppliedBeforePolicySelection()
    {
        await using var fixture = new DecisionFixture();
        var policy = await fixture.AddCompletePolicyAsync();
        fixture.SwitchTenant(Guid.NewGuid());

        (await fixture.Service.GetEffectivePolicyOptionsAsync(Moment)).Should().BeEmpty();
        await fixture.Service.Invoking(item => item.EvaluateAsync(Request(policy.Id), "trace-tenant"))
            .Should().ThrowAsync<ProcurementCompliancePolicyNotFoundException>();

        fixture.SwitchTenant(fixture.TenantId);
        await fixture.Service.Invoking(item => item.EvaluateAsync(Request(policy.Id, atUtc: Moment.AddYears(2)), "trace-date"))
            .Should().ThrowAsync<ProcurementCompliancePolicyNotFoundException>();
    }

    [Fact]
    public async Task AmbiguousDefaultSelectionRequiresAnExplicitPolicy()
    {
        await using var fixture = new DecisionFixture();
        await fixture.AddCompletePolicyAsync(code: "POLICY-A");
        await fixture.AddCompletePolicyAsync(code: "POLICY-B");

        await fixture.Service.Invoking(item => item.EvaluateAsync(Request(), "trace-conflict"))
            .Should().ThrowAsync<ProcurementCompliancePolicyConflictException>().WithMessage("Multiple default*");
    }

    [Fact]
    public async Task TenantOverrideReplaceAndDisableMergeAgainstImmutableBaseWithLineage()
    {
        await using var fixture = new DecisionFixture();
        var basePolicy = await fixture.AddCompletePolicyAsync(code: "BASE", lifecycle: ProcurementPolicyLifecycleStatus.Retired, isDefault: false);
        var baseAuthority = await fixture.Context.ProcurementPolicyAuthorityRules.SingleAsync(item => item.PolicySetId == basePolicy.Id);
        var baseEvidence = await fixture.Context.ProcurementPolicyEvidenceRules.SingleAsync(item => item.PolicySetId == basePolicy.Id);
        var tenantOverride = fixture.NewPolicy("OVERRIDE", ProcurementPolicyLifecycleStatus.Published, true, ProcurementPolicyScopeType.TenantOverride, basePolicy.Id);
        fixture.Context.ProcurementPolicySets.Add(tenantOverride);
        fixture.Context.ProcurementPolicyAuthorityRules.Add(new ProcurementPolicyAuthorityRule
        {
            TenantId = fixture.TenantId, PolicySetId = tenantOverride.Id, RuleCode = "AUTH-OVERRIDE", AuthorityName = "Board",
            AuthorityRole = "BoardSecretary", Category = ProcurementCategoryClass.Goods, CurrencyCode = "GHS", LowerBound = 0,
            UpperBound = 100000, Sequence = 1, Quorum = 2, IsEnabled = true, EffectiveFrom = Moment.AddMonths(-1), EffectiveTo = Moment.AddMonths(1),
            OverrideAction = ProcurementPolicyOverrideAction.Replace, SourceRuleId = baseAuthority.Id, SourceDecisionKey = "DEC-002"
        });
        fixture.Context.ProcurementPolicyEvidenceRules.Add(new ProcurementPolicyEvidenceRule
        {
            TenantId = fixture.TenantId, PolicySetId = tenantOverride.Id, RuleCode = "EVIDENCE-DISABLE", EvidenceName = "Disable base specification",
            Stage = ProcurementEvidenceStage.Requisition, IsEnabled = true, EffectiveFrom = Moment.AddMonths(-1), EffectiveTo = Moment.AddMonths(1),
            OverrideAction = ProcurementPolicyOverrideAction.Disable, SourceRuleId = baseEvidence.Id, SourceDecisionKey = "DEC-006"
        });
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.EvaluateAsync(Request(tenantOverride.Id), "trace-override");

        result.RequiredAuthorities.Should().ContainSingle(item => item.AuthorityRole == "BoardSecretary");
        result.RequiredEvidence.Should().BeEmpty();
        result.HardStops.Should().NotContain(item => item.Code == "MANDATORY_EVIDENCE_MISSING");
        result.MatchedRules.Should().Contain(item => item.RuleCode == "AUTH-OVERRIDE" && item.SourceRuleId == baseAuthority.Id && item.OverrideAction == ProcurementPolicyOverrideAction.Replace);
    }

    [Theory]
    [InlineData(ProcurementMethodType.QualityBasedSelection)]
    [InlineData(ProcurementMethodType.QualityAndCostBasedSelection)]
    public async Task ConsultancySelectionIncludesQbsAndQcbs(ProcurementMethodType method)
    {
        await using var fixture = new DecisionFixture();
        var policy = await fixture.AddCompletePolicyAsync();
        var suffix = method == ProcurementMethodType.QualityBasedSelection ? "QBS" : "QCBS";
        fixture.Context.ProcurementPolicyCategoryRules.Add(new ProcurementPolicyCategoryRule
        {
            TenantId = fixture.TenantId, PolicySetId = policy.Id, RuleCode = $"CATEGORY-{suffix}", Name = "Consultancy",
            Category = ProcurementCategoryClass.ConsultancyServices, RequiresSpecification = true, IsEnabled = true,
            EffectiveFrom = Moment.AddMonths(-1), EffectiveTo = Moment.AddMonths(1), SourceDecisionKey = "DEC-001", Priority = 20
        });
        fixture.Context.ProcurementPolicyMethodRules.Add(new ProcurementPolicyMethodRule
        {
            TenantId = fixture.TenantId, PolicySetId = policy.Id, RuleCode = $"METHOD-{suffix}", Name = suffix,
            Category = ProcurementCategoryClass.ConsultancyServices, Method = method, IsAllowed = true,
            RequiresCompetition = true, IsEnabled = true, EffectiveFrom = Moment.AddMonths(-1), EffectiveTo = Moment.AddMonths(1),
            SourceDecisionKey = "DEC-003", Priority = 20
        });
        fixture.Context.ProcurementPolicyThresholdRules.Add(new ProcurementPolicyThresholdRule
        {
            TenantId = fixture.TenantId, PolicySetId = policy.Id, RuleCode = $"THRESHOLD-{suffix}", Name = $"{suffix} range",
            Category = ProcurementCategoryClass.ConsultancyServices, Method = method, CurrencyCode = "GHS",
            LowerBound = 0, UpperBound = 100000, StatutoryReference = "Act 663", IsEnabled = true,
            EffectiveFrom = Moment.AddMonths(-1), EffectiveTo = Moment.AddMonths(1), SourceDecisionKey = "DEC-001", Priority = 20
        });
        fixture.Context.ProcurementPolicyAuthorityRules.Add(new ProcurementPolicyAuthorityRule
        {
            TenantId = fixture.TenantId, PolicySetId = policy.Id, RuleCode = $"AUTH-{suffix}", AuthorityName = "Consultancy Committee",
            AuthorityRole = "TenderCommittee", Category = ProcurementCategoryClass.ConsultancyServices, CurrencyCode = "GHS",
            LowerBound = 0, UpperBound = 100000, Sequence = 1, Quorum = 3, IsEnabled = true,
            EffectiveFrom = Moment.AddMonths(-1), EffectiveTo = Moment.AddMonths(1), SourceDecisionKey = "DEC-002", Priority = 20
        });
        await fixture.Context.SaveChangesAsync();
        var request = Request(policy.Id);
        request.Category = ProcurementCategoryClass.ConsultancyServices;

        var result = await fixture.Service.EvaluateAsync(request, $"trace-{suffix.ToLowerInvariant()}");

        result.SelectedMethod.Should().Be(method);
        result.MethodCandidates.Should().ContainSingle(item => item.Method == method && item.MatchesAmount);
    }

    [Fact]
    public async Task AuthorityBandsHonorExclusiveAndInclusiveBoundaryWithoutRouteAmbiguity()
    {
        await using var fixture = new DecisionFixture();
        var policy = await fixture.AddCompletePolicyAsync();
        var workflow = await fixture.AddAuthorityWorkflowAsync(
            ("DepartmentHead", 1, true, 10, WorkflowStepType.Approval),
            ("ManagingDirector", 1, true, 20, WorkflowStepType.Approval));
        var lower = await fixture.Context.ProcurementPolicyAuthorityRules.SingleAsync();
        lower.RuleCode = "AUTH-BELOW-100";
        lower.AuthorityName = "Department approval";
        lower.AuthorityRole = "DepartmentHead";
        lower.LowerBound = 0m;
        lower.UpperBound = 100m;
        lower.LowerInclusive = true;
        lower.UpperInclusive = false;
        lower.Quorum = 1;
        lower.WorkflowDefinitionId = workflow.Id;
        fixture.Context.ProcurementPolicyAuthorityRules.Add(new ProcurementPolicyAuthorityRule
        {
            TenantId = fixture.TenantId,
            PolicySetId = policy.Id,
            RuleCode = "AUTH-AT-OR-ABOVE-100",
            AuthorityName = "Managing Director approval",
            AuthorityRole = "ManagingDirector",
            Category = ProcurementCategoryClass.Goods,
            CurrencyCode = "GHS",
            LowerBound = 100m,
            UpperBound = null,
            LowerInclusive = true,
            UpperInclusive = true,
            Sequence = 1,
            Quorum = 1,
            WorkflowDefinitionId = workflow.Id,
            IsEnabled = true,
            EffectiveFrom = Moment.AddMonths(-1),
            EffectiveTo = Moment.AddMonths(1),
            SourceDecisionKey = "DEC-002"
        });
        await fixture.Context.SaveChangesAsync();

        var below = await fixture.Service.EvaluateAuthorityRouteAsync(
            AuthorityRequest(policy.Id, 99.99m), "trace-below");
        var at = await fixture.Service.EvaluateAuthorityRouteAsync(
            AuthorityRequest(policy.Id, 100m), "trace-at");
        var above = await fixture.Service.EvaluateAuthorityRouteAsync(
            AuthorityRequest(policy.Id, 100.01m), "trace-above");

        below.IsReady.Should().BeTrue();
        below.Steps.Should().ContainSingle(item => item.RuleCode == "AUTH-BELOW-100" && item.AuthorityRole == "DepartmentHead");
        at.IsReady.Should().BeTrue();
        at.Steps.Should().ContainSingle(item => item.RuleCode == "AUTH-AT-OR-ABOVE-100" && item.AuthorityRole == "ManagingDirector");
        above.IsReady.Should().BeTrue();
        above.Steps.Should().ContainSingle(item => item.RuleCode == "AUTH-AT-OR-ABOVE-100");
    }

    [Fact]
    public async Task AuthorityBandsFailClosedForGapAndSameSequenceOverlap()
    {
        await using var fixture = new DecisionFixture();
        var policy = await fixture.AddCompletePolicyAsync();
        var workflow = await fixture.AddAuthorityWorkflowAsync(
            ("DepartmentHead", 1, true, 10, WorkflowStepType.Approval),
            ("ManagingDirector", 1, true, 20, WorkflowStepType.Approval));
        var lower = await fixture.Context.ProcurementPolicyAuthorityRules.SingleAsync();
        lower.AuthorityRole = "DepartmentHead";
        lower.UpperBound = 100m;
        lower.UpperInclusive = false;
        lower.Quorum = 1;
        lower.WorkflowDefinitionId = workflow.Id;
        var upper = new ProcurementPolicyAuthorityRule
        {
            TenantId = fixture.TenantId,
            PolicySetId = policy.Id,
            RuleCode = "AUTH-UPPER",
            AuthorityName = "Managing Director approval",
            AuthorityRole = "ManagingDirector",
            Category = ProcurementCategoryClass.Goods,
            CurrencyCode = "GHS",
            LowerBound = 100m,
            LowerInclusive = false,
            Sequence = 1,
            Quorum = 1,
            WorkflowDefinitionId = workflow.Id,
            IsEnabled = true,
            EffectiveFrom = Moment.AddMonths(-1),
            EffectiveTo = Moment.AddMonths(1),
            SourceDecisionKey = "DEC-002"
        };
        fixture.Context.ProcurementPolicyAuthorityRules.Add(upper);
        await fixture.Context.SaveChangesAsync();

        var gap = await fixture.Service.EvaluateAuthorityRouteAsync(
            AuthorityRequest(policy.Id, 100m), "trace-gap");
        gap.IsReady.Should().BeFalse();
        gap.DecisionCode.Should().Be("PR_AUTHORITY_COVERAGE_GAP");

        lower.UpperInclusive = true;
        upper.LowerInclusive = true;
        await fixture.Context.SaveChangesAsync();
        var overlap = await fixture.Service.EvaluateAuthorityRouteAsync(
            AuthorityRequest(policy.Id, 100m), "trace-overlap");
        overlap.IsReady.Should().BeFalse();
        overlap.DecisionCode.Should().Be("PR_AUTHORITY_ROUTE_AMBIGUOUS");
    }

    [Fact]
    public async Task AuthorityRouteFailsClosedAcrossCategoryCurrencyDateAndTenantBoundaries()
    {
        await using var fixture = new DecisionFixture();
        var policy = await fixture.AddCompletePolicyAsync();
        var workflow = await fixture.AddAuthorityWorkflowAsync(
            ("TenderCommittee", 3, true, 10, WorkflowStepType.Approval));
        var authority = await fixture.Context.ProcurementPolicyAuthorityRules.SingleAsync();
        authority.WorkflowDefinitionId = workflow.Id;
        await fixture.Context.SaveChangesAsync();

        var wrongCategory = AuthorityRequest(policy.Id, 2500m);
        wrongCategory.Category = ProcurementCategoryClass.Works;
        (await fixture.Service.EvaluateAuthorityRouteAsync(wrongCategory, "trace-category"))
            .DecisionCode.Should().Be("PR_AUTHORITY_NOT_CONFIGURED");

        var wrongCurrency = AuthorityRequest(policy.Id, 2500m);
        wrongCurrency.CurrencyCode = "USD";
        (await fixture.Service.EvaluateAuthorityRouteAsync(wrongCurrency, "trace-currency"))
            .DecisionCode.Should().Be("PR_AUTHORITY_CURRENCY_MISMATCH");

        var staleDate = AuthorityRequest(policy.Id, 2500m);
        staleDate.AtUtc = Moment.AddYears(2);
        (await fixture.Service.EvaluateAuthorityRouteAsync(staleDate, "trace-date"))
            .DecisionCode.Should().Be("PR_AUTHORITY_POLICY_NOT_EFFECTIVE");

        fixture.SwitchTenant(Guid.NewGuid());
        (await fixture.Service.EvaluateAuthorityRouteAsync(
                AuthorityRequest(policy.Id, 2500m), "trace-tenant"))
            .DecisionCode.Should().Be("PR_AUTHORITY_POLICY_NOT_EFFECTIVE");
    }

    [Fact]
    public async Task AuthorityRouteBindsExactPublishedWorkflowVersionAndOrderedConfiguredStages()
    {
        await using var fixture = new DecisionFixture();
        var policy = await fixture.AddCompletePolicyAsync();
        var workflow = await fixture.AddAuthorityWorkflowAsync(
            ("DepartmentHead", 1, true, 10, WorkflowStepType.Approval),
            ("ProcurementHead", 1, true, 20, WorkflowStepType.Approval),
            ("FinanceHead", 2, true, 30, WorkflowStepType.Approval),
            ("ManagingDirector", 1, true, 40, WorkflowStepType.Approval));
        fixture.Context.ProcurementPolicyAuthorityRules.RemoveRange(
            await fixture.Context.ProcurementPolicyAuthorityRules.ToListAsync());
        var roles = new[] { "DepartmentHead", "ProcurementHead", "FinanceHead", "ManagingDirector" };
        for (var index = 0; index < roles.Length; index++)
        {
            fixture.Context.ProcurementPolicyAuthorityRules.Add(new ProcurementPolicyAuthorityRule
            {
                TenantId = fixture.TenantId,
                PolicySetId = policy.Id,
                RuleCode = $"AUTH-{index + 1}",
                AuthorityName = roles[index],
                AuthorityRole = roles[index],
                Category = ProcurementCategoryClass.Goods,
                CurrencyCode = "GHS",
                LowerBound = 0m,
                UpperBound = 100000m,
                Sequence = index + 1,
                Quorum = roles[index] == "FinanceHead" ? 2 : 1,
                WorkflowDefinitionId = workflow.Id,
                IsEnabled = true,
                EffectiveFrom = Moment.AddMonths(-1),
                EffectiveTo = Moment.AddMonths(1),
                SourceDecisionKey = index == 2 ? "DEC-004" : "DEC-002"
            });
        }
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.EvaluateAuthorityRouteAsync(
            AuthorityRequest(policy.Id, 2500m), "trace-ordered-route");

        result.IsReady.Should().BeTrue();
        result.Workflow.Should().NotBeNull();
        result.Workflow!.WorkflowDefinitionId.Should().Be(workflow.Id);
        result.Workflow.Version.Should().Be(2);
        result.Steps.Select(item => item.Sequence).Should().Equal(1, 2, 3, 4);
        result.Steps.Select(item => item.WorkflowStepOrder).Should().Equal(10, 20, 30, 40);
        result.Steps.Select(item => item.AuthorityRole).Should().Equal(roles);
    }

    [Theory]
    [InlineData("role", "PR_AUTHORITY_WORKFLOW_STAGE_MISSING")]
    [InlineData("quorum", "PR_AUTHORITY_WORKFLOW_QUORUM_MISMATCH")]
    [InlineData("sod", "PR_AUTHORITY_WORKFLOW_SOD_INCOMPLETE")]
    [InlineData("inactive", "PR_AUTHORITY_WORKFLOW_INVALID")]
    public async Task AuthorityRouteRejectsInvalidSharedWorkflowMapping(string defect, string expectedCode)
    {
        await using var fixture = new DecisionFixture();
        var policy = await fixture.AddCompletePolicyAsync();
        var workflow = await fixture.AddAuthorityWorkflowAsync(
            ("TenderCommittee", defect == "quorum" ? 2 : 3, defect != "sod", 10, WorkflowStepType.Approval));
        var authority = await fixture.Context.ProcurementPolicyAuthorityRules.SingleAsync();
        authority.WorkflowDefinitionId = workflow.Id;
        if (defect == "role") authority.AuthorityRole = "UnconfiguredAuthority";
        if (defect == "inactive") workflow.IsActive = false;
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.EvaluateAuthorityRouteAsync(
            AuthorityRequest(policy.Id, 2500m), $"trace-{defect}");

        result.IsReady.Should().BeFalse();
        result.DecisionCode.Should().Be(expectedCode);
    }

    [Fact]
    public async Task AuthorityRouteUsesTenantOverrideReplacementWithImmutableBaseLineage()
    {
        await using var fixture = new DecisionFixture();
        var workflow = await fixture.AddAuthorityWorkflowAsync(
            ("ManagingDirector", 1, true, 10, WorkflowStepType.Approval));
        var basePolicy = await fixture.AddCompletePolicyAsync(
            code: "BASE-AUTHORITY", lifecycle: ProcurementPolicyLifecycleStatus.Retired, isDefault: false);
        var baseAuthority = await fixture.Context.ProcurementPolicyAuthorityRules.SingleAsync();
        var tenantOverride = fixture.NewPolicy(
            "OVERRIDE-AUTHORITY", ProcurementPolicyLifecycleStatus.Published, true,
            ProcurementPolicyScopeType.TenantOverride, basePolicy.Id);
        fixture.Context.ProcurementPolicySets.Add(tenantOverride);
        fixture.Context.ProcurementPolicyAuthorityRules.Add(new ProcurementPolicyAuthorityRule
        {
            TenantId = fixture.TenantId,
            PolicySetId = tenantOverride.Id,
            RuleCode = "AUTH-MD-OVERRIDE",
            AuthorityName = "Managing Director",
            AuthorityRole = "ManagingDirector",
            Category = ProcurementCategoryClass.Goods,
            CurrencyCode = "GHS",
            LowerBound = 0m,
            UpperBound = 100000m,
            Sequence = 1,
            Quorum = 1,
            WorkflowDefinitionId = workflow.Id,
            OverrideAction = ProcurementPolicyOverrideAction.Replace,
            SourceRuleId = baseAuthority.Id,
            SourceDecisionKey = "DEC-002",
            IsEnabled = true,
            EffectiveFrom = Moment.AddMonths(-1),
            EffectiveTo = Moment.AddMonths(1)
        });
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.EvaluateAuthorityRouteAsync(
            AuthorityRequest(tenantOverride.Id, 2500m), "trace-authority-override");

        result.IsReady.Should().BeTrue();
        result.Policy!.BasePolicySetId.Should().Be(basePolicy.Id);
        result.Steps.Should().ContainSingle(item =>
            item.RuleCode == "AUTH-MD-OVERRIDE" && item.SourceRuleId == baseAuthority.Id);
    }

    private static ProcurementAuthorityRouteDecisionRequest AuthorityRequest(Guid policyId, decimal amount) => new()
    {
        PolicySetId = policyId,
        Category = ProcurementCategoryClass.Goods,
        Amount = amount,
        CurrencyCode = "GHS",
        AtUtc = Moment,
        SourceType = "PurchaseRequisition",
        SourceReference = "PR-AUTHORITY-001"
    };

    private static ProcurementComplianceDecisionRequest Request(
        Guid? policyId = null,
        IEnumerable<string>? evidence = null,
        DateTime? atUtc = null) => new()
    {
        PolicySetId = policyId,
        Category = ProcurementCategoryClass.Goods,
        Amount = 2500,
        CurrencyCode = "GHS",
        SourceType = "PurchaseRequisition",
        SourceReference = "PR-SIM-001",
        AtUtc = atUtc ?? Moment,
        EntityType = "PurchaseRequisition",
        Action = "Submit",
        EvidenceReferenceKeys = evidence?.ToList() ?? new()
    };

    private sealed class DecisionFixture : IAsyncDisposable
    {
        private Guid _activeTenantId;
        private readonly Mock<ICurrentUserProvider> _currentUser = new();
        private readonly UnitOfWork _unitOfWork;

        public DecisionFixture()
        {
            TenantId = Guid.NewGuid();
            _activeTenantId = TenantId;
            UserId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            _currentUser.SetupGet(item => item.TenantId).Returns(() => _activeTenantId);
            _currentUser.SetupGet(item => item.UserId).Returns(UserId);
            _currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            _currentUser.SetupGet(item => item.Roles).Returns(new[] { "TenantAdmin" });
            _unitOfWork = new UnitOfWork(Context);
            Service = new ProcurementComplianceDecisionService(_unitOfWork, _currentUser.Object,
                NullLogger<ProcurementComplianceDecisionService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public ApplicationDbContext Context { get; }
        public ProcurementComplianceDecisionService Service { get; }
        public void SwitchTenant(Guid tenantId) => _activeTenantId = tenantId;

        public ProcurementPolicySet NewPolicy(
            string code,
            ProcurementPolicyLifecycleStatus lifecycle,
            bool isDefault,
            ProcurementPolicyScopeType scope = ProcurementPolicyScopeType.TenantBaseline,
            Guid? basePolicySetId = null) => new()
        {
            TenantId = TenantId,
            PolicyKey = Guid.NewGuid(),
            Code = code,
            Name = code,
            Version = 1,
            LifecycleStatus = lifecycle,
            ScopeType = scope,
            SourceConfigurationProfileId = Guid.NewGuid(),
            BasePolicySetId = basePolicySetId,
            DefaultCurrencyCode = "GHS",
            EffectiveFrom = Moment.AddMonths(-1),
            EffectiveTo = Moment.AddMonths(1),
            IsDefault = isDefault,
            PublishedAt = lifecycle == ProcurementPolicyLifecycleStatus.Published ? Moment.AddDays(-1) : null
        };

        public async Task<ProcurementPolicySet> AddCompletePolicyAsync(
            string code = "TDC-POLICY",
            ProcurementPolicyLifecycleStatus lifecycle = ProcurementPolicyLifecycleStatus.Published,
            bool isDefault = true)
        {
            var policy = NewPolicy(code, lifecycle, isDefault);
            Context.ProcurementPolicySets.Add(policy);
            Context.ProcurementPolicyCategoryRules.Add(new ProcurementPolicyCategoryRule
            {
                TenantId = TenantId, PolicySetId = policy.Id, RuleCode = "CATEGORY-GOODS", Name = "Goods",
                Category = ProcurementCategoryClass.Goods, RequiresSpecification = true, SpecificationTemplateCode = "GOODS-SPEC",
                IsEnabled = true, EffectiveFrom = Moment.AddMonths(-1), EffectiveTo = Moment.AddMonths(1), SourceDecisionKey = "DEC-001", Priority = 10
            });
            Context.ProcurementPolicyMethodRules.Add(new ProcurementPolicyMethodRule
            {
                TenantId = TenantId, PolicySetId = policy.Id, RuleCode = "METHOD-RFQ", Name = "RFQ",
                Category = ProcurementCategoryClass.Goods, Method = ProcurementMethodType.RequestForQuotation,
                IsAllowed = true, RequiresCompetition = true, MinimumQuotationCount = 3, IsEnabled = true,
                EffectiveFrom = Moment.AddMonths(-1), EffectiveTo = Moment.AddMonths(1), SourceDecisionKey = "DEC-003", Priority = 10
            });
            Context.ProcurementPolicyThresholdRules.Add(new ProcurementPolicyThresholdRule
            {
                TenantId = TenantId, PolicySetId = policy.Id, RuleCode = "THRESHOLD-RFQ", Name = "RFQ range",
                Category = ProcurementCategoryClass.Goods, Method = ProcurementMethodType.RequestForQuotation,
                CurrencyCode = "GHS", LowerBound = 0, UpperBound = 100000, StatutoryReference = "Act 663",
                IsEnabled = true, EffectiveFrom = Moment.AddMonths(-1), EffectiveTo = Moment.AddMonths(1), SourceDecisionKey = "DEC-001", Priority = 10
            });
            Context.ProcurementPolicyAuthorityRules.Add(new ProcurementPolicyAuthorityRule
            {
                TenantId = TenantId, PolicySetId = policy.Id, RuleCode = "AUTH-ETC", AuthorityName = "Entity Tender Committee",
                AuthorityRole = "TenderCommittee", Category = ProcurementCategoryClass.Goods, CurrencyCode = "GHS",
                LowerBound = 0, UpperBound = 100000, Sequence = 1, Quorum = 3, IsEnabled = true,
                EffectiveFrom = Moment.AddMonths(-1), EffectiveTo = Moment.AddMonths(1), SourceDecisionKey = "DEC-002", Priority = 10
            });
            Context.ProcurementPolicyEvidenceRules.Add(new ProcurementPolicyEvidenceRule
            {
                TenantId = TenantId, PolicySetId = policy.Id, RuleCode = "EVIDENCE-SPEC", EvidenceName = "Approved specification",
                Stage = ProcurementEvidenceStage.Requisition, Category = ProcurementCategoryClass.Goods,
                Method = ProcurementMethodType.RequestForQuotation, SharedRequirementKey = "SPEC-KEY", IsMandatory = true,
                RequiresVerification = true, IsEnabled = true, EffectiveFrom = Moment.AddMonths(-1), EffectiveTo = Moment.AddMonths(1),
                SourceDecisionKey = "DEC-006", Priority = 10
            });
            Context.ProcurementPolicyExceptionRules.Add(new ProcurementPolicyExceptionRule
            {
                TenantId = TenantId, PolicySetId = policy.Id, RuleCode = "EXCEPTION-EMERGENCY", ExceptionName = "Emergency procurement",
                ExceptionType = "Emergency", Category = ProcurementCategoryClass.Goods,
                Method = ProcurementMethodType.RequestForQuotation, Disposition = ProcurementExceptionDisposition.ApprovalRequired,
                JustificationRequired = true, EvidenceRequired = true, ApproverRole = "ManagingDirector", IsEnabled = true,
                EffectiveFrom = Moment.AddMonths(-1), EffectiveTo = Moment.AddMonths(1), SourceDecisionKey = "DEC-006", Priority = 10
            });
            Context.ProcurementPolicySodRules.Add(new ProcurementPolicySodRule
            {
                TenantId = TenantId, PolicySetId = policy.Id, RuleCode = "SOD-OWN-APPROVAL", Name = "No own approval",
                InitiatorRole = "Requester", ConflictingRole = "Approver", EntityType = "PurchaseRequisition", Action = "Approve",
                Enforcement = ProcurementSodEnforcement.HardStop, Explanation = "An approver cannot approve their own request.",
                IsEnabled = true, EffectiveFrom = Moment.AddMonths(-1), EffectiveTo = Moment.AddMonths(1), SourceDecisionKey = "DEC-004", Priority = 10
            });
            await Context.SaveChangesAsync();
            return policy;
        }

        public async Task<WorkflowDefinition> AddAuthorityWorkflowAsync(
            params (string Role, int Quorum, bool PreventInitiatorApproval, int Order, WorkflowStepType StepType)[] stages)
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
                Version = 2,
                LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
                IsActive = true,
                PublishedAt = Moment.AddDays(-1)
            };
            foreach (var stage in stages)
            {
                workflow.Steps.Add(new WorkflowStep
                {
                    TenantId = TenantId,
                    WorkflowDefinitionId = workflow.Id,
                    Name = $"{stage.Role} approval",
                    StepType = stage.StepType,
                    Order = stage.Order,
                    RequiredRole = stage.Role,
                    Configuration = JsonSerializer.Serialize(new WorkflowStepConfigurationDto
                    {
                        ApprovalConfig = new WorkflowApprovalConfigDto
                        {
                            ApprovalType = stage.Quorum > 1 ? WorkflowApprovalType.Multiple : WorkflowApprovalType.Single,
                            MinApprovalsRequired = stage.Quorum,
                            PreventInitiatorApproval = stage.PreventInitiatorApproval,
                            RequireDistinctApprovers = stage.Quorum > 1,
                            ApproverRules =
                            [
                                new WorkflowAssignmentRuleDto
                                {
                                    AssignmentType = WorkflowAssignmentType.Role,
                                    Role = stage.Role
                                }
                            ]
                        }
                    })
                });
            }
            Context.WorkflowEntityTypes.Add(entityType);
            Context.WorkflowDefinitions.Add(workflow);
            await Context.SaveChangesAsync();
            return workflow;
        }

        public async Task<int> TotalPolicyRowsAsync() =>
            await Context.ProcurementPolicySets.CountAsync() +
            await Context.ProcurementPolicyCategoryRules.CountAsync() +
            await Context.ProcurementPolicyMethodRules.CountAsync() +
            await Context.ProcurementPolicyThresholdRules.CountAsync() +
            await Context.ProcurementPolicyAuthorityRules.CountAsync() +
            await Context.ProcurementPolicyEvidenceRules.CountAsync() +
            await Context.ProcurementPolicyExceptionRules.CountAsync() +
            await Context.ProcurementPolicySodRules.CountAsync();

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }
}
