using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class SupplierValidationServiceTests
{
    [Fact]
    public async Task BlacklistedSupplierIsDeniedWithExplainableHashedDecision()
    {
        await using var fixture = new Fixture();
        var supplier = fixture.AddSupplier(blacklisted: true);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.EvaluateEligibilityAsync(new SupplierEligibilityEvaluationRequest
        {
            BusinessPartnerId = supplier.Id,
            Boundary = SupplierEligibilityBoundary.Invitation
        });

        result.IsValid.Should().BeFalse();
        result.ValidationCode.Should().Be("SUPPLIER_BLACKLISTED");
        result.Findings.Should().Contain(item =>
            item.Code == "SUPPLIER_BLACKLISTED" && item.Blocking);
        result.DecisionHash.Should().MatchRegex("^[0-9A-F]{64}$");
        result.DecisionKeys.Should().HaveCount(14);
    }

    [Fact]
    public async Task RequiredPrequalificationUsesCurrentQualifiedListAndRetainsPolicyLineage()
    {
        await using var fixture = new Fixture();
        var categoryId = Guid.NewGuid();
        var supplier = fixture.AddSupplier(categoryId: categoryId);
        fixture.AddQualifiedEntry(supplier.Id, categoryId);
        var policy = fixture.AddDec011Policy();
        fixture.AddDueDiligenceReview(supplier.Id, policy);
        fixture.AddPerformanceScorecard(supplier.Id, policy);
        fixture.AddRiskAssessment(supplier.Id, policy);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.EvaluateEligibilityAsync(new SupplierEligibilityEvaluationRequest
        {
            BusinessPartnerId = supplier.Id,
            Boundary = SupplierEligibilityBoundary.Award,
            CategoryIds = [categoryId],
            RequiresPrequalification = true,
            SkipFormalAvlMembership = true
        });

        result.IsValid.Should().BeTrue(string.Join("; ",
            result.Findings.Select(item => $"{item.Code}: {item.Message}")));
        result.QualifiedListEntries.Should().ContainSingle(item =>
            item.CategoryId == categoryId && item.IsCurrent &&
            item.PolicySetCode == "TDC-POLICY");
        result.Warnings.Should().Contain(item =>
            item.Contains("governed onboarding", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LinkedRegistrationWithIncompleteEvidenceFailsClosed()
    {
        await using var fixture = new Fixture();
        var supplier = fixture.AddSupplier();
        var registration = new BusinessPartnerRegistration
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            RegistrationNumber = "REG-001",
            ApplicantName = supplier.PartnerName,
            PartnerType = "Supplier",
            Status = "Approved",
            BusinessPartnerId = supplier.Id,
            ApprovedDate = DateTime.UtcNow
        };
        fixture.Context.Add(registration);
        fixture.EvidencePacks.Setup(item => item.GetRegistrationReadinessAsync(
                registration.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSupplierEvidenceReadinessDto
            {
                RegistrationId = registration.Id,
                IsBound = true,
                IsReady = false,
                BlockingReasons = ["Tax clearance evidence has expired."]
            });
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.EvaluateEligibilityAsync(new SupplierEligibilityEvaluationRequest
        {
            BusinessPartnerId = supplier.Id,
            Boundary = SupplierEligibilityBoundary.Contract
        });

        result.IsValid.Should().BeFalse();
        result.ValidationCode.Should().Be("SUPPLIER_EVIDENCE_NOT_READY");
        result.RegistrationId.Should().Be(registration.Id);
    }

    [Fact]
    public async Task ForeignTenantSupplierIsNotDisclosed()
    {
        await using var fixture = new Fixture();
        var supplier = fixture.AddSupplier(tenantId: Guid.NewGuid());
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.EvaluateEligibilityAsync(new SupplierEligibilityEvaluationRequest
        {
            BusinessPartnerId = supplier.Id,
            Boundary = SupplierEligibilityBoundary.StatusReview
        });

        result.ValidationCode.Should().Be("SUPPLIER_NOT_FOUND");
        result.PartnerName.Should().BeEmpty();
    }

    [Fact]
    public async Task EnforcementRecordsSharedControlEvent()
    {
        await using var fixture = new Fixture();
        var supplier = fixture.AddSupplier();
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.EnforceEligibilityAsync(new SupplierEligibilityEvaluationRequest
        {
            BusinessPartnerId = supplier.Id,
            Boundary = SupplierEligibilityBoundary.FrameworkCallOff,
            SourceType = "PurchaseOrder",
            SourceReference = "BLANKET-001",
            CorrelationId = "eligibility-test"
        });
        await fixture.Service.EnforceEligibilityAsync(new SupplierEligibilityEvaluationRequest
        {
            BusinessPartnerId = supplier.Id,
            Boundary = SupplierEligibilityBoundary.FrameworkCallOff,
            SourceType = "PurchaseOrder",
            SourceReference = "BLANKET-001",
            CorrelationId = "eligibility-test"
        });

        result.IsValid.Should().BeTrue(string.Join("; ",
            result.Findings.Select(item => $"{item.Code}: {item.Message}")));
        fixture.ControlEvents.Verify(item => item.RecordAsync(
            It.Is<ProcurementControlEventWriteRequest>(request =>
                request.EventType == "SupplierEligibility" &&
                request.Action == "FrameworkCallOff" &&
                request.SourceReference == "BLANKET-001" &&
                request.DecisionKeys.Count == 14),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EffectiveDec011RequiresAnApprovedCurrentDueDiligenceReview()
    {
        await using var fixture = new Fixture();
        var supplier = fixture.AddSupplier();
        fixture.AddDec011Policy();
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.EvaluateEligibilityAsync(
            new SupplierEligibilityEvaluationRequest
            {
                BusinessPartnerId = supplier.Id,
                Boundary = SupplierEligibilityBoundary.Contract
            });

        result.IsValid.Should().BeFalse();
        result.Findings.Should().Contain(item =>
            item.Code == "SUPPLIER_DUE_DILIGENCE_REQUIRED" && item.Blocking);
        result.DueDiligencePolicyAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task ExactCurrentDueDiligenceLineageIsIncludedInEligibilityHash()
    {
        await using var fixture = new Fixture();
        var supplier = fixture.AddSupplier();
        var policy = fixture.AddDec011Policy();
        var review = fixture.AddDueDiligenceReview(supplier.Id, policy);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.EvaluateEligibilityAsync(
            new SupplierEligibilityEvaluationRequest
            {
                BusinessPartnerId = supplier.Id,
                Boundary = SupplierEligibilityBoundary.ManualPurchaseOrder,
                SkipFormalAvlMembership = true
            });

        result.IsValid.Should().BeTrue(string.Join("; ",
            result.Findings.Select(item => $"{item.Code}: {item.Message}")));
        result.DueDiligenceCurrent.Should().BeTrue();
        result.DueDiligenceReviewId.Should().Be(review.Id);
        result.DueDiligencePolicyDecisionId.Should().Be(policy.Id);
        result.DueDiligenceChecks.Should().HaveCount(6);
        result.DecisionHash.Should().MatchRegex("^[0-9A-F]{64}$");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
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
            Context.SaveChanges();

            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(item => item.TenantId).Returns(TenantId);
            current.SetupGet(item => item.UserId).Returns(UserId);
            current.SetupGet(item => item.IsAuthenticated).Returns(true);
            current.SetupGet(item => item.IsExternalUser).Returns(false);
            current.SetupGet(item => item.Username).Returns("eligibility@tdc.test");
            current.SetupGet(item => item.FullName).Returns("Eligibility Tester");
            current.SetupGet(item => item.Roles).Returns(["TenantAdmin"]);

            EvidencePacks = new Mock<IProcurementSupplierEvidencePackService>();
            ControlEvents = new Mock<IProcurementControlEventService>();
            ControlEvents.Setup(item => item.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementControlEventWriteRequest request, CancellationToken _) =>
                {
                    Context.Set<ProcurementControlEvent>().Add(new ProcurementControlEvent
                    {
                        TenantId = TenantId,
                        EventKey = request.EventKey,
                        EventType = request.EventType,
                        Action = request.Action,
                        Result = request.Result,
                        DecisionKeysJson = System.Text.Json.JsonSerializer.Serialize(request.DecisionKeys),
                        SourceType = request.SourceType,
                        SourceReference = request.SourceReference,
                        ActorUserId = UserId,
                        ActorName = "Eligibility Tester",
                        CorrelationId = request.CorrelationId,
                        OccurredAtUtc = request.OccurredAtUtc,
                        ResultValuesJson = System.Text.Json.JsonSerializer.Serialize(request.ResultValues),
                        IntegrityHash = new string('0', 64)
                    });
                    Context.SaveChanges();
                    var dto = new ProcurementControlEventDto
                    {
                        EventKey = request.EventKey,
                        EventType = request.EventType,
                        Action = request.Action,
                        SourceReference = request.SourceReference,
                        CorrelationId = request.CorrelationId,
                        ResultValuesJson = System.Text.Json.JsonSerializer.Serialize(request.ResultValues)
                    };
                    return dto;
                });

            _unitOfWork = new UnitOfWork(Context);
            Service = new SupplierValidationService(
                _unitOfWork,
                current.Object,
                EvidencePacks.Object,
                ControlEvents.Object,
                NullLogger<SupplierValidationService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public ApplicationDbContext Context { get; }
        public SupplierValidationService Service { get; }
        public Mock<IProcurementSupplierEvidencePackService> EvidencePacks { get; }
        public Mock<IProcurementControlEventService> ControlEvents { get; }

        public BusinessPartner AddSupplier(
            bool blacklisted = false,
            Guid? categoryId = null,
            Guid? tenantId = null)
        {
            var supplier = new BusinessPartner
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId ?? TenantId,
                PartnerCode = $"SUP-{Guid.NewGuid():N}"[..12],
                PartnerName = "Eligible supplier",
                PartnerType = "Supplier",
                RegistrationStatus = "Approved",
                ApprovalStatus = "Approved",
                IsActive = true,
                IsBlacklisted = blacklisted,
                BlacklistReason = blacklisted ? "Compliance hold" : null
            };
            if (categoryId.HasValue)
            {
                var category = new PartnerCategory
                {
                    Id = categoryId.Value,
                    TenantId = supplier.TenantId,
                    CategoryCode = "GOODS",
                    CategoryName = "Goods",
                    CategoryType = "Supplier",
                    IsActive = true
                };
                supplier.Categories.Add(new BusinessPartnerCategory
                {
                    Id = Guid.NewGuid(),
                    BusinessPartnerId = supplier.Id,
                    CategoryId = categoryId.Value,
                    Category = category
                });
                Context.Add(category);
            }
            Context.Add(supplier);
            return supplier;
        }

        public void AddQualifiedEntry(Guid supplierId, Guid categoryId)
        {
            var exercise = new ProcurementPrequalificationExercise
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Reference = "PQ-001",
                Title = "Goods qualification",
                Description = "Qualification",
                PolicySetId = Guid.NewGuid(),
                PolicySetCode = "TDC-POLICY",
                PolicySetVersion = 2,
                SourceConfigurationProfileId = Guid.NewGuid(),
                WorkflowDefinitionId = Guid.NewGuid(),
                IntegrityHash = new string('A', 64)
            };
            var application = new ProcurementPrequalificationApplication
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ExerciseId = exercise.Id,
                BusinessPartnerId = supplierId,
                ApplicationNumber = "PQA-001",
                SubmittedById = UserId,
                IntegrityHash = new string('B', 64)
            };
            Context.AddRange(exercise, application, new ProcurementQualifiedListEntry
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ExerciseId = exercise.Id,
                ApplicationId = application.Id,
                BusinessPartnerId = supplierId,
                CategoryId = categoryId,
                Status = ProcurementQualifiedListEntryStatus.Active,
                ValidFromUtc = DateTime.UtcNow.AddDays(-1),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
                ApprovalReference = "APPROVAL-001",
                ApprovalEvidenceReference = "MINUTE-001",
                IntegrityHash = new string('C', 64)
            });
        }

        public ProcurementConfigurationDecision AddDec011Policy()
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
            const string valueJson =
                """{"effectiveFrom":"2026-01-01T00:00:00Z","reviewFrequencyMonths":12,"exposureWindowMonths":12,"riskDimensions":["Compliance=40","DueDiligence=60"],"riskBands":["High=0-70","Low=70-100"],"concentrationLimitPercent":25,"minimumScore":70,"eligibilityAction":"escalationRequired","performanceWindowMonths":12,"performanceDimensions":["DeliveryTimeliness=15","GrnQuality=15","RejectionRate=15","PriceCompetitiveness=15","Responsiveness=10","ComplaintResolution=10","ContractCompletion=20"],"performanceBands":["Unsatisfactory=0-50","ImprovementRequired=50-75","Satisfactory=75-100"],"minimumPerformanceDataCoveragePercent":60,"responseTargetHours":48,"performanceEligibilityAction":"awardHardStop"}""";
            var decision = new ProcurementConfigurationDecision
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
                ValueJson = valueJson,
                EffectiveFrom = DateTime.UtcNow.AddDays(-30),
                ApprovedById = Guid.NewGuid(),
                ApprovedAt = DateTime.UtcNow.AddDays(-2)
            };
            profile.Decisions.Add(decision);
            Context.Add(profile);
            return decision;
        }

        public ProcurementSupplierDueDiligenceReview AddDueDiligenceReview(
            Guid supplierId,
            ProcurementConfigurationDecision policy)
        {
            var now = DateTime.UtcNow;
            var review = new ProcurementSupplierDueDiligenceReview
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BusinessPartnerId = supplierId,
                ReviewReference = "DD-2026-001",
                CycleNumber = 1,
                ReviewType = ProcurementSupplierDueDiligenceReviewType.Initial,
                Status = ProcurementSupplierDueDiligenceStatus.Approved,
                Outcome = ProcurementSupplierDueDiligenceOutcome.Clear,
                ReviewPeriodStartUtc = now.AddDays(-1),
                ReviewPeriodEndUtc = now.AddMonths(12),
                NextReviewDueAtUtc = now.AddMonths(12),
                PolicyDecisionId = policy.Id,
                PolicyProfileId = policy.ProfileId,
                PolicyProfileCode = "TDC-PROCUREMENT",
                PolicyProfileVersion = 1,
                ReviewFrequencyMonths = 12,
                PolicySnapshotJson = policy.ValueJson,
                PolicyValueHash = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(policy.ValueJson))),
                WorkflowDefinitionId = Guid.NewGuid(),
                WorkflowInstanceId = Guid.NewGuid(),
                ApprovedById = Guid.NewGuid(),
                ApprovedAtUtc = now,
                CreationCorrelationId = "eligibility-review",
                LastOperationCorrelationId = "eligibility-review-approved",
                LastOperation = "Approved",
                SnapshotJson = "{}",
                IntegrityHash = new string('D', 64),
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            foreach (var type in Enum.GetValues<ProcurementSupplierDueDiligenceCheckType>())
            {
                var check = new ProcurementSupplierDueDiligenceCheck
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    ReviewId = review.Id,
                    CheckType = type,
                    Status = ProcurementSupplierDueDiligenceCheckStatus.Clear,
                    SourceName = $"{type} source",
                    SourceReference = $"{type}-001",
                    CheckedAtUtc = now.AddDays(-1),
                    ValidUntilUtc = now.AddMonths(6),
                    ReviewedById = UserId,
                    ReviewerName = "Eligibility Tester",
                    SnapshotJson = "{}",
                    IntegrityHash = new string('E', 64)
                };
                check.EvidenceLinks.Add(
                    new ProcurementSupplierDueDiligenceEvidenceLink
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        ReviewId = review.Id,
                        CheckId = check.Id,
                        ReferenceKind =
                            ProcurementControlEvidenceReferenceKind.ExternalReference,
                        Reference = $"{type}-EVIDENCE-001",
                        RequirementKey = type.ToString(),
                        IntegrityHash = new string('F', 64)
                    });
                review.Checks.Add(check);
            }
            Context.Add(review);
            return review;
        }

        public ProcurementSupplierRiskAssessment AddRiskAssessment(
            Guid supplierId,
            ProcurementConfigurationDecision policy)
        {
            var now = DateTime.UtcNow;
            var policyHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(policy.ValueJson)));
            var assessment = new ProcurementSupplierRiskAssessment
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                AssessmentReference = $"SRISK-{Guid.NewGuid():N}"[..20],
                AssessmentSequence = 1,
                BusinessPartnerId = supplierId,
                AssessedAtUtc = now,
                PeriodStartUtc = now.AddMonths(-12),
                PeriodEndUtc = now,
                NextReviewDueAtUtc = now.AddMonths(12),
                PolicyDecisionId = policy.Id,
                PolicyProfileId = policy.ProfileId,
                PolicyProfileCode = "TDC-PROCUREMENT",
                PolicyProfileVersion = 1,
                PolicySnapshotJson = policy.ValueJson,
                PolicyValueHash = policyHash,
                ExposureWindowMonths = 12,
                MinimumScore = 70,
                ConcentrationLimitPercent = 25,
                RiskScore = 90,
                RiskBand = "Low",
                EligibilityAction =
                    ProcurementSupplierRiskEligibilityAction.EscalationRequired,
                DataComplete = true,
                DimensionScoresJson = "[]",
                SpendExposureJson = "[]",
                CategoryExposureJson = "[]",
                EligibilitySnapshotJson = "{}",
                EligibilityDecisionHash = new string('A', 64),
                FindingsJson = "[]",
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                CorrelationId = Guid.NewGuid().ToString("N"),
                AssessedByUserId = UserId,
                AssessedByName = "Eligibility Tester",
                SnapshotJson = "{}",
                IntegrityHash = new string('B', 64)
            };
            Context.Add(assessment);
            return assessment;
        }

        public ProcurementSupplierPerformanceScorecard AddPerformanceScorecard(
            Guid supplierId,
            ProcurementConfigurationDecision policy)
        {
            var now = DateTime.UtcNow;
            var policyHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(policy.ValueJson)));
            var scorecard = new ProcurementSupplierPerformanceScorecard
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ScorecardReference = $"SPERF-{Guid.NewGuid():N}"[..20],
                ScorecardSequence = 1,
                BusinessPartnerId = supplierId,
                CalculatedAtUtc = now,
                PeriodStartUtc = now.AddMonths(-12),
                PeriodEndUtc = now,
                NextReviewDueAtUtc = now.AddMonths(12),
                PolicyDecisionId = policy.Id,
                PolicyProfileId = policy.ProfileId,
                PolicyProfileCode = "TDC-PROCUREMENT",
                PolicyProfileVersion = 1,
                PolicySnapshotJson = policy.ValueJson,
                PolicyValueHash = policyHash,
                PerformanceWindowMonths = 12,
                MinimumScore = 70,
                MinimumDataCoveragePercent = 60,
                ResponseTargetHours = 48,
                EligibilityAction =
                    ProcurementSupplierRiskEligibilityAction.AwardHardStop,
                DataStatus = ProcurementSupplierPerformanceDataStatus.Complete,
                DataCoveragePercent = 100,
                OverallScore = 90,
                PerformanceBand = "Satisfactory",
                MinimumScoreBreached = false,
                DeliveryTimelinessScore = 90,
                GrnQualityScore = 90,
                RejectionRateScore = 90,
                PriceCompetitivenessScore = 90,
                ResponsivenessScore = 90,
                ComplaintResolutionScore = 90,
                ContractCompletionScore = 90,
                MeasureResultsJson = "[]",
                SourceSnapshotJson = "{}",
                SourceSnapshotHash = new string('A', 64),
                SupplierControlSnapshotJson = "{}",
                SupplierEligibilityDecisionHash = new string('B', 64),
                FindingsJson = "[]",
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                CorrelationId = Guid.NewGuid().ToString("N"),
                CalculatedByUserId = UserId,
                CalculatedByName = "Eligibility Tester",
                SnapshotJson = "{}",
                IntegrityHash = new string('C', 64)
            };
            Context.Add(scorecard);
            return scorecard;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }
}
