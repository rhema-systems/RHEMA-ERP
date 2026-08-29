using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementSodGuardServiceTests
{
    private static readonly DateTime Moment = new(2026, 7, 21, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RequiredRegistryContainsTheSixDistinctSrsControls()
    {
        ProcurementSodRequiredControlRegistry.Definitions.Should().HaveCount(6);
        ProcurementSodRequiredControlRegistry.Definitions.Select(item => item.Code).Should().OnlyHaveUniqueItems();
        ProcurementSodRequiredControlRegistry.Definitions.Should().OnlyContain(item =>
            item.SourceRequirement == "GOV-005" && item.SourceDecisionKey == "DEC-004");
    }

    [Fact]
    public async Task CoverageResolvesAllSixEffectiveTenantHardStops()
    {
        await using var fixture = new GuardFixture();
        await fixture.AddCompletePolicyAsync();

        var coverage = await fixture.Service.GetCoverageAsync(Moment);

        coverage.Status.Should().Be("Complete");
        coverage.IsComplete.Should().BeTrue();
        coverage.Controls.Should().HaveCount(6).And.OnlyContain(item =>
            item.IsConfigured && item.IsEffective && item.IsHardStop && item.RuleId.HasValue);
    }

    [Theory]
    [MemberData(nameof(RequiredControlCodes))]
    public async Task EveryRequiredConflictIsBlockedAndDurablyAudited(string controlCode)
    {
        await using var fixture = new GuardFixture();
        await fixture.AddCompletePolicyAsync();

        var result = await fixture.Service.EnforceAsync(Request(controlCode, fixture.UserId), $"trace-{controlCode}");

        result.Allowed.Should().BeFalse();
        result.IsHardStop.Should().BeTrue();
        result.WasAudited.Should().BeTrue();
        result.Code.Should().Be("SOD_CONFLICT");
        result.RuleCode.Should().Be(controlCode);
        (await fixture.Context.AuditLogs.SingleAsync()).Action.Should().Be("SOD_BYPASS_BLOCKED");
        (await fixture.Context.ProcurementControlEvents.SingleAsync()).Result
            .Should().Be(ProcurementControlEventResult.Denied);
    }

    [Fact]
    public async Task IndependentActorIsAllowedAndDoesNotCreateBypassAudit()
    {
        await using var fixture = new GuardFixture();
        await fixture.AddCompletePolicyAsync();

        var result = await fixture.Service.EnforceAsync(
            Request(ProcurementSodRequiredControlRegistry.Definitions[0].Code, Guid.NewGuid()), "trace-allowed");

        result.Allowed.Should().BeTrue();
        result.WasAudited.Should().BeFalse();
        (await fixture.Context.AuditLogs.CountAsync()).Should().Be(0);
        (await fixture.Context.ProcurementControlEvents.SingleAsync()).Result
            .Should().Be(ProcurementControlEventResult.Allowed);
    }

    [Fact]
    public async Task SoleActorConflictAllowsOnlyARecordedDistinctNonProhibitedActor()
    {
        await using var fixture = new GuardFixture();
        await fixture.AddCompletePolicyAsync();
        var independentActorId = Guid.NewGuid();
        var request = Request(
            "SOD-EVALUATOR-AWARD-APPROVER",
            fixture.UserId);
        request.RequireSoleActorConflict = true;
        request.IndependentActorUserIds = [independentActorId];

        var allowed = await fixture.Service.EnforceAsync(
            request, "trace-sole-actor-independent");

        allowed.Allowed.Should().BeTrue();
        allowed.Code.Should().Be("SOD_ALLOWED");

        request.IndependentActorUserIds =
            [fixture.UserId, fixture.UserId];
        var blocked = await fixture.Service.EnforceAsync(
            request, "trace-sole-actor-no-independent");

        blocked.Allowed.Should().BeFalse();
        blocked.Code.Should().Be("SOD_CONFLICT");
        blocked.WasAudited.Should().BeTrue();
    }

    [Fact]
    public async Task ReadOnlyCheckBlocksWithoutWritingWhileEnforceWritesExactlyOneAttempt()
    {
        await using var fixture = new GuardFixture();
        await fixture.AddCompletePolicyAsync();
        var request = Request(ProcurementSodRequiredControlRegistry.Definitions[0].Code, fixture.UserId);

        var preview = await fixture.Service.CheckAsync(request, "trace-check");
        var enforced = await fixture.Service.EnforceAsync(request, "trace-enforce");

        preview.Allowed.Should().BeFalse();
        preview.WasAudited.Should().BeFalse();
        enforced.WasAudited.Should().BeTrue();
        (await fixture.Context.AuditLogs.CountAsync()).Should().Be(1);
        (await fixture.Context.ProcurementControlEvents.CountAsync()).Should().Be(1);
        (await fixture.Service.GetBlockedAttemptsAsync()).Should().ContainSingle(item =>
            item.ControlCode == request.ControlCode && item.CorrelationId == "trace-enforce");
    }

    [Fact]
    public async Task MissingPolicyUsesSharedBaselineAndStillBlocksAndAuditsIdentityConflicts()
    {
        await using var fixture = new GuardFixture();
        await fixture.AddCompletePolicyAsync();
        fixture.SwitchTenant(Guid.NewGuid());

        var allowed = await fixture.Service.EnforceAsync(
            Request(ProcurementSodRequiredControlRegistry.Definitions[0].Code, Guid.NewGuid()), "trace-tenant-allowed");
        allowed.Allowed.Should().BeTrue();
        allowed.Code.Should().Be("SOD_ALLOWED");
        allowed.PolicySetId.Should().BeNull();
        allowed.Message.Should().Contain("shared maker-checker baseline");

        var blocked = await fixture.Service.EnforceAsync(
            Request(ProcurementSodRequiredControlRegistry.Definitions[0].Code, fixture.UserId), "trace-tenant");

        blocked.Allowed.Should().BeFalse();
        blocked.Code.Should().Be("SOD_CONFLICT");
        blocked.PolicySetId.Should().BeNull();
        blocked.WasAudited.Should().BeTrue();
        (await fixture.Context.AuditLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ApplyingRequiredControlsAddsAllSixToDraftUsingExistingPolicyLifecycleService()
    {
        await using var fixture = new GuardFixture();
        var policyId = Guid.NewGuid();
        fixture.PolicyService.Setup(item => item.GetPolicySetAsync(policyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementPolicySetDto
            {
                Id = policyId,
                LifecycleStatus = ProcurementPolicyLifecycleStatus.Draft,
                EffectiveFrom = Moment.AddDays(-1),
                EffectiveTo = Moment.AddYears(1)
            });
        fixture.PolicyService.Setup(item => item.SaveRuleAsync(policyId, null,
                It.IsAny<SaveProcurementPolicyRuleRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid? _, SaveProcurementPolicyRuleRequest request, string _, CancellationToken _) =>
                new ProcurementPolicyRuleDto
                {
                    Id = Guid.NewGuid(), Kind = request.Kind,
                    RuleCode = request.SegregationOfDuties!.RuleCode
                });

        var result = await fixture.Service.ApplyRequiredControlsAsync(policyId,
            new ApplyRequiredProcurementSodControlsRequest { Reason = "Apply SRS controls" }, "trace-provision");

        result.CreatedCount.Should().Be(6);
        result.CreatedControlCodes.Should().BeEquivalentTo(
            ProcurementSodRequiredControlRegistry.Definitions.Select(item => item.Code));
        fixture.PolicyService.Verify(item => item.SaveRuleAsync(policyId, null,
            It.Is<SaveProcurementPolicyRuleRequest>(request =>
                request.Kind == ProcurementPolicyRuleKind.SegregationOfDuties &&
                request.SegregationOfDuties!.Enforcement == ProcurementSodEnforcement.HardStop),
            "trace-provision", It.IsAny<CancellationToken>()), Times.Exactly(6));
    }

    public static IEnumerable<object[]> RequiredControlCodes() =>
        ProcurementSodRequiredControlRegistry.Definitions.Select(item => new object[] { item.Code });

    private static ProcurementSodGuardRequest Request(string controlCode, Guid prohibitedActorId) => new()
    {
        ControlCode = controlCode,
        SourceType = "AcceptanceFixture",
        SourceReference = $"SOD-{controlCode}",
        ProhibitedActorUserIds = new() { prohibitedActorId }
    };

    private sealed class GuardFixture : IAsyncDisposable
    {
        private Guid _activeTenantId;
        private readonly Mock<ICurrentUserProvider> _currentUser = new();
        private readonly UnitOfWork _unitOfWork;
        private readonly HashSet<string> _roles = new(StringComparer.OrdinalIgnoreCase) { "TenantAdmin" };

        public GuardFixture()
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
            _currentUser.SetupGet(item => item.Username).Returns("sod.tester@tdc.test");
            _currentUser.SetupGet(item => item.FullName).Returns("SOD Tester");
            _currentUser.SetupGet(item => item.Roles).Returns(() => _roles);
            _currentUser.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns((string role) => _roles.Contains(role));
            _unitOfWork = new UnitOfWork(Context);
            PolicyService = new Mock<IProcurementPolicyService>();
            var controlEvents = new ProcurementControlEventService(_unitOfWork, _currentUser.Object,
                NullLogger<ProcurementControlEventService>.Instance);
            Service = new ProcurementSodGuardService(_unitOfWork, _currentUser.Object,
                PolicyService.Object, controlEvents, NullLogger<ProcurementSodGuardService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public ApplicationDbContext Context { get; }
        public Mock<IProcurementPolicyService> PolicyService { get; }
        public ProcurementSodGuardService Service { get; }
        public void SwitchTenant(Guid tenantId) => _activeTenantId = tenantId;

        public async Task AddCompletePolicyAsync()
        {
            var policy = new ProcurementPolicySet
            {
                TenantId = TenantId,
                PolicyKey = Guid.NewGuid(),
                Code = "TDC-SOD",
                Name = "TDC SOD acceptance policy",
                Version = 1,
                LifecycleStatus = ProcurementPolicyLifecycleStatus.Published,
                ScopeType = ProcurementPolicyScopeType.TenantBaseline,
                SourceConfigurationProfileId = Guid.NewGuid(),
                DefaultCurrencyCode = "GHS",
                EffectiveFrom = Moment.AddDays(-1),
                EffectiveTo = Moment.AddYears(1),
                IsDefault = true,
                PublishedAt = Moment.AddHours(-1)
            };
            Context.ProcurementPolicySets.Add(policy);
            foreach (var definition in ProcurementSodRequiredControlRegistry.Definitions)
            {
                Context.ProcurementPolicySodRules.Add(new ProcurementPolicySodRule
                {
                    TenantId = TenantId,
                    PolicySetId = policy.Id,
                    RuleCode = definition.Code,
                    Name = definition.Name,
                    InitiatorRole = definition.InitiatorRole,
                    ConflictingRole = definition.ConflictingRole,
                    EntityType = definition.EntityType,
                    Action = definition.Action,
                    Enforcement = ProcurementSodEnforcement.HardStop,
                    Explanation = definition.Explanation,
                    SourceDecisionKey = definition.SourceDecisionKey,
                    IsEnabled = true,
                    Priority = 100,
                    EffectiveFrom = Moment.AddDays(-1),
                    EffectiveTo = Moment.AddYears(1)
                });
            }
            await Context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
        }
    }
}
