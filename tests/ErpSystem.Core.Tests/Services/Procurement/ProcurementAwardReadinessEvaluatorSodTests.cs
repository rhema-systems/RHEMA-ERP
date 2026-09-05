using System.Text.Json;
using System.Text.Json.Nodes;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementAwardReadinessEvaluatorSodTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EvaluatorIsDeniedAndAuditedBeforeAnyReadinessDecision(
        bool ensureReady)
    {
        await using var fixture = new Fixture();
        var source = await fixture.AddLegacyTenderAsync(fixture.ActorId);

        Func<Task> action = ensureReady
            ? () => fixture.Service.EnsureAwardReadyAsync(
                source.Type, source.Id,
                new EvaluateProcurementAwardReadinessRequest
                {
                    IdempotencyKey = $"deny-ensure-{ensureReady}"
                },
                $"deny-ensure-{ensureReady}")
            : () => fixture.Service.EvaluateAsync(
                source.Type, source.Id,
                new EvaluateProcurementAwardReadinessRequest
                {
                    IdempotencyKey = $"deny-evaluate-{ensureReady}"
                },
                $"deny-evaluate-{ensureReady}");

        await action.Should()
            .ThrowAsync<ProcurementAwardReadinessAuthorizationException>();

        fixture.Context.ProcurementAwardReadinessDecisions.Should().BeEmpty();
        fixture.SodRequests.Should().Contain(request =>
            request.ControlCode == "SOD-EVALUATOR-AWARD-APPROVER" &&
            request.ProhibitedActorUserIds.Contains(fixture.ActorId) &&
            request.RequireSoleActorConflict);
        var audit = fixture.Events.Single(item =>
            item.EventType == "ProcurementAwardEvaluatorSodDecision");
        audit.Result.Should().Be(ProcurementControlEventResult.Denied);
        audit.SourceType.Should().Be(source.Type.ToString());
        audit.SourceId.Should().Be(source.Id);
        audit.SourceReference.Should().Be(source.Reference);
        var payload = JsonSerializer.Serialize(new
        {
            audit.InputValues,
            audit.ResultValues
        });
        payload.Should().Contain(fixture.ActorId.ToString());
        payload.Should().Contain("LegacyTenderEvaluation");
        payload.Should().Contain("SOD_CONFLICT");
    }

    [Fact]
    public async Task IndependentApproverIsAllowedAndLatestDecisionLineageIsAudited()
    {
        await using var fixture = new Fixture();
        var evaluatorId = Guid.NewGuid();
        var source = await fixture.AddLegacyTenderAsync(evaluatorId);
        var latest = fixture.AddReadinessDecision(source);
        await fixture.Context.SaveChangesAsync();

        var status = await fixture.Service
            .GetEvaluatorAwardApproverSodStatusAsync(
                source.Type, source.Id, "independent-approver");

        status.Allowed.Should().BeTrue();
        status.Code.Should().Be("SOD_ALLOWED");
        status.CurrentActorUserId.Should().Be(fixture.ActorId);
        status.EvaluatorUserIds.Should().Equal(evaluatorId);
        status.ReadinessDecisionId.Should().Be(latest.Id);
        status.ReadinessDecisionSequence.Should().Be(latest.DecisionSequence);
        status.ReadinessSourceIntegrityHash.Should()
            .Be(latest.SourceIntegrityHash);
        status.ReadinessIntegrityHash.Should().Be(latest.IntegrityHash);
        var audit = fixture.Events.Single(item =>
            item.EventType == "ProcurementAwardEvaluatorSodDecision");
        var payload = JsonSerializer.Serialize(new
        {
            audit.InputValues,
            audit.ResultValues
        });
        payload.Should().Contain(latest.Id.ToString());
        payload.Should().Contain(latest.IntegrityHash);
        payload.Should().Contain(evaluatorId.ToString());
        payload.Should().Contain(fixture.ActorId.ToString());
    }

    [Theory]
    [InlineData("RFQ", "RequestForQuotationEvaluation")]
    [InlineData("Formal", "FormalTenderTechnicalEvaluation")]
    [InlineData("Legacy", "LegacyTenderEvaluation")]
    [InlineData("Exceptional", "ExceptionalNegotiatedRecommendation")]
    public async Task StatusResolvesEveryEvaluationSourceFamily(
        string family,
        string expectedLineageFamily)
    {
        await using var fixture = new Fixture();
        var evaluatorId = Guid.NewGuid();
        var source = family switch
        {
            "RFQ" => await fixture.AddRfqAsync(evaluatorId),
            "Formal" => await fixture.AddFormalTenderAsync(evaluatorId),
            "Legacy" => await fixture.AddLegacyTenderAsync(evaluatorId),
            "Exceptional" =>
                await fixture.AddExceptionalTenderAsync(evaluatorId),
            _ => throw new InvalidOperationException()
        };

        var status = await fixture.Service
            .GetEvaluatorAwardApproverSodStatusAsync(
                source.Type, source.Id, $"family-{family}");

        status.Allowed.Should().BeTrue();
        status.SourceType.Should().Be(source.Type);
        status.SourceId.Should().Be(source.Id);
        status.SourceReference.Should().Be(source.Reference);
        status.EvaluatorUserIds.Should().ContainSingle()
            .Which.Should().Be(evaluatorId);
        status.EvaluatorLineage.Should().Contain(item =>
            item.Family == expectedLineageFamily &&
            item.EvaluatorUserId == evaluatorId);
        status.SodRuleId.Should().NotBeNull();
        status.SodRuleCode.Should()
            .Be("SOD-EVALUATOR-AWARD-APPROVER");
        if (family != "Legacy")
        {
            status.SourceMethodRuleId.Should().NotBeNull();
            status.SourceMethodRuleCode.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task DistinctNonEvaluatorAuthorityActorAvoidsSoleActorOverBlocking()
    {
        await using var fixture = new Fixture();
        var independentActorId = Guid.NewGuid();
        var source = await fixture.AddFormalTenderAsync(
            fixture.ActorId, independentActorId);

        var status = await fixture.Service
            .GetEvaluatorAwardApproverSodStatusAsync(
                source.Type, source.Id, "sole-actor-relaxed");

        status.Allowed.Should().BeTrue();
        status.EvaluatorUserIds.Should().Contain(fixture.ActorId);
        status.IndependentApprovalActorUserIds.Should()
            .Equal(independentActorId);
        fixture.SodRequests.Should().ContainSingle(request =>
            request.ControlCode == "SOD-EVALUATOR-AWARD-APPROVER" &&
            request.IndependentActorUserIds.Contains(independentActorId) &&
            request.RequireSoleActorConflict);
    }

    [Fact]
    public async Task RecalledAndRetainedCommitteeAttemptsBothRemainEvaluatorLineage()
    {
        await using var fixture = new Fixture();
        var evaluatorId = Guid.NewGuid();
        var source = await fixture.AddFormalTenderAsync(evaluatorId);
        await fixture.AddScoreAttemptsAsync(source.Id, evaluatorId);

        var status = await fixture.Service
            .GetEvaluatorAwardApproverSodStatusAsync(
                source.Type, source.Id, "attempt-history");

        var attempts = status.EvaluatorLineage
            .Where(item => item.ScoreSheetId.HasValue)
            .OrderBy(item => item.Attempt)
            .ToList();
        attempts.Should().HaveCount(2);
        attempts[0].Attempt.Should().Be(1);
        attempts[0].IsRecalledAttempt.Should().BeTrue();
        attempts[0].IsRetainedAttempt.Should().BeFalse();
        attempts[1].Attempt.Should().Be(2);
        attempts[1].IsRecalledAttempt.Should().BeFalse();
        attempts[1].IsRetainedAttempt.Should().BeTrue();
        status.EvaluatorUserIds.Should().Contain(evaluatorId);
        var audit = fixture.Events.Single(item =>
            item.EventType == "ProcurementAwardEvaluatorSodDecision");
        var payload = JsonSerializer.Serialize(new
        {
            audit.InputValues,
            audit.ResultValues,
            audit.CorrelationId
        });
        payload.Should().Contain(attempts[0].CommitteeControlId!.Value
            .ToString());
        payload.Should().Contain(attempts[0].AppointmentId!.Value
            .ToString());
        payload.Should().Contain(attempts[0].ScoreSheetId!.Value
            .ToString());
        payload.Should().Contain(attempts[1].ScoreSheetId!.Value
            .ToString());
        payload.Should().Contain("attempt-history");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StandardBidSubjectUsesSnapshotEvaluationAndRetainsEvaluatorAwardSeparation(
        bool evaluatorIsCurrentActor)
    {
        await using var fixture = new Fixture();
        var evaluatorId = evaluatorIsCurrentActor ? fixture.ActorId : Guid.NewGuid();
        var source = await fixture.AddLegacyTenderAsync(evaluatorId);
        var evaluation = await fixture.AddStandardScoreAttemptsAsync(source.Id, evaluatorId);

        var status = await fixture.Service.GetEvaluatorAwardApproverSodStatusAsync(
            source.Type, source.Id, "standard-bid-subject");

        status.Allowed.Should().Be(!evaluatorIsCurrentActor);
        status.EvaluatorUserIds.Should().Equal(evaluatorId);
        var attempts = status.EvaluatorLineage.Where(item => item.ScoreSheetId.HasValue).ToList();
        attempts.Should().HaveCount(2);
        attempts.Should().OnlyContain(item =>
            item.EvaluationId == evaluation.Id &&
            item.ScoreSubjectId == evaluation.TenderBidId &&
            item.EvaluatorUserId == evaluatorId &&
            item.Phase == ProcurementEvaluationPhase.Combined);
        attempts.Should().ContainSingle(item => item.IsRetainedAttempt);
        attempts.Should().ContainSingle(item => item.IsRecalledAttempt);
        fixture.SodRequests.Should().Contain(request =>
            request.ProhibitedActorUserIds.Contains(evaluatorId));
    }

    [Theory]
    [InlineData("subject")]
    [InlineData("evaluationId")]
    [InlineData("TenderBidId")]
    [InlineData("TenderEvaluatorId")]
    [InlineData("evaluatorUserId")]
    [InlineData("submitter")]
    [InlineData("appointment-tenant")]
    [InlineData("evaluator-tenant")]
    [InlineData("phase")]
    [InlineData("missing-snapshot")]
    [InlineData("malformed-snapshot")]
    [InlineData("array-snapshot")]
    [InlineData("non-string-id")]
    public async Task StandardBidScoreLineageRejectsForeignOrMalformedBindings(string mismatch)
    {
        await using var fixture = new Fixture();
        var evaluatorId = Guid.NewGuid();
        var source = await fixture.AddLegacyTenderAsync(evaluatorId);
        var evaluation = await fixture.AddStandardScoreAttemptsAsync(source.Id, evaluatorId);
        var sheet = await fixture.Context.ProcurementEvaluationScoreSheets
            .Include(item => item.Appointment)
            .SingleAsync(item => item.Status == ProcurementEvaluationScoreSheetStatus.Locked);

        switch (mismatch)
        {
            case "subject": sheet.ScoreSubjectId = evaluation.Id; break;
            case "submitter": sheet.SubmittedByUserId = Guid.NewGuid(); break;
            case "appointment-tenant": sheet.Appointment.TenantId = Guid.NewGuid(); break;
            case "evaluator-tenant": evaluation.TenderEvaluator.TenantId = Guid.NewGuid(); break;
            case "phase": sheet.Phase = ProcurementEvaluationPhase.Technical; break;
            case "missing-snapshot": sheet.ScoreSnapshotJson = "{}"; break;
            case "malformed-snapshot": sheet.ScoreSnapshotJson = "not-json"; break;
            case "array-snapshot": sheet.ScoreSnapshotJson = "[]"; break;
            default:
                var snapshot = JsonNode.Parse(sheet.ScoreSnapshotJson)!;
                if (mismatch == "non-string-id") snapshot["evaluationId"] = 17;
                else snapshot[mismatch] = Guid.NewGuid().ToString();
                sheet.ScoreSnapshotJson = snapshot.ToJsonString();
                break;
        }
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.Invoking(service => service.GetEvaluatorAwardApproverSodStatusAsync(
                source.Type, source.Id, $"standard-mismatch-{mismatch}"))
            .Should().ThrowAsync<ProcurementAwardReadinessValidationException>()
            .Where(exception => exception.Code == "AWARD_READINESS_EVALUATOR_LINEAGE_AMBIGUOUS");
        fixture.Context.ProcurementAwardReadinessDecisions.Should().BeEmpty();
        fixture.SodRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task ExceptionalSourceIncludesCommitteeScoreEvaluatorWhenPresent()
    {
        await using var fixture = new Fixture();
        var recommendationEvaluatorId = Guid.NewGuid();
        var scoreEvaluatorId = Guid.NewGuid();
        var source = await fixture.AddExceptionalTenderAsync(
            recommendationEvaluatorId);
        await fixture.AddScoreAttemptsAsync(
            source.Id, scoreEvaluatorId);

        var status = await fixture.Service
            .GetEvaluatorAwardApproverSodStatusAsync(
                source.Type, source.Id, "exceptional-score");

        status.EvaluatorUserIds.Should().BeEquivalentTo(
            [recommendationEvaluatorId, scoreEvaluatorId]);
        status.EvaluatorLineage.Should().Contain(item =>
            item.Family == "ExceptionalCommitteeScore" &&
            item.EvaluatorUserId == scoreEvaluatorId &&
            item.ScoreSheetId.HasValue);
    }

    [Fact]
    public async Task MissingAndAmbiguousEvaluatorLineageFailClosed()
    {
        await using var missingFixture = new Fixture();
        var missing = await missingFixture.AddFormalTenderAsync(
            evaluatorId: null);

        await missingFixture.Service.Invoking(service =>
                service.GetEvaluatorAwardApproverSodStatusAsync(
                    missing.Type, missing.Id, "missing-lineage"))
            .Should()
            .ThrowAsync<ProcurementAwardReadinessValidationException>()
            .Where(exception =>
                exception.Code ==
                "AWARD_READINESS_EVALUATOR_LINEAGE_MISSING");

        await using var ambiguousFixture = new Fixture();
        var ambiguous = await ambiguousFixture.AddFormalTenderAsync(
            Guid.NewGuid());
        await ambiguousFixture.AddDuplicateFormalControlAsync(
            ambiguous.Id);

        await ambiguousFixture.Service.Invoking(service =>
                service.GetEvaluatorAwardApproverSodStatusAsync(
                    ambiguous.Type, ambiguous.Id, "ambiguous-lineage"))
            .Should()
            .ThrowAsync<ProcurementAwardReadinessValidationException>()
            .Where(exception =>
                exception.Code ==
                "AWARD_READINESS_EVALUATOR_LINEAGE_AMBIGUOUS");

        await using var foreignFixture = new Fixture();
        var foreignEvaluatorId = Guid.NewGuid();
        var foreign = await foreignFixture.AddFormalTenderAsync(
            foreignEvaluatorId);
        await foreignFixture.AddScoreAttemptsAsync(
            foreign.Id, foreignEvaluatorId);
        var appointment = await foreignFixture.Context
            .ProcurementEvaluationCommitteeAppointments
            .SingleAsync();
        appointment.TenantId = Guid.NewGuid();
        await foreignFixture.Context.SaveChangesAsync();

        await foreignFixture.Service.Invoking(service =>
                service.GetEvaluatorAwardApproverSodStatusAsync(
                    foreign.Type, foreign.Id, "foreign-lineage"))
            .Should()
            .ThrowAsync<ProcurementAwardReadinessValidationException>()
            .Where(exception =>
                exception.Code ==
                "AWARD_READINESS_EVALUATOR_LINEAGE_AMBIGUOUS");
    }

    [Fact]
    public async Task CrossTenantStatusCannotDiscoverEvaluatorLineage()
    {
        await using var fixture = new Fixture();
        var source = await fixture.AddLegacyTenderAsync(Guid.NewGuid());
        fixture.SwitchTenant(Guid.NewGuid());

        await fixture.Service.Invoking(service =>
                service.GetEvaluatorAwardApproverSodStatusAsync(
                    source.Type, source.Id, "foreign-tenant"))
            .Should()
            .ThrowAsync<ProcurementAwardReadinessNotFoundException>()
            .Where(exception => exception.Code == "TENDER_NOT_FOUND");
        fixture.SodRequests.Should().BeEmpty();
        fixture.Events.Should().BeEmpty();
    }

    private sealed record Source(
        ProcurementAwardReadinessSourceType Type,
        Guid Id,
        string Reference);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly Mock<ICurrentUserProvider> _current = new();
        private Guid _tenantId;
        private Guid _actorId;

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            _tenantId = TenantId;
            ActorId = Guid.NewGuid();
            _actorId = ActorId;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
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

            _current.SetupGet(item => item.TenantId)
                .Returns(() => _tenantId);
            _current.SetupGet(item => item.UserId)
                .Returns(() => _actorId);
            _current.SetupGet(item => item.IsAuthenticated).Returns(true);
            _current.SetupGet(item => item.IsExternalUser).Returns(false);
            _current.SetupGet(item => item.Username)
                .Returns("award.approver@tdc.test");
            _current.SetupGet(item => item.FullName)
                .Returns("TDC Award Approver");
            _current.SetupGet(item => item.Roles)
                .Returns(["TDC_HEAD_OF_PROCUREMENT"]);
            _current.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns(false);
            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Message = "Allowed"
                });
            var sod = new Mock<IProcurementSodGuardService>();
            sod.Setup(item => item.EnforceAsync(
                    It.IsAny<ProcurementSodGuardRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((
                    ProcurementSodGuardRequest request,
                    string correlationId,
                    CancellationToken _) =>
                {
                    SodRequests.Add(request);
                    var independent = request.IndependentActorUserIds.Any(id =>
                        id != Guid.Empty &&
                        id != _actorId &&
                        !request.ProhibitedActorUserIds.Contains(id));
                    var blocked =
                        request.ProhibitedActorUserIds.Contains(_actorId) &&
                        (!request.RequireSoleActorConflict || !independent);
                    return new ProcurementSodGuardDecisionDto
                    {
                        DecisionId = Guid.NewGuid(),
                        CorrelationId = correlationId,
                        EvaluatedAtUtc = DateTime.UtcNow,
                        Allowed = !blocked,
                        IsHardStop = true,
                        WasAudited = blocked,
                        Code = blocked ? "SOD_CONFLICT" : "SOD_ALLOWED",
                        Message = blocked
                            ? "The evaluator cannot be the sole award approver."
                            : "The award approval has the required separation.",
                        ActorUserId = _actorId,
                        ActorRoles = ["TDC_HEAD_OF_PROCUREMENT"],
                        ControlCode = request.ControlCode,
                        SourceType = request.SourceType,
                        SourceReference = request.SourceReference,
                        PolicySetId = Guid.Parse(
                            "10000000-0000-0000-0000-000000000001"),
                        PolicyCode = "TDC-SOD",
                        PolicyVersion = 1,
                        RuleId = Guid.Parse(
                            "10000000-0000-0000-0000-000000000002"),
                        RuleCode = request.ControlCode,
                        SourceDecisionKey = "DEC-004"
                    };
                });
            var events = new Mock<IProcurementControlEventService>();
            events.Setup(item => item.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback((
                    ProcurementControlEventWriteRequest request,
                    CancellationToken _) => Events.Add(request))
                .ReturnsAsync(new ProcurementControlEventDto());
            _unitOfWork = new UnitOfWork(Context);
            Service = new ProcurementAwardReadinessService(
                _unitOfWork,
                _current.Object,
                access.Object,
                sod.Object,
                events.Object);
        }

        public Guid TenantId { get; }
        public Guid ActorId { get; }
        public ApplicationDbContext Context { get; }
        public ProcurementAwardReadinessService Service { get; }
        public List<ProcurementSodGuardRequest> SodRequests { get; } = [];
        public List<ProcurementControlEventWriteRequest> Events { get; } = [];

        public void SwitchTenant(Guid tenantId) => _tenantId = tenantId;

        public async Task<Source> AddRfqAsync(Guid evaluatorId)
        {
            var rfq = new RequestForQuotation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                RfqNumber = $"RFQ-{Guid.NewGuid():N}",
                Title = "RFQ evaluator SOD",
                Status = "Closed",
                CreatedById = Guid.NewGuid()
            };
            var evaluation = new ProcurementRfqEvaluation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                RfqId = rfq.Id,
                OpeningRegisterId = Guid.NewGuid(),
                Status = ProcurementRfqEvaluationStatus.Approved,
                AwardMode = "WinnerTakesAll",
                RecommendationReason = "Best evaluated offer.",
                EvidenceReference = "evidence://rfq-evaluation",
                MethodRuleId = Guid.NewGuid(),
                MethodRuleCode = "METHOD-RFQ",
                SubmittedAtUtc = DateTime.UtcNow.AddHours(-2),
                SubmittedByUserId = evaluatorId,
                ApprovedAtUtc = DateTime.UtcNow.AddHours(-1),
                IntegrityHash = new string('a', 64),
                SnapshotJson = "{}"
            };
            Context.AddRange(rfq, evaluation);
            await Context.SaveChangesAsync();
            return new Source(
                ProcurementAwardReadinessSourceType.RequestForQuotation,
                rfq.Id,
                rfq.RfqNumber);
        }

        public async Task<Source> AddFormalTenderAsync(
            Guid? evaluatorId,
            Guid? independentApprovalActorId = null)
        {
            var tender = Tender("FORMAL");
            var evaluatorJson = evaluatorId.HasValue
                ? JsonSerializer.Serialize(new
                {
                    evaluatorUserId = evaluatorId.Value
                })
                : "{}";
            var control = FormalControl(
                tender.Id,
                evaluatorJson,
                independentApprovalActorId);
            Context.AddRange(tender, control);
            await Context.SaveChangesAsync();
            return new Source(
                ProcurementAwardReadinessSourceType.Tender,
                tender.Id,
                tender.TenderNumber);
        }

        public async Task<Source> AddLegacyTenderAsync(Guid evaluatorId)
        {
            var partner = Partner();
            var tender = Tender("LEGACY");
            var bid = new TenderBid
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderId = tender.Id,
                BusinessPartnerId = partner.Id,
                BidNumber = $"BID-{Guid.NewGuid():N}",
                Status = "Accepted",
                TotalBidAmount = 100m
            };
            var evaluator = new TenderEvaluator
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderId = tender.Id,
                UserId = evaluatorId,
                Role = "Evaluator",
                Status = "Completed",
                CompletedDate = DateTime.UtcNow.AddHours(-1)
            };
            var evaluation = new TenderEvaluation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderBidId = bid.Id,
                TenderEvaluatorId = evaluator.Id,
                TenderBid = bid,
                TenderEvaluator = evaluator,
                EvaluationDate = DateTime.UtcNow.AddHours(-2),
                SubmittedDate = DateTime.UtcNow.AddHours(-1),
                Status = "Approved",
                TotalScore = 90m,
                IsRecommended = true,
                Recommendation = "Best evaluated bid."
            };
            Context.AddRange(partner, tender, bid, evaluator, evaluation);
            await Context.SaveChangesAsync();
            return new Source(
                ProcurementAwardReadinessSourceType.Tender,
                tender.Id,
                tender.TenderNumber);
        }

        public async Task<Source> AddExceptionalTenderAsync(Guid evaluatorId)
        {
            var tender = Tender("EXCEPTIONAL");
            var control = new ProcurementExceptionalSourcingControl
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderId = tender.Id,
                SourcingCaseId = Guid.NewGuid(),
                MethodRuleId = Guid.NewGuid(),
                ExceptionRuleId = Guid.NewGuid(),
                AuthorityRouteId = Guid.NewGuid(),
                Method = ProcurementMethodType.SingleSource,
                MethodRuleCode = "METHOD-SINGLE-SOURCE",
                ExceptionRuleCode = "EXCEPTION-001",
                AuthorityRouteReference = "AUTH-EXCEPTIONAL",
                Status =
                    ProcurementExceptionalSourcingControlStatus.Recommended,
                Justification = "Only source.",
                JustificationEvidenceReference =
                    "evidence://justification",
                SupplierSelectionEvidenceReference =
                    "evidence://selection",
                PreparedAtUtc = DateTime.UtcNow.AddDays(-2),
                PreparedById = Guid.NewGuid(),
                WorkflowDefinitionId = Guid.NewGuid(),
                RecommendedById = evaluatorId,
                RecommendedAtUtc = DateTime.UtcNow.AddHours(-1),
                RecommendationReason = "Negotiated recommendation.",
                RecommendationEvidenceReference =
                    "evidence://recommendation",
                ApprovalActorsJson = "[]",
                SupplierSnapshotJson = "[]",
                EvidenceChecklistJson = "[]",
                LifecycleSnapshotJson = "{}",
                IntegrityHash = new string('b', 64)
            };
            Context.AddRange(tender, control);
            await Context.SaveChangesAsync();
            return new Source(
                ProcurementAwardReadinessSourceType.ExceptionalSourcing,
                tender.Id,
                tender.TenderNumber);
        }

        public async Task AddScoreAttemptsAsync(
            Guid tenderId,
            Guid evaluatorId)
        {
            var committee = new ProcurementEvaluationCommitteeControl
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                SourceType = ProcurementEvaluationSourceType.Tender,
                SourceId = tenderId,
                Version = 1,
                SourceReference = "TENDER-SCORE",
                Purpose = "Evaluate bids.",
                Status = ProcurementEvaluationCommitteeControlStatus.Active,
                CommitteeTemplateId = Guid.NewGuid(),
                CommitteeCode = "TDC_EVALUATION",
                CommitteeName = "Evaluation Committee",
                RequiredQuorum = 1,
                PolicySetId = Guid.NewGuid(),
                PolicyCode = "TDC-POLICY",
                PolicyVersion = 1,
                MethodRuleId = Guid.NewGuid(),
                MethodRuleCode = "METHOD-NCT",
                EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
                ActivatedAtUtc = DateTime.UtcNow.AddDays(-1),
                ActivatedByUserId = Guid.NewGuid(),
                ActivationEvidenceReference = "evidence://activation",
                CompositionSnapshotJson = "{}",
                CompositionIntegrityHash = new string('c', 64),
                CreationIdempotencyKey = $"committee-{Guid.NewGuid():N}",
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            var appointment =
                new ProcurementEvaluationCommitteeAppointment
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    CommitteeControlId = committee.Id,
                    CommitteeControl = committee,
                    CommitteeMemberId = Guid.NewGuid(),
                    ResponsibilityAssignmentId = Guid.NewGuid(),
                    UserId = evaluatorId,
                    MemberKind =
                        ProcurementCommitteeMemberKind.VotingMember,
                    RoleName = "Evaluator",
                    IsVoting = true,
                    Status =
                        ProcurementEvaluationAppointmentStatus.Accepted,
                    EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
                    AcceptedAtUtc = DateTime.UtcNow.AddDays(-1),
                    AcceptanceSignatureReference = "sig://accept",
                    AcceptanceEvidenceReference = "evidence://accept",
                    RowVersion = Guid.NewGuid().ToByteArray()
                };
            var first = ScoreSheet(
                committee, appointment, tenderId, evaluatorId, 1,
                ProcurementEvaluationScoreSheetStatus.Recalled);
            var second = ScoreSheet(
                committee, appointment, tenderId, evaluatorId, 2,
                ProcurementEvaluationScoreSheetStatus.Locked);
            Context.AddRange(committee, appointment, first, second);
            await Context.SaveChangesAsync();
        }

        public async Task<TenderEvaluation> AddStandardScoreAttemptsAsync(
            Guid tenderId, Guid evaluatorId)
        {
            await AddScoreAttemptsAsync(tenderId, evaluatorId);
            var evaluation = await Context.TenderEvaluations
                .Include(item => item.TenderEvaluator)
                .SingleAsync(item => item.TenderBid.TenderId == tenderId);
            evaluation.Status = "Submitted";
            foreach (var sheet in await Context.ProcurementEvaluationScoreSheets.ToListAsync())
            {
                sheet.Phase = ProcurementEvaluationPhase.Combined;
                sheet.ScoreSubjectType = "TenderEvaluation";
                sheet.ScoreSubjectId = evaluation.TenderBidId;
                sheet.ScoreSnapshotJson = TenderEvaluationService.BuildLegacyScoreSnapshot(
                    evaluation, evaluation.SubmittedDate!.Value);
            }
            await Context.SaveChangesAsync();
            return evaluation;
        }

        public async Task AddDuplicateFormalControlAsync(Guid tenderId)
        {
            Context.ProcurementTenderControls.Add(FormalControl(
                tenderId,
                JsonSerializer.Serialize(new
                {
                    evaluatorUserId = Guid.NewGuid()
                }),
                null));
            await Context.SaveChangesAsync();
        }

        public ProcurementAwardReadinessDecision AddReadinessDecision(
            Source source)
        {
            var decision = new ProcurementAwardReadinessDecision
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                SourceType = source.Type,
                SourceId = source.Id,
                SourceReference = source.Reference,
                Method =
                    ProcurementMethodType.NationalCompetitiveTendering,
                DecisionSequence = 4,
                Status =
                    ProcurementAwardReadinessDecisionStatus.Blocked,
                RecommendationSubjectType = "TenderBid",
                RecommendedSubjectIdsJson = "[]",
                RecommendedBusinessPartnerIdsJson = "[]",
                RecommendationSnapshotJson = "{}",
                EvaluationLineageJson = "[]",
                SupplierLineageJson = "[]",
                PrequalificationLineageJson = "[]",
                VerificationLineageJson = "[]",
                AuthorityLineageJson = "{}",
                EvidenceLineageJson = "[]",
                PrerequisiteSnapshotJson = "[]",
                TimelineSnapshotJson = "[]",
                BlockedReasonsJson = "[]",
                SourceIntegrityHash = new string('d', 64),
                IntegrityHash = new string('e', 64),
                IdempotencyKey = "prior-readiness",
                CorrelationId = "prior-readiness",
                EvaluatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                EvaluatedByUserId = Guid.NewGuid(),
                EvaluatedByName = "Prior approver"
            };
            Context.ProcurementAwardReadinessDecisions.Add(decision);
            return decision;
        }

        private Tender Tender(string family) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            TenderNumber = $"TND-{family}-{Guid.NewGuid():N}",
            Title = $"{family} evaluator SOD",
            TenderType = family == "EXCEPTIONAL" ? "SingleSource" : "NCT",
            Status = "Closed",
            CreatedById = Guid.NewGuid()
        };

        private BusinessPartner Partner() => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            PartnerCode = $"SUP-{Guid.NewGuid():N}",
            PartnerName = "Evaluator SOD Supplier",
            PartnerType = "Supplier",
            IsActive = true,
            ApprovalStatus = "Approved",
            RegistrationStatus = "Approved",
            ApprovedById = Guid.NewGuid()
        };

        private ProcurementTenderControl FormalControl(
            Guid tenderId,
            string evaluatorJson,
            Guid? independentApprovalActorId) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            TenderId = tenderId,
            SourcingCaseId = Guid.NewGuid(),
            MethodRuleId = Guid.NewGuid(),
            AuthorityRouteId = Guid.NewGuid(),
            Method =
                ProcurementMethodType.NationalCompetitiveTendering,
            MethodRuleCode = "METHOD-NCT",
            AuthorityRouteReference = "AUTH-NCT",
            Status = ProcurementTenderControlStatus.Approved,
            AdvertisementReference = "ADV-001",
            PublicationChannel = "GHANEPS",
            TenderDocumentReference = "DOC-001",
            TenderDocumentVersion = "1",
            AdvertisementEvidenceReference = "evidence://advert",
            TechnicalEvaluationSnapshotJson = evaluatorJson,
            TechnicalEvaluationIntegrityHash = new string('f', 64),
            TechnicalEvaluatedAtUtc = DateTime.UtcNow.AddHours(-3),
            FinancialEvaluationSnapshotJson = evaluatorJson,
            FinancialEvaluationIntegrityHash = new string('a', 64),
            FinancialEvaluatedAtUtc = DateTime.UtcNow.AddHours(-2),
            ApprovedAtUtc = DateTime.UtcNow.AddHours(-1),
            ApprovedById = independentApprovalActorId,
            ApprovalActorsJson = independentApprovalActorId.HasValue
                ? JsonSerializer.Serialize(
                    new[] { independentApprovalActorId.Value })
                : "[]",
            LifecycleSnapshotJson = "{}",
            IntegrityHash = new string('b', 64),
            RowVersion = Guid.NewGuid().ToByteArray()
        };

        private ProcurementEvaluationScoreSheet ScoreSheet(
            ProcurementEvaluationCommitteeControl committee,
            ProcurementEvaluationCommitteeAppointment appointment,
            Guid tenderId,
            Guid evaluatorId,
            int attempt,
            ProcurementEvaluationScoreSheetStatus status) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            CommitteeControlId = committee.Id,
            CommitteeControl = committee,
            MeetingId = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            Appointment = appointment,
            Phase = ProcurementEvaluationPhase.Technical,
            ScoreSubjectType = "ProcurementTenderControl",
            ScoreSubjectId = tenderId,
            Attempt = attempt,
            Status = status,
            SubmittedAtUtc = DateTime.UtcNow.AddMinutes(-20 + attempt),
            SubmittedByUserId = evaluatorId,
            SubmittedByName = "Evaluator",
            ScoreSnapshotJson = "{}",
            SignatureReference = $"sig://score/{attempt}",
            EvidenceReference = $"evidence://score/{attempt}",
            IntegrityHash = new string(
                attempt == 1 ? '1' : '2', 64),
            IdempotencyKey = $"score-{attempt}",
            RowVersion = Guid.NewGuid().ToByteArray()
        };

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }
}
