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
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementEvaluationCommitteeControlServiceTests
{
    [Fact]
    public async Task MissingTenderCaseLinkIsRecoveredFromItsCurrentImmutableRelease()
    {
        await using var fixture = new Fixture();
        fixture.RemoveTenderCaseLink();

        var readiness = await fixture.Service.GetReadinessAsync(
            ProcurementEvaluationSourceType.Tender, fixture.Tender.Id);

        readiness.SourceExists.Should().BeTrue();
        var retained = await fixture.Context.Tenders.AsNoTracking()
            .SingleAsync(item => item.Id == fixture.Tender.Id);
        retained.SourcingReleaseId.Should().Be(fixture.SourcingCase.SourcingReleaseId);
        retained.SourcingCaseId.Should().Be(fixture.SourcingCase.Id);
        fixture.SourcingCases.Verify(service => service.RecoverTenderSourceEntryAsync(
            fixture.Tender.SourcePurchaseRequisitionId!.Value,
            fixture.SourcingCase.SourcingReleaseId,
            fixture.Tender.Id,
            fixture.Tender.TenderNumber,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OptionsAndBindingReuseExactMasterCommitteeMembership()
    {
        await using var fixture = new Fixture();

        var options = await fixture.Service.GetOptionsAsync(
            ProcurementEvaluationSourceType.Tender, fixture.Tender.Id);
        var bound = await fixture.BindAndActivateAsync();

        options.Committees.Should().ContainSingle(item =>
            item.Id == fixture.Committee.Id &&
            item.CompositionReady &&
            item.ActiveMemberCount == 3);
        options.Users.Should().HaveCount(3);
        options.Users.Should().OnlyContain(item =>
            !string.IsNullOrWhiteSpace(item.DisplayName));
        bound.CommitteeTemplateId.Should().Be(fixture.Committee.Id);
        bound.Members.Select(item => item.UserId)
            .Should().BeEquivalentTo(fixture.MemberUserIds);
        bound.RequiredRoles.Should().Contain(item =>
            item.MemberKind == ProcurementCommitteeMemberKind.Chair);
        bound.RequiredRoles.Should().Contain(item =>
            item.MemberKind == ProcurementCommitteeMemberKind.Secretary);
        bound.PolicySetId.Should().Be(fixture.Policy.Id);
        bound.ConfigurationProfileId.Should().Be(fixture.Profile.Id);
        bound.ActivationEvidenceReference.Should().Be("evidence://constitution");
        bound.CompositionIntegrityHash.Should().HaveLength(64);
    }

    [Fact]
    public async Task CrossTenantActivationCannotDiscoverOrMutateCommitteeControl()
    {
        await using var fixture = new Fixture();
        var bound = await fixture.BindDraftAsync();
        fixture.SwitchTenant(Guid.NewGuid());

        await fixture.Service.Invoking(service => service.ActivateAsync(bound.Id,
                new ActivateProcurementEvaluationCommitteeRequest
                {
                    RowVersion = bound.RowVersion,
                    EvidenceReference = "evidence://cross-tenant-attempt",
                    IdempotencyKey = "activate-cross-tenant"
                }, "activate-cross-tenant"))
            .Should().ThrowAsync<ProcurementEvaluationCommitteeNotFoundException>()
            .Where(exception =>
                exception.Code == "EVALUATION_COMMITTEE_NOT_FOUND");

        var persisted = await fixture.Context
            .ProcurementEvaluationCommitteeControls
            .AsNoTracking()
            .SingleAsync(item => item.Id == bound.Id);
        persisted.TenantId.Should().Be(fixture.TenantId);
        persisted.Status.Should()
            .Be(ProcurementEvaluationCommitteeControlStatus.Draft);
        persisted.ActivatedAtUtc.Should().BeNull();
        persisted.ActivationEvidenceReference.Should().BeNull();
    }

    [Fact]
    public async Task StaleCommitteeRowVersionRejectsActivationWithoutPartialMutation()
    {
        await using var fixture = new Fixture();
        var bound = await fixture.BindDraftAsync();

        await fixture.Service.Invoking(service => service.ActivateAsync(bound.Id,
                new ActivateProcurementEvaluationCommitteeRequest
                {
                    RowVersion = Convert.ToBase64String(
                        Guid.NewGuid().ToByteArray()),
                    EvidenceReference = "evidence://stale-attempt",
                    IdempotencyKey = "activate-stale"
                }, "activate-stale"))
            .Should().ThrowAsync<ProcurementEvaluationCommitteeConflictException>()
            .Where(exception =>
                exception.Code == "EVALUATION_COMMITTEE_STALE");

        var persisted = await fixture.Context
            .ProcurementEvaluationCommitteeControls
            .AsNoTracking()
            .SingleAsync(item => item.Id == bound.Id);
        persisted.Status.Should()
            .Be(ProcurementEvaluationCommitteeControlStatus.Draft);
        persisted.ActivatedAtUtc.Should().BeNull();
        persisted.ActivatedByUserId.Should().BeNull();
        persisted.ActivationEvidenceReference.Should().BeNull();
        persisted.ActivationIdempotencyKey.Should().BeNull();
    }

    [Fact]
    public async Task AcceptanceAndConflictDeclarationAreSelfOnlyAndSigned()
    {
        await using var fixture = new Fixture();
        var bound = await fixture.BindAndActivateAsync();
        var chair = bound.Members.Single(item =>
            item.MemberKind == ProcurementCommitteeMemberKind.Chair);
        fixture.SwitchUser(fixture.MemberUserIds.Last());

        await fixture.Service.Invoking(service =>
                service.RespondToAppointmentAsync(chair.Id,
                    new RespondProcurementEvaluationAppointmentRequest
                    {
                        Accept = true,
                        SignatureReference = "sig://wrong-user",
                        EvidenceReference = "evidence://wrong-user",
                        RowVersion = chair.RowVersion,
                        IdempotencyKey = "accept-wrong-user"
                    }, "accept-wrong-user"))
            .Should().ThrowAsync<ProcurementEvaluationCommitteeAuthorizationException>();

        fixture.SwitchUser(chair.UserId);
        await fixture.Service.Invoking(service =>
                service.RespondToAppointmentAsync(chair.Id,
                    new RespondProcurementEvaluationAppointmentRequest
                    {
                        Accept = true,
                        RowVersion = chair.RowVersion,
                        IdempotencyKey = "accept-no-signature"
                    }, "accept-no-signature"))
            .Should().ThrowAsync<ProcurementEvaluationCommitteeValidationException>()
            .Where(exception =>
                exception.Code == "EVALUATION_APPOINTMENT_SIGNATURE_REQUIRED");

        var accepted = await fixture.AcceptAsync(chair);
        var declared = await fixture.DeclareNoConflictAsync(accepted);

        declared.Status.Should().Be(ProcurementEvaluationAppointmentStatus.Accepted);
        declared.CurrentDeclaration.Should().NotBeNull();
        declared.CurrentDeclaration!.Outcome.Should()
            .Be(ProcurementEvaluationConflictOutcome.NoConflict);
        declared.CurrentDeclaration.SignatureReference.Should().StartWith("sig://");
        declared.CurrentDeclaration.IntegrityHash.Should().HaveLength(64);
        declared.EligibleToScore.Should().BeTrue();
    }

    [Fact]
    public async Task QuorumCountsOnlyAcceptedNoConflictMembersWithSignedAttendance()
    {
        await using var fixture = new Fixture();
        var control = await fixture.PrepareEligibleCommitteeAsync();
        fixture.SwitchAdministrator();
        var meeting = await fixture.Service.CreateMeetingAsync(control.Id,
            new CreateProcurementEvaluationMeetingRequest
            {
                Phase = ProcurementEvaluationPhase.Technical,
                MeetingMode = "Remote",
                MeetingChannel = "Teams",
                ScheduledAtUtc = DateTime.UtcNow.AddMinutes(5),
                EvidenceReference = "evidence://agenda",
                RemoteMeetingEvidenceReference = "evidence://remote-session",
                CommitteeRowVersion = control.RowVersion,
                IdempotencyKey = "meeting-technical"
            }, "meeting-technical");

        foreach (var member in control.Members)
        {
            fixture.SwitchUser(member.UserId);
            var current = await fixture.Service.GetAsync(
                ProcurementEvaluationSourceType.Tender, fixture.Tender.Id);
            var currentMeeting = current.Meetings.Single(item => item.Id == meeting.Id);
            var currentMember = current.Members.Single(item => item.Id == member.Id);
            await fixture.Service.SignAttendanceAsync(meeting.Id,
                new SignProcurementEvaluationAttendanceRequest
                {
                    IsPresent = true,
                    SignatureReference = $"sig://attendance/{member.UserId:N}",
                    EvidenceReference = $"evidence://attendance/{member.UserId:N}",
                    MeetingRowVersion = currentMeeting.RowVersion,
                    AppointmentRowVersion = currentMember.RowVersion,
                    IdempotencyKey = $"attendance-{member.UserId:N}"
                }, $"attendance-{member.UserId:N}");
        }

        fixture.SwitchAdministrator();
        var currentControl = await fixture.Service.GetAsync(
            ProcurementEvaluationSourceType.Tender, fixture.Tender.Id);
        var confirmed = await fixture.Service.ConfirmQuorumAsync(meeting.Id,
            new ConfirmProcurementEvaluationQuorumRequest
            {
                RowVersion = currentControl.Meetings.Single(item => item.Id == meeting.Id)
                    .RowVersion,
                EvidenceReference = "evidence://quorum-register",
                RemoteMeetingEvidenceReference = "evidence://remote-session",
                IdempotencyKey = "quorum-technical"
            }, "quorum-technical");

        confirmed.QuorumMet.Should().BeTrue();
        confirmed.ChairPresent.Should().BeTrue();
        confirmed.SecretaryPresent.Should().BeTrue();
        confirmed.SignedVotingAttendanceCount.Should().Be(2);
        confirmed.QuorumIntegrityHash.Should().HaveLength(64);

        fixture.SwitchUser(control.Members.Single(item =>
            item.MemberKind == ProcurementCommitteeMemberKind.VotingMember).UserId);
        var eligibility = await fixture.Service.EnsureScorerEligibleAsync(
            ProcurementEvaluationSourceType.Tender, fixture.Tender.Id,
            ProcurementEvaluationPhase.Technical, "eligible");
        eligibility.Allowed.Should().BeTrue();
        eligibility.MeetingId.Should().Be(meeting.Id);
    }

    [Fact]
    public async Task LockedAttemptIsIdempotentAndTechnicalRecallFailsAfterFinancialProgression()
    {
        await using var fixture = new Fixture();
        var control = await fixture.PrepareEligibleCommitteeWithQuorumAsync();
        var evaluator = control.Members.Single(item =>
            item.MemberKind == ProcurementCommitteeMemberKind.VotingMember);
        fixture.SwitchUser(evaluator.UserId);
        var current = await fixture.Service.GetAsync(
            ProcurementEvaluationSourceType.Tender, fixture.Tender.Id);
        var meeting = current.Meetings.Single(item =>
            item.Phase == ProcurementEvaluationPhase.Technical);
        var member = current.Members.Single(item => item.Id == evaluator.Id);
        var request = new LockProcurementEvaluationScoreSheetRequest
        {
            SourceType = ProcurementEvaluationSourceType.Tender,
            SourceId = fixture.Tender.Id,
            Phase = ProcurementEvaluationPhase.Technical,
            ScoreSubjectType = "ProcurementTenderControl",
            ScoreSubjectId = fixture.Tender.Id,
            MeetingId = meeting.Id,
            AppointmentId = member.Id,
            CommitteeRowVersion = current.RowVersion,
            MeetingRowVersion = meeting.RowVersion,
            AppointmentRowVersion = member.RowVersion,
            ScoreSnapshotJson = """{"scores":[{"bid":"A","score":85}]}""",
            SignatureReference = "sig://technical-score",
            EvidenceReference = "evidence://technical-score",
            IdempotencyKey = "score-technical-1"
        };

        var locked = await fixture.Service.LockScoreSheetAsync(request, "score-1");
        var replay = await fixture.Service.LockScoreSheetAsync(request, "score-1-replay");

        replay.Id.Should().Be(locked.Id);
        replay.Attempt.Should().Be(1);
        replay.Status.Should().Be(ProcurementEvaluationScoreSheetStatus.Locked);
        locked.IntegrityHash.Should().HaveLength(64);
        fixture.TenderControl.Status =
            ProcurementTenderControlStatus.FinancialEvaluated;
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.Invoking(service => service.RequestScoreRecallAsync(
                locked.Id,
                new RequestProcurementEvaluationScoreRecallRequest
                {
                    ScoreSheetRowVersion = locked.RowVersion,
                    Reason = "A material scoring correction is required.",
                    EvidenceReference = "evidence://recall",
                    WorkflowDefinitionId = fixture.Workflow.Id,
                    IdempotencyKey = "recall-after-financial"
                }, "recall-after-financial"))
            .Should().ThrowAsync<ProcurementEvaluationCommitteeConflictException>()
            .Where(exception =>
                exception.Code == "EVALUATION_SCORE_RECALL_DOWNSTREAM_FINAL");
        (await fixture.Context.ProcurementEvaluationScoreRecalls.CountAsync())
            .Should().Be(0);
    }

    [Fact]
    public async Task MeetingModeIsCanonicalAndRemoteEvidenceCannotBeBypassed()
    {
        await using var fixture = new Fixture();
        var control = await fixture.PrepareEligibleCommitteeAsync();
        fixture.SwitchAdministrator();

        await fixture.Service.Invoking(service => service.CreateMeetingAsync(
                control.Id,
                new CreateProcurementEvaluationMeetingRequest
                {
                    Phase = ProcurementEvaluationPhase.Combined,
                    MeetingMode = "Zoom",
                    MeetingChannel = "Zoom",
                    ScheduledAtUtc = DateTime.UtcNow.AddMinutes(5),
                    EvidenceReference = "evidence://agenda",
                    CommitteeRowVersion = control.RowVersion,
                    IdempotencyKey = "invalid-mode"
                }, "invalid-mode"))
            .Should().ThrowAsync<ProcurementEvaluationCommitteeValidationException>()
            .Where(exception => exception.Code == "EVALUATION_MEETING_MODE_INVALID");

        await fixture.Service.Invoking(service => service.CreateMeetingAsync(
                control.Id,
                new CreateProcurementEvaluationMeetingRequest
                {
                    Phase = ProcurementEvaluationPhase.Combined,
                    MeetingMode = "remote",
                    MeetingChannel = "Zoom",
                    ScheduledAtUtc = DateTime.UtcNow.AddMinutes(5),
                    EvidenceReference = "evidence://agenda",
                    CommitteeRowVersion = control.RowVersion,
                    IdempotencyKey = "missing-remote-evidence"
                }, "missing-remote-evidence"))
            .Should().ThrowAsync<ProcurementEvaluationCommitteeValidationException>()
            .Where(exception =>
                exception.Code == "EVALUATION_REMOTE_MEETING_EVIDENCE_REQUIRED");
    }

    [Fact]
    public async Task ExactCompletedWorkflowWithIndependentProcessorApprovesRecall()
    {
        await using var fixture = new Fixture();
        var seeded = await fixture.SeedRecallDecisionAsync(
            WorkflowInstanceStatus.Completed, Guid.NewGuid());

        var decided = await fixture.Service.DecideScoreRecallAsync(seeded.Recall.Id,
            fixture.Decision(seeded.Recall, approve: true), "decide-recall");

        decided.Status.Should().Be(ProcurementEvaluationScoreRecallStatus.Approved);
        decided.AuthorizedNewAttempt.Should().Be(2);
        decided.DecisionEvidenceReference.Should().Be("evidence://recall-decision");
    }

    [Fact]
    public async Task CompletedWorkflowCannotBeRecordedAsRecallRejection()
    {
        await using var fixture = new Fixture();
        var seeded = await fixture.SeedRecallDecisionAsync(
            WorkflowInstanceStatus.Completed, Guid.NewGuid());

        await fixture.Service.Invoking(service => service.DecideScoreRecallAsync(
                seeded.Recall.Id, fixture.Decision(seeded.Recall, approve: false),
                "reject-completed"))
            .Should().ThrowAsync<ProcurementEvaluationCommitteeConflictException>()
            .Where(exception =>
                exception.Code == "EVALUATION_SCORE_RECALL_REJECTION_INCOMPLETE");
    }

    [Fact]
    public async Task RecallDecisionRejectsWorkflowSubjectMismatch()
    {
        await using var fixture = new Fixture();
        var seeded = await fixture.SeedRecallDecisionAsync(
            WorkflowInstanceStatus.Completed, Guid.NewGuid(),
            workflowSubjectMismatch: true);

        await fixture.Service.Invoking(service => service.DecideScoreRecallAsync(
                seeded.Recall.Id, fixture.Decision(seeded.Recall, approve: true),
                "subject-mismatch"))
            .Should().ThrowAsync<ProcurementEvaluationCommitteeConflictException>()
            .Where(exception =>
                exception.Code ==
                "EVALUATION_SCORE_RECALL_WORKFLOW_SUBJECT_MISMATCH");
    }

    [Fact]
    public async Task RecallDecisionRejectsScorerAsWorkflowProcessor()
    {
        await using var fixture = new Fixture();
        var locked = await fixture.CreateLockedTechnicalScoreAsync();
        var seeded = await fixture.SeedRecallDecisionAsync(
            WorkflowInstanceStatus.Completed, locked.SubmittedByUserId, locked);

        await fixture.Service.Invoking(service => service.DecideScoreRecallAsync(
                seeded.Recall.Id, fixture.Decision(seeded.Recall, approve: true),
                "workflow-sod"))
            .Should().ThrowAsync<ProcurementEvaluationCommitteeConflictException>()
            .Where(exception =>
                exception.Code ==
                "EVALUATION_SCORE_RECALL_WORKFLOW_SOD_CONFLICT");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly Mock<ICurrentUserProvider> _current;
        private Guid _currentTenantId;
        private Guid _currentUserId;
        private bool _administrator = true;

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            _currentTenantId = TenantId;
            AdministratorId = Guid.NewGuid();
            _currentUserId = AdministratorId;
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
            Profile = new ProcurementConfigurationProfile
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ProfileCode = "TDC-PROCUREMENT",
                Name = "TDC Procurement",
                Version = 1,
                LifecycleStatus = ProcurementConfigurationProfileStatus.Published,
                EffectiveFrom = DateTime.UtcNow.AddDays(-30)
            };
            Policy = new ProcurementPolicySet
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = "TDC-POLICY",
                Name = "TDC Policy",
                Version = 1,
                LifecycleStatus = ProcurementPolicyLifecycleStatus.Published,
                SourceConfigurationProfileId = Profile.Id,
                EffectiveFrom = DateTime.UtcNow.AddDays(-30),
                DefaultCurrencyCode = "GHS"
            };
            var rule = new ProcurementPolicyMethodRule
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PolicySetId = Policy.Id,
                RuleCode = "M-NCT",
                Name = "NCT",
                Category = ProcurementCategoryClass.Goods,
                Method = ProcurementMethodType.NationalCompetitiveTendering,
                IsAllowed = true,
                IsEnabled = true,
                EffectiveFrom = DateTime.UtcNow.AddDays(-30)
            };
            SourcingCase = new ProcurementSourcingCase
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PurchaseRequisitionId = Guid.NewGuid(),
                SourcingReleaseId = Guid.NewGuid(),
                CaseSequence = 1,
                CaseNumber = "CASE-EVAL-001",
                SourcePlanId = Guid.NewGuid(),
                SourcePlanItemId = Guid.NewGuid(),
                Category = ProcurementCategoryClass.Goods,
                RecommendedMethod = rule.Method,
                SelectedMethod = rule.Method,
                EstimatedValue = 500000m,
                CurrencyCode = "GHS",
                PolicySetId = Policy.Id,
                PolicyCode = Policy.Code,
                PolicyVersion = Policy.Version,
                MethodRuleId = rule.Id,
                MethodRuleCode = rule.RuleCode,
                ThresholdRuleId = Guid.NewGuid(),
                ThresholdRuleCode = "TH-NCT",
                AuthorityRouteId = Guid.NewGuid(),
                AuthorityRouteReference = "AUTH-NCT",
                Justification = "Competitive tender.",
                SourceControlFingerprint = new string('a', 64),
                CaseFingerprint = new string('b', 64),
                SnapshotJson = "{}",
                IntegrityHash = new string('c', 64)
            };
            Tender = new Tender
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderNumber = "TND-EVAL-001",
                Title = "Evaluation-controlled tender",
                TenderType = "NCT",
                Status = "Closed",
                SourcePurchaseRequisitionId = SourcingCase.PurchaseRequisitionId,
                SourcingReleaseId = SourcingCase.SourcingReleaseId,
                SourcingCaseId = SourcingCase.Id
            };
            TenderControl = new ProcurementTenderControl
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderId = Tender.Id,
                SourcingCaseId = SourcingCase.Id,
                MethodRuleId = rule.Id,
                AuthorityRouteId = SourcingCase.AuthorityRouteId!.Value,
                Method = rule.Method,
                MethodRuleCode = rule.RuleCode,
                AuthorityRouteReference = SourcingCase.AuthorityRouteReference!,
                Status = ProcurementTenderControlStatus.TechnicalEvaluated,
                AdvertisementReference = "ADV-001",
                PublicationChannel = "GHANEPS",
                TenderDocumentReference = "DOC-001",
                TenderDocumentVersion = "1",
                AdvertisementEvidenceReference = "evidence://advert",
                LifecycleSnapshotJson = "{}",
                IntegrityHash = new string('d', 64),
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            var role = new ApplicationRole
            {
                Id = Guid.NewGuid(),
                Name = "TDC_EVALUATOR",
                NormalizedName = "TDC_EVALUATOR"
            };
            Committee = new ProcurementCommittee
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = "TDC_EVALUATION",
                Name = "Evaluation Committee",
                CommitteeType = ProcurementCommitteeType.EvaluationCommittee,
                Status = ProcurementCommitteeStatus.Active,
                RequiredQuorum = 2,
                RequiredRoleName = "TDC_EVALUATOR",
                EffectiveFrom = DateTime.UtcNow.AddDays(-30),
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            for (var index = 0; index < 3; index++)
            {
                var user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    UserName = $"member{index}@tdc.test",
                    Email = $"member{index}@tdc.test",
                    FirstName = $"Member {index}",
                    LastName = "Evaluator",
                    IsActive = true
                };
                MemberUserIds.Add(user.Id);
                var assignment = new ProcurementResponsibilityAssignment
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    UserId = user.Id,
                    RoleId = role.Id,
                    RoleName = "TDC_EVALUATOR",
                    IsActive = true,
                    EffectiveFrom = DateTime.UtcNow.AddDays(-30),
                    Reason = "Evaluation appointment.",
                    RowVersion = Guid.NewGuid().ToByteArray()
                };
                Committee.Members.Add(new ProcurementCommitteeMember
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    AssignmentId = assignment.Id,
                    Assignment = assignment,
                    MemberKind = index switch
                    {
                        0 => ProcurementCommitteeMemberKind.Chair,
                        1 => ProcurementCommitteeMemberKind.Secretary,
                        _ => ProcurementCommitteeMemberKind.VotingMember
                    },
                    IsVoting = index != 1,
                    IsActive = true,
                    EffectiveFrom = DateTime.UtcNow.AddDays(-30),
                    Reason = "Source committee.",
                    RowVersion = Guid.NewGuid().ToByteArray()
                });
                Context.AddRange(user, assignment);
            }
            var workflowEntityType = new ErpSystem.Core.Entities.Workflow.WorkflowEntityType
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = "PROCUREMENT_SOURCING",
                Name = "Procurement Sourcing",
                IsActive = true
            };
            Workflow = new ErpSystem.Core.Entities.Workflow.WorkflowDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                DefinitionKey = Guid.NewGuid(),
                Name = "Evaluation Score Recall",
                EntityTypeId = workflowEntityType.Id,
                Version = 1,
                LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
                IsActive = true
            };
            Context.AddRange(Profile, Policy, rule, SourcingCase, Tender,
                TenderControl, role, Committee, workflowEntityType, Workflow);
            Context.SaveChanges();
            Context.ChangeTracker.Clear();

            _unitOfWork = new UnitOfWork(Context);
            _current = new Mock<ICurrentUserProvider>();
            _current.SetupGet(item => item.TenantId)
                .Returns(() => _currentTenantId);
            _current.SetupGet(item => item.UserId).Returns(() => _currentUserId);
            _current.SetupGet(item => item.IsAuthenticated).Returns(true);
            _current.SetupGet(item => item.IsExternalUser).Returns(false);
            _current.SetupGet(item => item.Username).Returns(() =>
                $"{_currentUserId:N}@tdc.test");
            _current.SetupGet(item => item.FullName).Returns("TDC Evaluator");
            _current.SetupGet(item => item.Roles).Returns(() =>
                _administrator ? ["Administrator"] : ["TDC_EVALUATOR"]);
            _current.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns((string role) =>
                    _administrator &&
                    string.Equals(role, Constants.Roles.SuperAdmin,
                        StringComparison.OrdinalIgnoreCase));
            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Message = "Allowed"
                });
            var sod = new Mock<IProcurementSodGuardService>();
            sod.Setup(item => item.EnforceAsync(
                    It.IsAny<ProcurementSodGuardRequest>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementSodGuardDecisionDto
                {
                    Allowed = true
                });
            var events = new Mock<IProcurementControlEventService>();
            events.Setup(item => item.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementControlEventDto());
            var workflowInstances = new Mock<IWorkflowInstanceService>();
            workflowInstances.Setup(item => item.StartWorkflowAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                    It.IsAny<Guid>(), It.IsAny<object?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ErpSystem.Core.Entities.Workflow.WorkflowInstance
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId
                });
            var notifications = new Mock<INotificationTopicPublisher>();
            SourcingCases.Setup(service => service.RecoverTenderSourceEntryAsync(
                    Tender.SourcePurchaseRequisitionId!.Value,
                    SourcingCase.SourcingReleaseId,
                    Tender.Id,
                    Tender.TenderNumber,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementSourcingCaseEntryGateDto
                {
                    SourcingReleaseId = SourcingCase.SourcingReleaseId,
                    SourcingCaseId = SourcingCase.Id,
                    SelectedMethod = SourcingCase.SelectedMethod,
                    EstimatedValue = SourcingCase.EstimatedValue,
                    CurrencyCode = SourcingCase.CurrencyCode
                });
            Service = new ProcurementEvaluationCommitteeControlService(
                _unitOfWork, _current.Object, access.Object, sod.Object,
                events.Object, SourcingCases.Object, workflowInstances.Object,
                notifications.Object);
        }

        public Guid TenantId { get; }
        public Guid AdministratorId { get; }
        public ApplicationDbContext Context { get; }
        public ProcurementConfigurationProfile Profile { get; }
        public ProcurementPolicySet Policy { get; }
        public ProcurementSourcingCase SourcingCase { get; }
        public Tender Tender { get; }
        public ProcurementTenderControl TenderControl { get; }
        public ProcurementCommittee Committee { get; }
        public ErpSystem.Core.Entities.Workflow.WorkflowDefinition Workflow { get; }
        public List<Guid> MemberUserIds { get; } = new();
        public Mock<IProcurementSourcingCaseService> SourcingCases { get; } = new();
        public ProcurementEvaluationCommitteeControlService Service { get; }

        public void RemoveTenderCaseLink()
        {
            var tender = Context.Tenders.Single(item => item.Id == Tender.Id);
            tender.SourcingCaseId = null;
            Context.SaveChanges();
            Context.ChangeTracker.Clear();
        }

        public void SwitchTenant(Guid tenantId) => _currentTenantId = tenantId;

        public void SwitchUser(Guid userId)
        {
            _currentUserId = userId;
            _administrator = false;
        }

        public void SwitchAdministrator()
        {
            _currentUserId = AdministratorId;
            _administrator = true;
        }

        public Task<ProcurementEvaluationCommitteeDto> BindDraftAsync() =>
            Service.BindAsync(
                new BindProcurementEvaluationCommitteeRequest
                {
                    SourceType = ProcurementEvaluationSourceType.Tender,
                    SourceId = Tender.Id,
                    CommitteeTemplateId = Committee.Id,
                    Purpose = "Evaluate all responsive bids.",
                    EffectiveFromUtc = DateTime.UtcNow.AddHours(-1),
                    RequiredRoles = [],
                    IdempotencyKey = "bind-evaluation"
                }, "bind-evaluation");

        public async Task<ProcurementEvaluationCommitteeDto> BindAndActivateAsync()
        {
            SwitchAdministrator();
            var bound = await BindDraftAsync();
            return await Service.ActivateAsync(bound.Id,
                new ActivateProcurementEvaluationCommitteeRequest
                {
                    RowVersion = bound.RowVersion,
                    EvidenceReference = "evidence://constitution",
                    IdempotencyKey = "activate-evaluation"
                }, "activate-evaluation");
        }

        public async Task<ProcurementEvaluationAppointmentDto> AcceptAsync(
            ProcurementEvaluationAppointmentDto member)
        {
            SwitchUser(member.UserId);
            return await Service.RespondToAppointmentAsync(member.Id,
                new RespondProcurementEvaluationAppointmentRequest
                {
                    Accept = true,
                    SignatureReference = $"sig://accept/{member.UserId:N}",
                    EvidenceReference = $"evidence://accept/{member.UserId:N}",
                    RowVersion = member.RowVersion,
                    IdempotencyKey = $"accept-{member.UserId:N}"
                }, $"accept-{member.UserId:N}");
        }

        public async Task<ProcurementEvaluationAppointmentDto> DeclareNoConflictAsync(
            ProcurementEvaluationAppointmentDto member)
        {
            SwitchUser(member.UserId);
            return await Service.SubmitConflictDeclarationAsync(member.Id,
                new SubmitProcurementEvaluationConflictDeclarationRequest
                {
                    Outcome = ProcurementEvaluationConflictOutcome.NoConflict,
                    Declaration = "I have no actual, potential, or perceived conflict.",
                    SignatureReference = $"sig://coi/{member.UserId:N}",
                    EvidenceReference = $"evidence://coi/{member.UserId:N}",
                    ValidFromUtc = DateTime.UtcNow.AddMinutes(-1),
                    ValidToUtc = DateTime.UtcNow.AddDays(30),
                    AppointmentRowVersion = member.RowVersion,
                    IdempotencyKey = $"coi-{member.UserId:N}"
                }, $"coi-{member.UserId:N}");
        }

        public async Task<ProcurementEvaluationCommitteeDto>
            PrepareEligibleCommitteeAsync()
        {
            var control = await BindAndActivateAsync();
            foreach (var member in control.Members)
            {
                var accepted = await AcceptAsync(member);
                await DeclareNoConflictAsync(accepted);
            }
            SwitchAdministrator();
            return await Service.GetAsync(
                ProcurementEvaluationSourceType.Tender, Tender.Id);
        }

        public async Task<ProcurementEvaluationCommitteeDto>
            PrepareEligibleCommitteeWithQuorumAsync()
        {
            var control = await PrepareEligibleCommitteeAsync();
            SwitchAdministrator();
            var meeting = await Service.CreateMeetingAsync(control.Id,
                new CreateProcurementEvaluationMeetingRequest
                {
                    Phase = ProcurementEvaluationPhase.Technical,
                    MeetingMode = "InPerson",
                    MeetingChannel = "Board room",
                    ScheduledAtUtc = DateTime.UtcNow.AddMinutes(5),
                    EvidenceReference = "evidence://agenda",
                    CommitteeRowVersion = control.RowVersion,
                    IdempotencyKey = "meeting-with-quorum"
                }, "meeting-with-quorum");
            foreach (var member in control.Members)
            {
                SwitchUser(member.UserId);
                var current = await Service.GetAsync(
                    ProcurementEvaluationSourceType.Tender, Tender.Id);
                await Service.SignAttendanceAsync(meeting.Id,
                    new SignProcurementEvaluationAttendanceRequest
                    {
                        SignatureReference = $"sig://attendance/{member.UserId:N}",
                        EvidenceReference =
                            $"evidence://attendance/{member.UserId:N}",
                        MeetingRowVersion = current.Meetings.Single(item =>
                            item.Id == meeting.Id).RowVersion,
                        AppointmentRowVersion = current.Members.Single(item =>
                            item.Id == member.Id).RowVersion,
                        IdempotencyKey = $"attend-{member.UserId:N}"
                    }, $"attend-{member.UserId:N}");
            }
            SwitchAdministrator();
            var beforeQuorum = await Service.GetAsync(
                ProcurementEvaluationSourceType.Tender, Tender.Id);
            await Service.ConfirmQuorumAsync(meeting.Id,
                new ConfirmProcurementEvaluationQuorumRequest
                {
                    RowVersion = beforeQuorum.Meetings.Single(item =>
                        item.Id == meeting.Id).RowVersion,
                    EvidenceReference = "evidence://quorum",
                    IdempotencyKey = "confirm-quorum"
                }, "confirm-quorum");
            return await Service.GetAsync(
                ProcurementEvaluationSourceType.Tender, Tender.Id);
        }

        public async Task<ProcurementEvaluationScoreSheetDto>
            CreateLockedTechnicalScoreAsync()
        {
            var control = await PrepareEligibleCommitteeWithQuorumAsync();
            var evaluator = control.Members.Single(item =>
                item.MemberKind == ProcurementCommitteeMemberKind.VotingMember);
            SwitchUser(evaluator.UserId);
            var current = await Service.GetAsync(
                ProcurementEvaluationSourceType.Tender, Tender.Id);
            var meeting = current.Meetings.Single(item =>
                item.Phase == ProcurementEvaluationPhase.Technical);
            var member = current.Members.Single(item => item.Id == evaluator.Id);
            return await Service.LockScoreSheetAsync(
                new LockProcurementEvaluationScoreSheetRequest
                {
                    SourceType = ProcurementEvaluationSourceType.Tender,
                    SourceId = Tender.Id,
                    Phase = ProcurementEvaluationPhase.Technical,
                    ScoreSubjectType = "ProcurementTenderControl",
                    ScoreSubjectId = Tender.Id,
                    MeetingId = meeting.Id,
                    AppointmentId = member.Id,
                    CommitteeRowVersion = current.RowVersion,
                    MeetingRowVersion = meeting.RowVersion,
                    AppointmentRowVersion = member.RowVersion,
                    ScoreSnapshotJson = """{"scores":[{"bid":"A","score":85}]}""",
                    SignatureReference = "sig://technical-score",
                    EvidenceReference = "evidence://technical-score",
                    IdempotencyKey = "score-technical-1"
                }, "score-technical-1");
        }

        public async Task<(ProcurementEvaluationScoreRecall Recall,
            ProcurementEvaluationScoreSheetDto Locked)> SeedRecallDecisionAsync(
            WorkflowInstanceStatus workflowStatus,
            Guid workflowProcessorId,
            ProcurementEvaluationScoreSheetDto? locked = null,
            bool workflowSubjectMismatch = false)
        {
            locked ??= await CreateLockedTechnicalScoreAsync();
            var recall = new ProcurementEvaluationScoreRecall
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ScoreSheetId = locked.Id,
                Status = ProcurementEvaluationScoreRecallStatus.PendingApproval,
                Reason = "Correct a material scoring error.",
                EvidenceReference = "evidence://recall-request",
                WorkflowDefinitionId = Workflow.Id,
                RequestedByUserId = locked.SubmittedByUserId,
                RequestedByName = "Evaluator",
                RequestedAtUtc = DateTime.UtcNow,
                IdempotencyKey = $"recall-{Guid.NewGuid():N}",
                SnapshotJson = "{}",
                IntegrityHash = new string('e', 64),
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            var instance = new ErpSystem.Core.Entities.Workflow.WorkflowInstance
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                WorkflowDefinitionId = Workflow.Id,
                EntityId = workflowSubjectMismatch ? Guid.NewGuid() : recall.Id,
                EntityTypeId = Workflow.EntityTypeId,
                Status = workflowStatus,
                InitiatedById = recall.RequestedByUserId
            };
            recall.WorkflowInstanceId = instance.Id;
            var step = new ErpSystem.Core.Entities.Workflow.WorkflowStep
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                WorkflowDefinitionId = Workflow.Id,
                Name = "Independent recall approval",
                StepType = WorkflowStepType.Approval,
                Order = 1,
                IsStartStep = true,
                IsEndStep = true
            };
            var stepInstance =
                new ErpSystem.Core.Entities.Workflow.WorkflowStepInstance
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    WorkflowInstanceId = instance.Id,
                    WorkflowStepId = step.Id,
                    Status = WorkflowStepInstanceStatus.Completed,
                    CompletedDate = DateTime.UtcNow
                };
            var approval = new ErpSystem.Core.Entities.Workflow.WorkflowApproval
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                StepInstanceId = stepInstance.Id,
                ApproverId = workflowProcessorId,
                ProcessedById = workflowProcessorId,
                Status = WorkflowApprovalStatus.Approved,
                ProcessedDate = DateTime.UtcNow
            };
            Context.AddRange(recall, instance, step, stepInstance, approval);
            await Context.SaveChangesAsync();
            SwitchAdministrator();
            return (recall, locked);
        }

        public DecideProcurementEvaluationScoreRecallRequest Decision(
            ProcurementEvaluationScoreRecall recall,
            bool approve) => new()
        {
            Approve = approve,
            RowVersion = Convert.ToBase64String(recall.RowVersion),
            DecisionReference = "DECISION-RECALL-001",
            EvidenceReference = "evidence://recall-decision",
            IdempotencyKey = $"decision-{recall.Id:N}-{approve}"
        };

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }
}
