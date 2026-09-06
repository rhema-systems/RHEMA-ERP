using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
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

public sealed class ProcurementSupplierRiskPolicyResolutionTests
{
    [Fact]
    public async Task AssessmentAuditAndNotificationSavesNeverAttachOrModifyTheReadOnlySupplier()
    {
        await using var fixture = new Fixture();
        var supplier = await fixture.Context.BusinessPartners.SingleAsync();
        supplier.UpdatedBy = "Original supplier controller";
        supplier.LastModifiedById = Guid.NewGuid();
        await fixture.Context.SaveChangesAsync();
        var supplierId = supplier.Id;
        var before = await fixture.Context.BusinessPartners.AsNoTracking().SingleAsync();
        fixture.Context.ChangeTracker.Clear();
        var checkpoints = new List<string>();

        async Task CheckSupplierAsync(string checkpoint)
        {
            fixture.Context.ChangeTracker.DetectChanges();
            fixture.Context.ChangeTracker.Entries<BusinessPartner>().Should().BeEmpty(
                "supplier reads must not become a mutable part of the assessment graph at {0}", checkpoint);
            var retained = await fixture.Context.BusinessPartners.AsNoTracking().SingleAsync();
            retained.UpdatedAt.Should().Be(before.UpdatedAt, checkpoint);
            retained.UpdatedBy.Should().Be(before.UpdatedBy, checkpoint);
            retained.LastModifiedById.Should().Be(before.LastModifiedById, checkpoint);
            retained.CreatedAt.Should().Be(before.CreatedAt, checkpoint);
            retained.PartnerCode.Should().Be(before.PartnerCode, checkpoint);
            retained.PartnerName.Should().Be(before.PartnerName, checkpoint);
            retained.PartnerType.Should().Be(before.PartnerType, checkpoint);
            retained.IsActive.Should().Be(before.IsActive, checkpoint);
            retained.IsBlacklisted.Should().Be(before.IsBlacklisted, checkpoint);
            retained.ApprovalStatus.Should().Be(before.ApprovalStatus, checkpoint);
            retained.RegistrationStatus.Should().Be(before.RegistrationStatus, checkpoint);
            checkpoints.Add(checkpoint);
        }

        fixture.SupplierValidation.Setup(item => item.EvaluateEligibilityAsync(
                It.IsAny<SupplierEligibilityEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SupplierValidationResult
            {
                IsValid = true, BusinessPartnerId = supplierId, TenantId = before.TenantId,
                DecisionHash = new string('d', 64)
            });
        fixture.ControlEvents.Setup(item => item.RecordAsync(
                It.IsAny<ProcurementControlEventWriteRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (ProcurementControlEventWriteRequest request, CancellationToken cancellationToken) =>
            {
                await CheckSupplierAsync("before control-event save");
                var result = await fixture.RealControlEvents.RecordAsync(request, cancellationToken);
                await CheckSupplierAsync("after control-event save");
                return result;
            });
        fixture.Notifications.Setup(item => item.PublishAsync(
                It.IsAny<NotificationTopicEvent>(), It.IsAny<CancellationToken>()))
            .Returns(async (NotificationTopicEvent _, CancellationToken cancellationToken) =>
            {
                await CheckSupplierAsync("before notification save");
                await fixture.Context.SaveChangesAsync(cancellationToken);
                await CheckSupplierAsync("after notification save");
            });

        var request = new EvaluateProcurementSupplierRiskRequest
        {
            BusinessPartnerId = supplierId,
            IdempotencyKey = "supplier-read-only-assessment"
        };
        var assessment = await fixture.Service.EvaluateAsync(request, "supplier-read-only-assessment");
        await fixture.Context.SaveChangesAsync();
        await CheckSupplierAsync("after response mapping and later caller save");

        checkpoints.Should().Equal("before control-event save", "after control-event save",
            "before notification save", "after notification save", "after response mapping and later caller save");
        assessment.PartnerCode.Should().Be(before.PartnerCode);
        assessment.PartnerName.Should().Be(before.PartnerName);
        assessment.Alerts.Should().NotBeEmpty();
        (await fixture.Context.ProcurementControlEvents.SingleAsync()).SourceId.Should().Be(assessment.Id);
        var replay = await fixture.Service.EvaluateAsync(request, "supplier-read-only-assessment");
        replay.Id.Should().Be(assessment.Id);
        replay.PartnerCode.Should().Be(before.PartnerCode);
        replay.PartnerName.Should().Be(before.PartnerName);
        fixture.ControlEvents.Verify(item => item.RecordAsync(It.IsAny<ProcurementControlEventWriteRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Notifications.Verify(item => item.PublishAsync(It.IsAny<NotificationTopicEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("unsupported")]
    [InlineData("withdrawn")]
    [InlineData("expired")]
    public async Task UnavailablePolicyDoesNotReportHistoricalAssessmentsAsCurrentAwardBlocks(string policyState)
    {
        await using var fixture = new Fixture();
        if (policyState == "unsupported")
            fixture.Policy.ValueJson = fixture.Policy.ValueJson.Replace("FinancialStability=100", "Financial=100", StringComparison.Ordinal);
        else if (policyState == "withdrawn")
            fixture.Policy.Status = ProcurementConfigurationDecisionStatus.Withdrawn;
        else
            fixture.Policy.EffectiveTo = DateTime.UtcNow.AddDays(-1);
        await fixture.Context.SaveChangesAsync();

        var summary = await fixture.Service.GetSummaryAsync();
        summary.PolicyAvailable.Should().BeFalse();
        summary.PolicyReleaseGate.Should().NotBeNullOrWhiteSpace();
        summary.CurrentAssessmentCount.Should().Be(0);
        summary.AwardBlockedSupplierCount.Should().Be(0);
        summary.AssessedSupplierCount.Should().Be(1);
        summary.OpenAlertCount.Should().Be(1);
        var historical = await fixture.Service.GetAsync(fixture.Assessment.Id);
        historical.Id.Should().Be(fixture.Assessment.Id);
        historical.AwardBlocked.Should().BeTrue("the historical assessment outcome remains intact");
        historical.IntegrityHash.Should().Be(fixture.Assessment.IntegrityHash);
        historical.Dimensions.Should().ContainSingle(item => item.Dimension == "Financial");
        (await fixture.Context.ProcurementSupplierRiskAlerts.SingleAsync()).Status
            .Should().Be(ProcurementSupplierRiskAlertStatus.Open);
    }

    [Theory]
    [InlineData("decision")]
    [InlineData("hash")]
    public async Task ActivePolicyDoesNotCountAssessmentBoundToDifferentDecisionOrValue(string mismatch)
    {
        await using var fixture = new Fixture();
        if (mismatch == "decision") fixture.Assessment.PolicyDecisionId = Guid.NewGuid();
        else fixture.Assessment.PolicyValueHash = new string('b', 64);
        await fixture.Context.SaveChangesAsync();

        var summary = await fixture.Service.GetSummaryAsync();
        summary.PolicyAvailable.Should().BeTrue();
        summary.CurrentAssessmentCount.Should().Be(0);
        summary.AwardBlockedSupplierCount.Should().Be(0);
        summary.AssessedSupplierCount.Should().Be(1);
        summary.OpenAlertCount.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActiveMatchingPolicyStillCountsIncompleteOrOverdueAssessmentAsBlocked(bool overdue)
    {
        await using var fixture = new Fixture();
        if (overdue)
        {
            fixture.Assessment.DataComplete = true;
            fixture.Assessment.NextReviewDueAtUtc = DateTime.UtcNow.AddDays(-1);
        }
        await fixture.Context.SaveChangesAsync();

        var summary = await fixture.Service.GetSummaryAsync();
        summary.PolicyAvailable.Should().BeTrue();
        summary.AwardBlockedSupplierCount.Should().Be(1);
        summary.CurrentAssessmentCount.Should().Be(overdue ? 0 : 1);
        summary.OpenAlertCount.Should().Be(1);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;

        public Fixture()
        {
            var tenantId = Guid.NewGuid();
            Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
            var supplier = new BusinessPartner
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                PartnerCode = "SUP-RISK-HISTORY", PartnerName = "Risk history supplier",
                PartnerType = "Supplier", IsActive = true
            };
            var profile = new ProcurementConfigurationProfile
            {
                Id = Guid.NewGuid(), TenantId = tenantId, ProfileKey = Guid.NewGuid(),
                ProfileCode = "TEST-RISK-POLICY", Name = "Test risk policy", Version = 1,
                LifecycleStatus = ProcurementConfigurationProfileStatus.Published,
                EffectiveFrom = DateTime.UtcNow.AddDays(-30)
            };
            var value = ProcurementSupplierRiskFormulaTests.ValidRiskPolicy("FinancialStability=100");
            value.EffectiveFrom = profile.EffectiveFrom;
            value.EligibilityAction = ProcurementSupplierRiskEligibilityAction.AwardHardStop;
            var valid = ProcurementConfigurationDecisionRegistry.Validate("DEC-011", 1,
                JsonSerializer.SerializeToElement(value, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) }
                }));
            valid.IsValid.Should().BeTrue(string.Join("; ", valid.Errors));
            Policy = new ProcurementConfigurationDecision
            {
                Id = Guid.NewGuid(), TenantId = tenantId, ProfileId = profile.Id, Profile = profile,
                DecisionKey = "DEC-011", SchemaVersion = 1, OwnerGroup = "Procurement",
                Status = ProcurementConfigurationDecisionStatus.Approved,
                ApprovalStatus = ProcurementConfigurationApprovalStatus.Approved,
                EvidenceStatus = ProcurementConfigurationEvidenceStatus.Verified,
                ValueJson = valid.CanonicalJson!, EffectiveFrom = value.EffectiveFrom,
                ApprovedAt = DateTime.UtcNow.AddDays(-2), ApprovedById = Guid.NewGuid()
            };
            Assessment = new ProcurementSupplierRiskAssessment
            {
                Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerId = supplier.Id,
                BusinessPartner = supplier, AssessmentReference = "TEST-HISTORICAL-RISK-001",
                AssessedAtUtc = DateTime.UtcNow.AddDays(-1), PeriodStartUtc = DateTime.UtcNow.AddMonths(-12),
                PeriodEndUtc = DateTime.UtcNow.AddDays(-1), NextReviewDueAtUtc = DateTime.UtcNow.AddMonths(1),
                PolicyDecisionId = Policy.Id, PolicyDecision = Policy, PolicyProfileId = profile.Id,
                PolicyProfile = profile, PolicyProfileCode = profile.ProfileCode, PolicyProfileVersion = 1,
                PolicySnapshotJson = Policy.ValueJson,
                PolicyValueHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Policy.ValueJson))).ToLowerInvariant(),
                DataComplete = false, EligibilityAction = ProcurementSupplierRiskEligibilityAction.AwardHardStop,
                DimensionScoresJson = "[{\"dimension\":\"Financial\",\"weightPercent\":100,\"source\":\"Unsupported\"}]",
                IntegrityHash = new string('a', 64), IdempotencyKey = "historical-risk-test",
                CorrelationId = "historical-risk-test", AssessedByUserId = Guid.NewGuid(), AssessedByName = "Test assessor"
            };
            var alert = new ProcurementSupplierRiskAlert
            {
                Id = Guid.NewGuid(), TenantId = tenantId, AssessmentId = Assessment.Id, Assessment = Assessment,
                BusinessPartnerId = supplier.Id, BusinessPartner = supplier,
                AlertType = ProcurementSupplierRiskAlertType.DataIncomplete,
                Status = ProcurementSupplierRiskAlertStatus.Open,
                RuleCode = "SUPPLIER_RISK_DATA_INCOMPLETE", Severity = "Critical", Message = "Historical incomplete risk assessment",
                OpenedAtUtc = Assessment.AssessedAtUtc, CreationCorrelationId = "historical-risk-test",
                LastOperationCorrelationId = "historical-risk-test", IntegrityHash = new string('c', 64),
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            Context.AddRange(supplier, profile, Policy, Assessment, alert);
            Context.SaveChanges();
            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(item => item.TenantId).Returns(tenantId);
            current.SetupGet(item => item.UserId).Returns(Guid.NewGuid());
            current.SetupGet(item => item.IsAuthenticated).Returns(true);
            current.SetupGet(item => item.IsExternalUser).Returns(false);
            current.SetupGet(item => item.Username).Returns("risk-assessor");
            current.SetupGet(item => item.FullName).Returns("Risk assessor");
            current.SetupGet(item => item.Roles).Returns(Array.Empty<string>());
            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(item => item.CheckCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true });
            access.Setup(item => item.EnforceCapabilityAsync(
                    It.Is<ProcurementAccessCapabilityRequest>(request =>
                        request.PermissionCode == "procurement.supplier.review" &&
                        request.SourceType == "ProcurementSupplierRisk"),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true });
            _unitOfWork = new UnitOfWork(Context);
            RealControlEvents = new ProcurementControlEventService(_unitOfWork, current.Object,
                NullLogger<ProcurementControlEventService>.Instance);
            Service = new ProcurementSupplierRiskService(_unitOfWork, current.Object, access.Object,
                Mock.Of<IProcurementSodGuardService>(), ControlEvents.Object,
                SupplierValidation.Object, Mock.Of<IWorkflowInstanceService>(),
                Notifications.Object, NullLogger<ProcurementSupplierRiskService>.Instance);
        }

        public ApplicationDbContext Context { get; }
        public ProcurementConfigurationDecision Policy { get; }
        public ProcurementSupplierRiskAssessment Assessment { get; }
        public ProcurementSupplierRiskService Service { get; }
        public ProcurementControlEventService RealControlEvents { get; }
        public Mock<IProcurementControlEventService> ControlEvents { get; } = new();
        public Mock<ISupplierValidationService> SupplierValidation { get; } = new();
        public Mock<INotificationTopicPublisher> Notifications { get; } = new();

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }
}
