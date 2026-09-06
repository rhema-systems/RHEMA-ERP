using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementConfigurationWithdrawalTests
{
    [Fact]
    public async Task WithdrawsOnlyDec011AndPreservesProfileOtherDecisionsEvidenceAndRiskHistory()
    {
        await using var fixture = new Fixture();
        var others = fixture.OtherDecisionSnapshot();
        var profile = fixture.ProfileSnapshot();
        var approvedAt = fixture.RiskDecision.ApprovedAt;
        var value = fixture.RiskDecision.ValueJson;

        var result = await fixture.Withdraw();

        var withdrawn = result.Decisions.Single(item => item.DecisionKey == "DEC-011");
        withdrawn.Status.Should().Be(ProcurementConfigurationDecisionStatus.Withdrawn);
        withdrawn.ApprovedAt.Should().Be(approvedAt);
        withdrawn.ApprovalStatus.Should().Be(ProcurementConfigurationApprovalStatus.Approved);
        withdrawn.Value.GetRawText().Should().Be(value);
        withdrawn.Evidence.Should().ContainSingle();
        result.LifecycleStatus.Should().Be(ProcurementConfigurationProfileStatus.Published);
        result.IsComplete.Should().BeTrue();
        fixture.OtherDecisionSnapshot().Should().Be(others);
        fixture.ProfileSnapshot().Should().Be(profile);
        fixture.Context.ProcurementConfigurationDecisions.Should().HaveCount(14);
        fixture.Context.ProcurementConfigurationEvidenceLinks.Should().HaveCount(14);
        var assessment = await fixture.Context.ProcurementSupplierRiskAssessments.SingleAsync();
        assessment.PolicyDecisionId.Should().Be(fixture.RiskDecision.Id);
        assessment.PolicySnapshotJson.Should().Be(value);
        assessment.SnapshotJson.Should().Be("{\"historicalIncompleteAssessment\":true}");
        assessment.DataComplete.Should().BeFalse();
        (await fixture.Context.ProcurementConfigurationDecisions.CountAsync(item =>
            item.DecisionKey == "DEC-011" && item.Status == ProcurementConfigurationDecisionStatus.Approved))
            .Should().Be(0);

        var revision = await fixture.Context.ProcurementConfigurationRevisions.SingleAsync();
        revision.Action.Should().Be("WithdrawDecision");
        revision.Result.Should().Be("Succeeded");
        revision.DecisionId.Should().Be(fixture.RiskDecision.Id);
        revision.ActorUserId.Should().Be(fixture.ActorId);
        revision.Reason.Should().Be(Fixture.Reason);
        revision.BeforeJson.Should().Contain("\"status\":2");
        revision.AfterJson.Should().Contain("\"status\":4");
        result.RecentHistory.Should().ContainSingle(item => item.Reason == Fixture.Reason);
    }

    [Fact]
    public async Task HistoricalUnsupportedValueRemainsReadableWithoutAnActiveValidationError()
    {
        await using var fixture = new Fixture();
        var result = await fixture.Withdraw();
        result.Validation.Errors.Should().NotContain(item => item.DecisionKey == "DEC-011");
        result.Validation.Warnings.Should().ContainSingle(item =>
            item.DecisionKey == "DEC-011" && item.Code == "DECISION_WITHDRAWN");
        result.Decisions.Single(item => item.DecisionKey == "DEC-011")
            .Value.GetRawText().Should().Contain("Financial=100");
    }

    [Fact]
    public async Task ClonePreservesWithdrawalInsteadOfReactivatingHistoricalRiskValues()
    {
        await using var fixture = new Fixture();
        await fixture.Withdraw();
        var clone = await fixture.Service.CloneDraftAsync(fixture.Profile.Id,
            new CloneProcurementConfigurationProfileRequest { ChangeSummary = "Later profile revision" }, "clone-withdrawal");
        var clonedRisk = clone.Decisions.Single(item => item.DecisionKey == "DEC-011");
        clonedRisk.Status.Should().Be(ProcurementConfigurationDecisionStatus.Withdrawn);
        clonedRisk.Value.GetRawText().Should().Be(fixture.RiskDecision.ValueJson);
        clone.Validation.Errors.Should().NotContain(item => item.DecisionKey == "DEC-011");
        clone.Decisions.Where(item => item.DecisionKey != "DEC-011").Should()
            .OnlyContain(item => item.Status == ProcurementConfigurationDecisionStatus.Proposed);
        (await fixture.Context.ProcurementConfigurationDecisions.SingleAsync(item => item.Id == clonedRisk.Id))
            .SourceDecisionId.Should().Be(fixture.RiskDecision.Id);
    }

    [Theory]
    [InlineData("DEC-001")]
    [InlineData("DEC-014")]
    public async Task OtherDecisionsCannotUseWithdrawal(string decisionKey)
    {
        await using var fixture = new Fixture();
        await fixture.Service.Invoking(service => service.WithdrawDecisionAsync(
            fixture.Profile.Id, decisionKey, fixture.Request(), "wrong-decision"))
            .Should().ThrowAsync<ProcurementConfigurationValidationException>();
        fixture.Context.ProcurementConfigurationRevisions.Should().BeEmpty();
        fixture.RiskDecision.Status.Should().Be(ProcurementConfigurationDecisionStatus.Approved);
    }

    [Theory]
    [InlineData(ProcurementConfigurationProfileStatus.Draft)]
    [InlineData(ProcurementConfigurationProfileStatus.Retired)]
    public async Task OnlyPublishedProfilesCanWithdraw(ProcurementConfigurationProfileStatus status)
    {
        await using var fixture = new Fixture();
        fixture.Profile.LifecycleStatus = status;
        await fixture.Context.SaveChangesAsync();
        await fixture.Invoking(item => item.Withdraw()).Should()
            .ThrowAsync<ProcurementConfigurationConflictException>();
        fixture.Context.ProcurementConfigurationRevisions.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ProcurementConfigurationDecisionStatus.Draft)]
    [InlineData(ProcurementConfigurationDecisionStatus.Proposed)]
    [InlineData(ProcurementConfigurationDecisionStatus.Rejected)]
    [InlineData(ProcurementConfigurationDecisionStatus.Withdrawn)]
    public async Task OnlyApprovedCurrentDecisionsCanWithdraw(ProcurementConfigurationDecisionStatus status)
    {
        await using var fixture = new Fixture();
        fixture.RiskDecision.Status = status;
        await fixture.Context.SaveChangesAsync();
        await fixture.Invoking(item => item.Withdraw()).Should()
            .ThrowAsync<ProcurementConfigurationConflictException>();
        fixture.Context.ProcurementConfigurationRevisions.Should().BeEmpty();
    }

    [Fact]
    public async Task RepeatWithdrawalIsConflictAndDoesNotDuplicateAudit()
    {
        await using var fixture = new Fixture();
        await fixture.Withdraw();
        await fixture.Invoking(item => item.Withdraw()).Should()
            .ThrowAsync<ProcurementConfigurationConflictException>();
        fixture.Context.ProcurementConfigurationRevisions.Should().ContainSingle();
    }

    [Fact]
    public async Task StaleDecisionVersionCannotWithdraw()
    {
        await using var fixture = new Fixture();
        var request = fixture.Request();
        request.RowVersion = Convert.ToBase64String([9, 8, 7, 6]);
        await fixture.Service.Invoking(service => service.WithdrawDecisionAsync(
            fixture.Profile.Id, "DEC-011", request, "stale-version"))
            .Should().ThrowAsync<ProcurementConfigurationConflictException>();
        fixture.RiskDecision.Status.Should().Be(ProcurementConfigurationDecisionStatus.Approved);
        fixture.Context.ProcurementConfigurationRevisions.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AuditReasonIsRequired(string reason)
    {
        await using var fixture = new Fixture();
        var request = fixture.Request();
        request.Reason = reason;
        await fixture.Service.Invoking(service => service.WithdrawDecisionAsync(
            fixture.Profile.Id, "DEC-011", request, "missing-reason"))
            .Should().ThrowAsync<ProcurementConfigurationValidationException>();
        fixture.Context.ProcurementConfigurationRevisions.Should().BeEmpty();
    }

    [Fact]
    public async Task ExistingPublisherAuthorizationAndTenantIsolationRemainMandatory()
    {
        await using var fixture = new Fixture();
        fixture.SetRoles("TDC_EMPLOYEE");
        await fixture.Invoking(item => item.Withdraw()).Should()
            .ThrowAsync<ProcurementConfigurationAuthorizationException>();
        fixture.SetRoles(ProcurementAccessControlRegistry.IctAdministratorRole);
        fixture.SwitchTenant(Guid.NewGuid());
        await fixture.Invoking(item => item.Withdraw()).Should()
            .ThrowAsync<ProcurementConfigurationNotFoundException>();
        fixture.Context.ProcurementConfigurationRevisions.Should().BeEmpty();
        fixture.RiskDecision.Status.Should().Be(ProcurementConfigurationDecisionStatus.Approved);
    }

    [Fact]
    public async Task DecisionEditingCannotForgeWithdrawnStatus()
    {
        await using var fixture = new Fixture();
        fixture.Profile.LifecycleStatus = ProcurementConfigurationProfileStatus.Draft;
        fixture.RiskDecision.Status = ProcurementConfigurationDecisionStatus.Proposed;
        await fixture.Context.SaveChangesAsync();
        await fixture.Service.Invoking(service => service.SaveDecisionAsync(
            fixture.Profile.Id, "DEC-011", new SaveProcurementConfigurationDecisionRequest
            {
                OwnerGroup = "Procurement", Status = ProcurementConfigurationDecisionStatus.Withdrawn,
                ApprovalStatus = ProcurementConfigurationApprovalStatus.Pending,
                RowVersion = Convert.ToBase64String(fixture.RiskDecision.RowVersion),
                Value = JsonSerializer.SerializeToElement(new { riskDimensions = new[] { "Financial=100" } })
            }, "forged-withdrawal"))
            .Should().ThrowAsync<ProcurementConfigurationValidationException>()
            .Where(exception => exception.Validation.Errors.Any(item => item.Message.Contains("Withdrawn cannot")));
        fixture.Context.ProcurementConfigurationRevisions.Should().BeEmpty();
    }

    [Fact]
    public void MigrationPermitsOnlyDec011WithdrawalAndCannotEraseHistoryOnRollback()
    {
        var migration = new AddGovernedSupplierRiskDecisionWithdrawal();
        migration.UpOperations.Should().HaveCount(2);
        migration.UpOperations.OfType<AddCheckConstraintOperation>().Single().Sql.Should()
            .Be("[Status] IN (0, 1, 2, 3) OR ([Status] = 4 AND [DecisionKey] = 'DEC-011')");
        var rollbackGuard = migration.DownOperations.OfType<SqlOperation>().Single().Sql;
        rollbackGuard.Should().Contain("[Status] = 4").And.Contain("WithdrawDecision")
            .And.Contain("THROW 51213").And.NotContain("DELETE");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public const string Reason = "Architecture baseline alignment: withdraw unsupported universal financial gate; retain normal supplier and award controls.";
        private Guid _tenantId = Guid.NewGuid();
        private readonly HashSet<string> _roles = [ProcurementAccessControlRegistry.IctAdministratorRole];
        private readonly UnitOfWork _unitOfWork;
        public Guid ActorId { get; } = Guid.NewGuid();
        public ApplicationDbContext Context { get; }
        public ProcurementConfigurationService Service { get; }
        public ProcurementConfigurationProfile Profile { get; }
        public ProcurementConfigurationDecision RiskDecision { get; }

        public Fixture()
        {
            Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(item => item.UserId).Returns(ActorId);
            current.SetupGet(item => item.TenantId).Returns(() => _tenantId);
            current.SetupGet(item => item.IsAuthenticated).Returns(true);
            current.SetupGet(item => item.FullName).Returns("Configuration Publisher");
            current.SetupGet(item => item.Username).Returns("configuration-publisher");
            current.SetupGet(item => item.Roles).Returns(() => _roles);
            current.Setup(item => item.HasRole(It.IsAny<string>())).Returns((string role) => _roles.Contains(role));
            _unitOfWork = new UnitOfWork(Context);
            Service = new ProcurementConfigurationService(_unitOfWork, current.Object,
                NullLogger<ProcurementConfigurationService>.Instance);
            var now = DateTime.UtcNow;
            Profile = new ProcurementConfigurationProfile
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, ProfileCode = "TDC-WITHDRAWAL-TEST",
                Name = "Retained published configuration", Version = 3,
                LifecycleStatus = ProcurementConfigurationProfileStatus.Published,
                EffectiveFrom = now.AddDays(-1), PublishedAt = now.AddDays(-1), PublishedById = ActorId,
                RowVersion = [1, 2, 3, 4], CreatedAt = now.AddDays(-2), CreatedBy = "Test"
            };
            Context.ProcurementConfigurationProfiles.Add(Profile);
            foreach (var definition in ProcurementConfigurationDecisionRegistry.Definitions)
            {
                var decision = new ProcurementConfigurationDecision
                {
                    Id = Guid.NewGuid(), TenantId = _tenantId, ProfileId = Profile.Id,
                    DecisionKey = definition.DecisionKey, OwnerGroup = definition.OwnerGroup,
                    Status = ProcurementConfigurationDecisionStatus.Approved,
                    ApprovalStatus = ProcurementConfigurationApprovalStatus.Approved,
                    EvidenceStatus = ProcurementConfigurationEvidenceStatus.Verified,
                    ValueJson = definition.DecisionKey == "DEC-011" ? "{\"riskDimensions\":[\"Financial=100\"]}" : "{}",
                    EffectiveFrom = now.AddDays(-1), DecisionDate = now.AddDays(-1),
                    ApprovedAt = now.AddDays(-1), ApprovedById = ActorId, ApprovalReference = "Retained approval",
                    RowVersion = [1, 2, 3, 4], CreatedAt = now.AddDays(-2), CreatedBy = "Test"
                };
                Context.ProcurementConfigurationDecisions.Add(decision);
                Context.ProcurementConfigurationEvidenceLinks.Add(new ProcurementConfigurationEvidenceLink
                {
                    Id = Guid.NewGuid(), TenantId = _tenantId, ProfileId = Profile.Id, DecisionId = decision.Id,
                    EvidenceType = "ExternalReference", ExternalReference = $"TEST-{definition.DecisionKey}",
                    UploadedById = ActorId, UploadedAt = now.AddDays(-1), CreatedBy = "Test"
                });
            }
            Context.SaveChanges();
            RiskDecision = Context.ProcurementConfigurationDecisions.Single(item => item.DecisionKey == "DEC-011");
            Context.ProcurementSupplierRiskAssessments.Add(new ProcurementSupplierRiskAssessment
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, BusinessPartnerId = Guid.NewGuid(),
                PolicyDecisionId = RiskDecision.Id, PolicyProfileId = Profile.Id,
                PolicySnapshotJson = RiskDecision.ValueJson, DataComplete = false,
                SnapshotJson = "{\"historicalIncompleteAssessment\":true}", CreatedBy = "Test"
            });
            Context.SaveChanges();
        }

        public WithdrawProcurementConfigurationDecisionRequest Request() => new()
        {
            RowVersion = Convert.ToBase64String(RiskDecision.RowVersion), Reason = Reason
        };
        public Task<ProcurementConfigurationProfileDto> Withdraw() => Service.WithdrawDecisionAsync(
            Profile.Id, "DEC-011", Request(), "withdraw-risk-policy");
        public string ProfileSnapshot() => JsonSerializer.Serialize(new
        {
            Profile.Id, Profile.Version, Profile.LifecycleStatus, Profile.EffectiveFrom,
            Profile.EffectiveTo, Profile.PublishedAt, Profile.RetiredAt, Profile.RowVersion, Profile.UpdatedAt
        });
        public string OtherDecisionSnapshot() => JsonSerializer.Serialize(Context.ProcurementConfigurationDecisions
            .Where(item => item.DecisionKey != "DEC-011").OrderBy(item => item.DecisionKey)
            .Select(item => new
            {
                item.Id, item.Status, item.ApprovalStatus, item.EvidenceStatus, item.ValueJson,
                item.EffectiveFrom, item.EffectiveTo, item.ApprovedAt, item.ApprovedById,
                item.ApprovalReference, item.SourceDecisionId, item.Notes, item.RowVersion, item.UpdatedAt
            }).ToList());
        public void SetRoles(params string[] roles)
        {
            _roles.Clear();
            foreach (var role in roles) _roles.Add(role);
        }
        public void SwitchTenant(Guid tenantId) => _tenantId = tenantId;
        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }
}
