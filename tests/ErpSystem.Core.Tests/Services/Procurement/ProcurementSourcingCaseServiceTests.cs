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

public sealed class ProcurementSourcingCaseServiceTests
{
    [Fact]
    public async Task ApprovedReadyRequisitionIsSelectableBeforeItsAutomaticReleaseExists()
    {
        await using var fixture = new Fixture();
        fixture.MarkReadyWithoutRelease();

        var options = await fixture.Service.GetSourceOptionsAsync();

        var option = options.Should().ContainSingle().Which;
        option.RequisitionId.Should().Be(fixture.Requisition.Id);
        option.SourcingReleaseId.Should().BeNull();
        option.ReleaseReference.Should().BeNull();
        option.CurrentCaseId.Should().BeNull();
    }

    [Fact]
    public async Task AutomaticRecommendationCreatesImmutablePolicyReleaseLotsAndRequestLineage()
    {
        await using var fixture = new Fixture();

        var created = await fixture.Service.CreateAsync(fixture.ValidRequest(), "trace-case-create");

        created.CaseNumber.Should().Be("SC-PR-CASE-0001-A1");
        created.SelectedMethod.Should().Be(ProcurementMethodType.RequestForQuotation);
        created.RecommendedMethod.Should().Be(ProcurementMethodType.RequestForQuotation);
        created.MethodSelectionBasis.Should().Be(ProcurementSourcingMethodSelectionBasis.AutomaticRecommendation);
        created.PolicySetId.Should().Be(fixture.PolicySetId);
        created.MethodRuleId.Should().Be(fixture.MethodRuleId);
        created.ThresholdRuleId.Should().Be(fixture.ThresholdRuleId);
        created.AuthorityRouteId.Should().Be(fixture.AuthorityRouteId);
        created.IsSourceCurrent.Should().BeTrue();
        created.Lots.Should().HaveCount(2);
        created.Lots.Sum(item => item.EstimatedValue).Should().Be(300m);
        created.Lots.SelectMany(item => item.Items).Select(item => item.RequisitionItemId)
            .Should().BeEquivalentTo(fixture.Items.Select(item => item.Id));
        created.SourceRequests.Should().ContainSingle(item =>
            item.SourceType == "RequestForQuotation" && item.Status == ProcurementSourcingCaseSourceRequestStatus.Planned);
        created.SourceControlFingerprint.Should().Be(fixture.ReleaseDto.ControlFingerprint);
        created.CaseFingerprint.Should().HaveLength(64);
        created.IntegrityHash.Should().HaveLength(64);
        var events = await fixture.Context.ProcurementControlEvents.OrderBy(item => item.OccurredAtUtc).ToListAsync();
        events.Should().HaveCount(2);
        events.Should().ContainSingle(item => item.Action == "SourcingMethodRecommended" && item.RuleCode == "TDC-0202");
        events.Should().ContainSingle(item => item.Action == "SourcingCaseCreated" && item.RuleCode == "TDC-0201");
    }

    [Fact]
    public async Task ApprovedReleaseWithoutLegacyPlanOrAuthorityLineageCanLockAControlledCase()
    {
        await using var fixture = new Fixture();
        fixture.RemoveLegacyAdvancedLineage();
        var request = fixture.ValidRequest();
        request.Justification = null;
        request.Lots =
        [
            new CreateProcurementSourcingCaseLotRequest
            {
                LotCode = "LOT-01",
                Title = "Approved operational equipment",
                PurchaseRequisitionItemIds = fixture.Items.Select(item => item.Id).ToList()
            }
        ];

        var created = await fixture.Service.CreateAsync(request, "trace-simplified-lineage");

        created.SourcePlanId.Should().BeNull();
        created.SourcePlanItemId.Should().BeNull();
        created.AuthorityRouteId.Should().BeNull();
        created.AuthorityRouteReference.Should().BeNull();
        created.Justification.Should().Contain("selected automatically by policy");
        created.Status.Should().Be(ProcurementSourcingCaseStatus.Ready);
    }

    [Theory]
    [InlineData(ProcurementMethodType.RequestForQuotation)]
    [InlineData(ProcurementMethodType.NationalCompetitiveTendering)]
    [InlineData(ProcurementMethodType.InternationalCompetitiveTendering)]
    [InlineData(ProcurementMethodType.RestrictedTendering)]
    [InlineData(ProcurementMethodType.SingleSource)]
    [InlineData(ProcurementMethodType.PettyPurchase)]
    [InlineData(ProcurementMethodType.FrameworkCallOff)]
    [InlineData(ProcurementMethodType.QualityBasedSelection)]
    [InlineData(ProcurementMethodType.QualityAndCostBasedSelection)]
    public async Task ServerRecommendationIsTheDefaultForEveryModeledMethod(ProcurementMethodType method)
    {
        await using var fixture = new Fixture { RecommendedMethod = method };
        var request = fixture.ValidRequest();
        request.SelectedMethod = null;

        var created = await fixture.Service.CreateAsync(request, $"trace-auto-{method}");

        created.RecommendedMethod.Should().Be(method);
        created.SelectedMethod.Should().Be(method);
        created.MethodSelectionBasis.Should().Be(ProcurementSourcingMethodSelectionBasis.AutomaticRecommendation);
    }

    [Fact]
    public async Task DifferentClientMethodRequiresExactApprovedOverrideLineage()
    {
        await using var fixture = new Fixture();
        var request = fixture.ValidRequest();
        request.SelectedMethod = ProcurementMethodType.SingleSource;
        request.MethodOverrideReason = "Urgent proprietary compatibility requires the approved single-source exception.";

        await fixture.Service.Invoking(service => service.CreateAsync(request, "trace-override-missing"))
            .Should().ThrowAsync<ProcurementSourcingCaseValidationException>()
            .Where(exception => exception.Code == "SOURCING_METHOD_OVERRIDE_RULE_REQUIRED");

        fixture.EnableApprovedOverride(ProcurementMethodType.SingleSource);
        var created = await fixture.Service.CreateAsync(request, "trace-override-approved");

        created.RecommendedMethod.Should().Be(ProcurementMethodType.RequestForQuotation);
        created.SelectedMethod.Should().Be(ProcurementMethodType.SingleSource);
        created.MethodSelectionBasis.Should().Be(ProcurementSourcingMethodSelectionBasis.ApprovedOverride);
        created.ApprovedExceptionRuleId.Should().Be(fixture.OverrideRuleId);
        created.MethodOverrideWorkflowInstanceId.Should().Be(fixture.OverrideWorkflowId);
        created.MethodOverrideApprovalActorUserIds.Should().ContainSingle().Which.Should().Be(fixture.OverrideApproverId);
        created.MethodOverrideReason.Should().Be(request.MethodOverrideReason);
        (await fixture.Context.ProcurementControlEvents.CountAsync(item =>
            item.Action == "SourcingMethodOverrideApproved" && item.RuleCode == "TDC-0202")).Should().Be(1);
    }

    [Fact]
    public async Task OverrideRejectsSelfApprovalThroughExistingSodGuard()
    {
        await using var fixture = new Fixture();
        fixture.EnableApprovedOverride(ProcurementMethodType.SingleSource, selfApproved: true);
        var request = fixture.ValidRequest();
        request.SelectedMethod = ProcurementMethodType.SingleSource;
        request.MethodOverrideReason = "Request the approved single-source exception for compatibility.";

        await fixture.Service.Invoking(service => service.CreateAsync(request, "trace-override-sod"))
            .Should().ThrowAsync<ProcurementSourcingCaseValidationException>()
            .Where(exception => exception.Code == "SOD_CONFLICT");
    }

    [Fact]
    public async Task OverrideRequiresExplicitSourcingApprovalCapability()
    {
        await using var fixture = new Fixture();
        fixture.EnableApprovedOverride(ProcurementMethodType.SingleSource);
        fixture.DenyOverrideCapability();
        var request = fixture.ValidRequest();
        request.SelectedMethod = ProcurementMethodType.SingleSource;
        request.MethodOverrideReason = "Use the independently approved compatibility exception.";

        var readiness = await fixture.Service.GetReadinessAsync(
            fixture.Requisition.Id, request.SelectedMethod, request.MethodOverrideReason);
        readiness.CanCreate.Should().BeFalse();
        readiness.DecisionCode.Should().Be("SOURCING_METHOD_OVERRIDE_FORBIDDEN");
        await fixture.Service.Invoking(service => service.CreateAsync(request, "trace-override-forbidden"))
            .Should().ThrowAsync<ProcurementSourcingCaseAuthorizationException>();
    }

    [Fact]
    public async Task OverrideFailsClosedForMissingEvidenceExpiredApprovalAndRequesterApproval()
    {
        await using (var missingEvidence = new Fixture())
        {
            missingEvidence.EnableApprovedOverride(ProcurementMethodType.SingleSource);
            missingEvidence.ClearOverrideEvidence();
            var request = missingEvidence.OverrideRequest();
            await missingEvidence.Service.Invoking(service => service.CreateAsync(request, "trace-override-evidence"))
                .Should().ThrowAsync<ProcurementSourcingCaseValidationException>()
                .Where(exception => exception.Code == "SOURCING_METHOD_OVERRIDE_EVIDENCE_REQUIRED");
        }

        await using (var expired = new Fixture())
        {
            expired.EnableApprovedOverride(ProcurementMethodType.SingleSource);
            expired.ExpireOverrideApproval();
            var request = expired.OverrideRequest();
            await expired.Service.Invoking(service => service.CreateAsync(request, "trace-override-expired"))
                .Should().ThrowAsync<ProcurementSourcingCaseValidationException>()
                .Where(exception => exception.Code == "SOURCING_METHOD_OVERRIDE_APPROVAL_EXPIRED");
        }

        await using (var requesterApproved = new Fixture())
        {
            requesterApproved.EnableApprovedOverride(ProcurementMethodType.SingleSource, requesterApproved: true);
            var request = requesterApproved.OverrideRequest();
            await requesterApproved.Service.Invoking(service => service.CreateAsync(request, "trace-override-requester"))
                .Should().ThrowAsync<ProcurementSourcingCaseValidationException>()
                .Where(exception => exception.Code == "SOURCING_METHOD_OVERRIDE_SOD_CONFLICT");
        }
    }

    [Fact]
    public async Task ReadyRequisitionAutoRecordsReleaseWhileBlockedMethodStillFailsClosed()
    {
        await using (var ready = new Fixture())
        {
            ready.MarkReadyWithoutRelease();

            var created = await ready.Service.CreateAsync(ready.ValidRequest(), "trace-auto-release");

            created.SourcingReleaseId.Should().Be(ready.ReleaseDto.Id);
            ready.VerifyAutomaticRelease();
            (await ready.Context.ProcurementSourcingCases.CountAsync()).Should().Be(1);
        }

        await using (var blocked = new Fixture())
        {
            blocked.AllowMethod = false;
            await blocked.Service.Invoking(service => service.CreateAsync(blocked.ValidRequest(), "trace-method-blocked"))
                .Should().ThrowAsync<ProcurementSourcingCaseValidationException>()
                .Where(exception => exception.Code == "METHOD_BLOCKED");
            (await blocked.Context.ProcurementSourcingCases.CountAsync()).Should().Be(0);
        }
    }

    [Fact]
    public async Task IdempotentRetryReusesExactReleaseButChangedPayloadConflicts()
    {
        await using var fixture = new Fixture();
        var request = fixture.ValidRequest();

        var first = await fixture.Service.CreateAsync(request, "trace-first");
        var retry = await fixture.Service.CreateAsync(request, "trace-retry");

        retry.Id.Should().Be(first.Id);
        (await fixture.Context.ProcurementSourcingCases.CountAsync()).Should().Be(1);
        var changed = fixture.ValidRequest();
        changed.Justification = "A materially different method justification.";
        await fixture.Service.Invoking(service => service.CreateAsync(changed, "trace-changed"))
            .Should().ThrowAsync<ProcurementSourcingCaseConflictException>()
            .Where(exception => exception.Code == "SOURCING_CASE_RELEASE_ALREADY_USED");
    }

    [Fact]
    public async Task LotCoverageRejectsMissingDuplicateOrMismatchedValueLines()
    {
        await using var fixture = new Fixture();
        var missing = fixture.ValidRequest();
        missing.Lots.RemoveAt(1);
        await fixture.Service.Invoking(service => service.CreateAsync(missing, "trace-missing-line"))
            .Should().ThrowAsync<ProcurementSourcingCaseValidationException>()
            .Where(exception => exception.Code == "SOURCING_CASE_LINE_COVERAGE");

        var duplicate = fixture.ValidRequest();
        duplicate.Lots[1].PurchaseRequisitionItemIds.Add(fixture.Items[0].Id);
        await fixture.Service.Invoking(service => service.CreateAsync(duplicate, "trace-duplicate-line"))
            .Should().ThrowAsync<ProcurementSourcingCaseValidationException>()
            .Where(exception => exception.Code == "SOURCING_CASE_LINE_DUPLICATE");

        fixture.Requisition.TotalAmount = 301m;
        await fixture.Context.SaveChangesAsync();
        await fixture.Service.Invoking(service => service.CreateAsync(fixture.ValidRequest(), "trace-value"))
            .Should().ThrowAsync<ProcurementSourcingCaseValidationException>()
            .Where(exception => exception.Code == "SOURCING_CASE_VALUE_MISMATCH");
    }

    [Fact]
    public async Task SourceEntryAllowsReleaseOnlyAndAdvancedCaseRegistrationFollowsLifecycle()
    {
        await using var fixture = new Fixture();
        var direct = await fixture.Service.EnforceSourceEntryAsync(
            fixture.Requisition.Id, ProcurementMethodType.RequestForQuotation,
            "RequestForQuotation", "RFQ-DIRECT-001", "trace-no-case");
        direct.SourcingCaseId.Should().BeNull();
        direct.SourcingReleaseId.Should().Be(fixture.ReleaseDto.Id);

        var created = await fixture.Service.CreateAsync(fixture.ValidRequest(), "trace-create");
        await fixture.Service.Invoking(service => service.EnforceSourceEntryAsync(
                fixture.Requisition.Id, ProcurementMethodType.SingleSource,
                "Tender", "TND-WRONG-001", "trace-wrong-method"))
            .Should().ThrowAsync<ProcurementRequisitionSourcingValidationException>()
            .Where(exception => exception.Code == "SOURCING_CASE_METHOD_MISMATCH");

        var gate = await fixture.Service.EnforceSourceEntryAsync(
            fixture.Requisition.Id, ProcurementMethodType.RequestForQuotation,
            "RequestForQuotation", "RFQ-CASE-001", "trace-entry");
        gate.SourcingCaseId.Should().Be(created.Id);

        await fixture.Service.RegisterSourceRequestAsync(created.Id, "RequestForQuotation",
            fixture.SourceEntityId, "RFQ-CASE-001", "trace-register");
        var revalidated = await fixture.Service.RevalidateSourceEntryAsync(
            fixture.Requisition.Id, fixture.ReleaseDto.Id, created.Id,
            ProcurementMethodType.RequestForQuotation, "RequestForQuotation",
            fixture.SourceEntityId, "RFQ-CASE-001", "trace-revalidate");
        revalidated.SourcingCaseId.Should().Be(created.Id);
        var started = await fixture.Service.GetAsync(created.Id);
        started.Status.Should().Be(ProcurementSourcingCaseStatus.InProgress);
        started.SourceRequests.Should().ContainSingle(item =>
            item.Status == ProcurementSourcingCaseSourceRequestStatus.Created && item.SourceEntityReference == "RFQ-CASE-001");

        var closed = await fixture.Service.CloseAsync(created.Id,
            new ProcurementSourcingCaseActionRequest { RowVersion = started.RowVersion, Reason = "Controlled source request completed." },
            "trace-close");
        closed.Status.Should().Be(ProcurementSourcingCaseStatus.Closed);
        await fixture.Service.Invoking(service => service.CancelAsync(created.Id,
                new ProcurementSourcingCaseActionRequest { RowVersion = closed.RowVersion, Reason = "Cannot reopen terminal case." }, "trace-terminal"))
            .Should().ThrowAsync<ProcurementSourcingCaseConflictException>();
    }

    [Fact]
    public async Task SourcingManagerCanCancelButCannotCloseWithoutApprovalCapability()
    {
        await using var fixture = new Fixture();
        var cancellable = await fixture.Service.CreateAsync(fixture.ValidRequest(), "trace-create-cancellable");
        fixture.DenyOverrideCapability();

        var cancelled = await fixture.Service.CancelAsync(cancellable.Id,
            new ProcurementSourcingCaseActionRequest
            {
                RowVersion = cancellable.RowVersion,
                Reason = "Replace stale legacy tender sourcing control."
            }, "trace-cancel-as-manager");

        cancelled.Status.Should().Be(ProcurementSourcingCaseStatus.Cancelled);

        await using var closeFixture = new Fixture();
        var started = await closeFixture.Service.CreateAsync(closeFixture.ValidRequest(), "trace-create-close");
        await closeFixture.Service.RegisterSourceRequestAsync(started.Id, "RequestForQuotation",
            closeFixture.SourceEntityId, "RFQ-CLOSE-001", "trace-register-close");
        started = await closeFixture.Service.GetAsync(started.Id);
        closeFixture.DenyOverrideCapability();

        await closeFixture.Service.Invoking(service => service.CloseAsync(started.Id,
                new ProcurementSourcingCaseActionRequest
                {
                    RowVersion = started.RowVersion,
                    Reason = "Controlled source request completed."
                }, "trace-close-as-manager"))
            .Should().ThrowAsync<ProcurementSourcingCaseAuthorizationException>();
    }

    [Fact]
    public async Task TenderEntryAutomaticallyLocksAndReusesThePolicySelectedSourcingCase()
    {
        await using var fixture = new Fixture
        {
            RecommendedMethod = ProcurementMethodType.NationalCompetitiveTendering
        };

        var first = await fixture.Service.EnforceSourceEntryAsync(
            fixture.Requisition.Id,
            null,
            "Tender",
            "TND-AUTO-CASE-001",
            "trace-tender-auto-case");
        var retry = await fixture.Service.EnforceSourceEntryAsync(
            fixture.Requisition.Id,
            null,
            "Tender",
            "TND-AUTO-CASE-001",
            "trace-tender-auto-case-retry");

        first.SourcingReleaseId.Should().Be(fixture.ReleaseDto.Id);
        first.SourcingCaseId.Should().NotBeNull();
        first.SelectedMethod.Should().Be(ProcurementMethodType.NationalCompetitiveTendering);
        retry.SourcingCaseId.Should().Be(first.SourcingCaseId);

        var retained = await fixture.Service.GetAsync(first.SourcingCaseId!.Value);
        retained.Lots.Should().ContainSingle();
        retained.Lots[0].Items.Select(item => item.RequisitionItemId)
            .Should().BeEquivalentTo(fixture.Items.Select(item => item.Id));
        retained.SourceRequests.Should().ContainSingle(item =>
            item.SourceType == "Tender" &&
            item.Status == ProcurementSourcingCaseSourceRequestStatus.Planned);
        (await fixture.Context.ProcurementSourcingCases.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task NewerReleaseCannotCreateASecondCaseWhileEarlierCaseIsActive()
    {
        await using var fixture = new Fixture();
        var active = await fixture.Service.CreateAsync(fixture.ValidRequest(), "trace-active-case");
        fixture.AdvanceToNewRelease();

        var readiness = await fixture.Service.GetReadinessAsync(fixture.Requisition.Id);

        readiness.CanCreate.Should().BeFalse();
        readiness.DecisionCode.Should().Be("SOURCING_CASE_ACTIVE_RELEASE_CONFLICT");
        readiness.CurrentCase.Should().NotBeNull();
        readiness.CurrentCase!.Id.Should().Be(active.Id);
        readiness.Message.Should().Contain(active.CaseNumber);

        await fixture.Service.Invoking(service => service.CreateAsync(
                fixture.ValidRequest(), "trace-second-case"))
            .Should().ThrowAsync<ProcurementSourcingCaseConflictException>()
            .Where(exception => exception.Code == "SOURCING_CASE_ACTIVE_RELEASE_CONFLICT");
        (await fixture.Context.ProcurementSourcingCases.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SourceEntryRejectsStaleActiveCaseBeforeIssuingAnotherRelease()
    {
        await using var fixture = new Fixture();
        var active = await fixture.Service.CreateAsync(fixture.ValidRequest(), "trace-active-source");
        fixture.AdvanceToNewRelease();

        await fixture.Service.Invoking(service => service.EnforceSourceEntryAsync(
                fixture.Requisition.Id,
                ProcurementMethodType.RequestForQuotation,
                "RequestForQuotation",
                "RFQ-SECOND-001",
                "trace-stale-active"))
            .Should().ThrowAsync<ProcurementSourcingCaseConflictException>()
            .Where(exception => exception.Code == "SOURCING_CASE_ACTIVE_RELEASE_CONFLICT" &&
                exception.Message.Contains(active.CaseNumber));

        fixture.VerifyNoSourceReleaseWasIssued();
        (await fixture.Context.ProcurementSourcingCases.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task EvaluatorRecoveryLocksAndRegistersExactCurrentTenderLineage()
    {
        await using var fixture = new Fixture
        {
            RecommendedMethod = ProcurementMethodType.NationalCompetitiveTendering
        };
        var tenderId = Guid.NewGuid();

        var recovered = await fixture.Service.RecoverTenderSourceEntryAsync(
            fixture.Requisition.Id,
            fixture.ReleaseDto.Id,
            tenderId,
            "TND-LEGACY-001",
            "trace-evaluation-recovery");
        var retry = await fixture.Service.RecoverTenderSourceEntryAsync(
            fixture.Requisition.Id,
            fixture.ReleaseDto.Id,
            tenderId,
            "TND-LEGACY-001",
            "trace-evaluation-recovery");

        recovered.SourcingReleaseId.Should().Be(fixture.ReleaseDto.Id);
        recovered.SourcingCaseId.Should().NotBeNull();
        retry.SourcingCaseId.Should().Be(recovered.SourcingCaseId);
        recovered.SelectedMethod.Should().Be(ProcurementMethodType.NationalCompetitiveTendering);
        var retained = await fixture.Service.GetAsync(recovered.SourcingCaseId!.Value);
        retained.Status.Should().Be(ProcurementSourcingCaseStatus.InProgress);
        retained.SourceRequests.Should().ContainSingle(item =>
            item.Status == ProcurementSourcingCaseSourceRequestStatus.Created &&
            item.SourceType == "Tender" &&
            item.SourceEntityId == tenderId &&
            item.SourceEntityReference == "TND-LEGACY-001");
        (await fixture.Context.ProcurementControlEvents.CountAsync(item =>
            item.Action == "SourcingCaseEntryAllowed" &&
            item.CorrelationId == "trace-evaluation-recovery")).Should().Be(1);
        (await fixture.Context.ProcurementControlEvents.CountAsync(item =>
            item.Action == "SourcingSourceRegistered" &&
            item.CorrelationId == "trace-evaluation-recovery")).Should().Be(1);
        (await fixture.Context.ProcurementSourcingCases.CountAsync()).Should().Be(1);
        fixture.VerifyRecoveryAuthorization("procurement.tender.evaluate", Times.Exactly(2));
    }

    [Fact]
    public async Task TenderLineageRecoveryRequiresTenderEvaluationPermission()
    {
        await using var fixture = new Fixture
        {
            RecommendedMethod = ProcurementMethodType.NationalCompetitiveTendering
        };
        fixture.DenyEvaluatorRecovery();

        await fixture.Service.Invoking(service => service.RecoverTenderSourceEntryAsync(
                fixture.Requisition.Id,
                fixture.ReleaseDto.Id,
                Guid.NewGuid(),
                "TND-UNAUTHORIZED-001",
                "trace-evaluation-recovery-denied"))
            .Should().ThrowAsync<ProcurementSourcingCaseAuthorizationException>();
        (await fixture.Context.ProcurementSourcingCases.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(ProcurementTenderSourceRecoveryBoundary.AwardAdministration, "procurement.tender.administer")]
    [InlineData(ProcurementTenderSourceRecoveryBoundary.AwardApproval, "procurement.tender.approve")]
    [InlineData(ProcurementTenderSourceRecoveryBoundary.ContractCreation, "procurement.contract.manage")]
    [InlineData(ProcurementTenderSourceRecoveryBoundary.PurchaseOrderCreation, "procurement.purchase-order.create")]
    public async Task DownstreamTenderRecoveryUsesThePermissionOfItsExactBoundary(
        ProcurementTenderSourceRecoveryBoundary boundary,
        string expectedPermission)
    {
        await using var fixture = new Fixture
        {
            RecommendedMethod = ProcurementMethodType.NationalCompetitiveTendering
        };

        var recovered = await fixture.Service.RecoverTenderSourceEntryAsync(
            fixture.Requisition.Id,
            fixture.ReleaseDto.Id,
            Guid.NewGuid(),
            $"TND-{boundary}",
            $"trace-{boundary}",
            boundary);

        recovered.SourcingCaseId.Should().NotBeNull();
        fixture.VerifyRecoveryAuthorization(expectedPermission);
    }

    [Fact]
    public async Task StalePolicyIntegrityAndTenantIsolationBlockUseAndDisclosure()
    {
        await using var fixture = new Fixture();
        var created = await fixture.Service.CreateAsync(fixture.ValidRequest(), "trace-create");
        fixture.PolicySetId = Guid.NewGuid();
        var stale = await fixture.Service.GetAsync(created.Id);
        stale.IsSourceCurrent.Should().BeFalse();
        stale.SourceStateCode.Should().Be("SOURCING_CASE_POLICY_STALE");
        await fixture.Service.Invoking(service => service.EnforceSourceEntryAsync(
                fixture.Requisition.Id, ProcurementMethodType.RequestForQuotation,
                "RequestForQuotation", "RFQ-STALE", "trace-stale"))
            .Should().ThrowAsync<ProcurementRequisitionSourcingValidationException>()
            .Where(exception => exception.Code == "SOURCING_CASE_POLICY_STALE");

        fixture.TenantId = Guid.NewGuid();
        await fixture.Service.Invoking(service => service.GetAsync(created.Id))
            .Should().ThrowAsync<ProcurementSourcingCaseNotFoundException>();
    }

    [Fact]
    public async Task RetainedCaseReportsUnavailableSourceInsteadOfBreakingHistoryReads()
    {
        await using var fixture = new Fixture();
        var created = await fixture.Service.CreateAsync(fixture.ValidRequest(), "trace-create");
        fixture.MakeSourceUnavailable();

        var retained = await fixture.Service.GetAsync(created.Id);

        retained.IsSourceCurrent.Should().BeFalse();
        retained.SourceStateCode.Should().Be("SOURCING_CASE_SOURCE_NOT_AVAILABLE");
        retained.SourceStateMessage.Should().Contain("retained for history");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly Mock<ICurrentUserProvider> _currentUser = new();
        private readonly Mock<IProcurementComplianceDecisionService> _compliance = new();
        private readonly Mock<IProcurementRequisitionSourcingReleaseService> _releases = new();
        private readonly Mock<IProcurementSodGuardService> _sodGuard = new();
        private readonly Mock<IProcurementAccessControlService> _accessControl = new();
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _overrideApproverId = Guid.NewGuid();
        private Guid _policySetId = Guid.NewGuid();
        private ProcurementRequisitionSourcingRelease _release = null!;
        private Guid _overrideWorkflowDefinitionId;
        private bool _isAdministrator = true;
        private bool _allowOverrideCapability = true;
        private bool _allowEvaluatorRecovery = true;

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            MethodRuleId = Guid.NewGuid();
            ThresholdRuleId = Guid.NewGuid();
            AuthorityRouteId = Guid.NewGuid();
            var planId = Guid.NewGuid();
            var planItemId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);
            Context.Tenants.Add(new Tenant { Id = TenantId, Code = "TDC", Name = "TDC", Status = TenantStatus.Active });
            Requisition = new PurchaseRequisition
            {
                TenantId = TenantId,
                RequisitionNumber = "PR-CASE-0001",
                RequisitionDate = DateTime.UtcNow.AddDays(-3),
                RequestedById = Guid.NewGuid(),
                RequiredDate = DateTime.UtcNow.AddDays(20),
                Status = "Approved",
                ApprovedAt = DateTime.UtcNow.AddDays(-1),
                ApprovedById = Guid.NewGuid(),
                CostCenter = "OPS",
                Justification = "Approved operational requirement.",
                RequisitionType = PurchaseRequisitionType.StockReplenishment,
                SourcePlanId = planId,
                SourcePlanItemId = planItemId,
                ProcurementCategory = ProcurementCategoryClass.Goods,
                Currency = "GHS",
                TotalAmount = 300m
            };
            Items =
            [
                new PurchaseRequisitionItem
                {
                    TenantId = TenantId, RequisitionId = Requisition.Id, Requisition = Requisition,
                    ItemDescription = "Laptops", Quantity = 2, UnitOfMeasure = "EA",
                    EstimatedUnitPrice = 100m, LineTotal = 200m, CreatedAt = DateTime.UtcNow.AddMinutes(-2)
                },
                new PurchaseRequisitionItem
                {
                    TenantId = TenantId, RequisitionId = Requisition.Id, Requisition = Requisition,
                    ItemDescription = "Docking stations", Quantity = 2, UnitOfMeasure = "EA",
                    EstimatedUnitPrice = 50m, LineTotal = 100m, CreatedAt = DateTime.UtcNow.AddMinutes(-1)
                }
            ];
            var release = new ProcurementRequisitionSourcingRelease
            {
                TenantId = TenantId,
                PurchaseRequisitionId = Requisition.Id,
                PurchaseRequisition = Requisition,
                AttemptNumber = 1,
                ReleaseReference = "SRL-PR-CASE-0001-A1",
                SourcePlanId = planId,
                SourcePlanItemId = planItemId,
                SpecificationTemplateId = Guid.NewGuid(),
                SpecificationTemplateCode = "SPEC-GOODS",
                SpecificationTemplateVersion = 1,
                BudgetCommitmentId = Guid.NewGuid(),
                BudgetCommitmentReference = "BCR-CASE-001",
                AuthorityRouteId = AuthorityRouteId,
                AuthorityRouteReference = "ARR-CASE-001",
                WorkflowInstanceId = Guid.NewGuid(),
                ReleasedAtUtc = DateTime.UtcNow.AddHours(-1),
                ReleasedById = Guid.NewGuid(),
                ReleasedByName = "Procurement Controller",
                ReleaseReason = "Approved for sourcing.",
                CorrelationId = "trace-release",
                ControlFingerprint = new string('a', 64),
                SnapshotJson = "{}",
                IntegrityHash = new string('b', 64)
            };
            _release = release;
            ReleaseDto = new PurchaseRequisitionSourcingReleaseDto
            {
                Id = release.Id,
                RequisitionId = Requisition.Id,
                RequisitionNumber = Requisition.RequisitionNumber,
                AttemptNumber = 1,
                ReleaseReference = release.ReleaseReference,
                SourcePlanId = planId,
                SourcePlanItemId = planItemId,
                SpecificationTemplateId = release.SpecificationTemplateId,
                SpecificationTemplateCode = release.SpecificationTemplateCode,
                SpecificationTemplateVersion = release.SpecificationTemplateVersion,
                BudgetCommitmentId = release.BudgetCommitmentId,
                BudgetCommitmentReference = release.BudgetCommitmentReference,
                AuthorityRouteId = release.AuthorityRouteId,
                AuthorityRouteReference = release.AuthorityRouteReference,
                WorkflowInstanceId = release.WorkflowInstanceId,
                ReleasedAtUtc = release.ReleasedAtUtc,
                ReleasedById = release.ReleasedById,
                ReleasedByName = release.ReleasedByName,
                ReleaseReason = release.ReleaseReason,
                CorrelationId = release.CorrelationId,
                ControlFingerprint = release.ControlFingerprint,
                IntegrityHash = release.IntegrityHash
            };
            Readiness = new PurchaseRequisitionSourcingReadinessDto
            {
                RequisitionId = Requisition.Id,
                RequisitionNumber = Requisition.RequisitionNumber,
                Status = Requisition.Status,
                IsCompliant = true,
                IsReleased = true,
                DecisionCode = "PR_SOURCING_RELEASE_CURRENT",
                Message = "Current immutable release is available.",
                ControlFingerprint = release.ControlFingerprint,
                CurrentRelease = ReleaseDto
            };
            Context.AddRange(Requisition, release);
            Context.AddRange(Items);
            Context.Add(new ProcurementPolicyMethodRule
            {
                Id = MethodRuleId,
                TenantId = TenantId,
                PolicySetId = PolicySetId,
                RuleCode = "METHOD-RequestForQuotation",
                Name = "RFQ method rule",
                Category = ProcurementCategoryClass.Goods,
                Method = ProcurementMethodType.RequestForQuotation,
                IsAllowed = true,
                RequiresCompetition = true,
                MinimumQuotationCount = 2,
                SourceDecisionKey = "DEC-001",
                IsEnabled = true,
                EffectiveFrom = DateTime.UtcNow.AddDays(-30),
                RowVersion = Guid.NewGuid().ToByteArray()
            });
            Context.Add(new ProcurementPolicyThresholdRule
            {
                Id = ThresholdRuleId,
                TenantId = TenantId,
                PolicySetId = PolicySetId,
                RuleCode = "THRESHOLD-RequestForQuotation",
                Name = "Approved sourcing value band",
                Category = ProcurementCategoryClass.Goods,
                Method = ProcurementMethodType.RequestForQuotation,
                CurrencyCode = "GHS",
                LowerBound = 0m,
                UpperBound = 100000m,
                StatutoryReference = "Configured procurement threshold",
                SourceDecisionKey = "DEC-001",
                IsEnabled = true,
                EffectiveFrom = DateTime.UtcNow.AddDays(-30),
                RowVersion = Guid.NewGuid().ToByteArray()
            });
            Context.SaveChanges();

            _currentUser.SetupGet(item => item.TenantId).Returns(() => TenantId);
            _currentUser.SetupGet(item => item.UserId).Returns(_userId);
            _currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            _currentUser.SetupGet(item => item.Username).Returns("procurement.controller@tdc.test");
            _currentUser.SetupGet(item => item.FullName).Returns("Procurement Controller");
            _currentUser.SetupGet(item => item.Roles).Returns(() =>
                _isAdministrator ? new[] { "TenantAdmin" } : new[] { "ProcurementOfficer" });
            _currentUser.Setup(item => item.HasRole(It.IsAny<string>())).Returns((string role) =>
                _isAdministrator && string.Equals(role, "TenantAdmin", StringComparison.Ordinal));
            _releases.Setup(item => item.GetReadinessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Readiness);
            _releases.Setup(item => item.GetLinkedControlReadinessAsync(
                    It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Readiness);
            _releases.Setup(item => item.EnforceSourcingAsync(Requisition.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => ReleaseDto);
            _releases.Setup(item => item.ReleaseAsync(
                    Requisition.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    Readiness.IsReleased = true;
                    Readiness.CanRelease = false;
                    Readiness.DecisionCode = "PR_SOURCING_RELEASE_CURRENT";
                    Readiness.Message = "System-generated release is current.";
                    Readiness.CurrentRelease = ReleaseDto;
                    return ReleaseDto;
                });
            _compliance.Setup(item => item.EvaluateAsync(
                    It.IsAny<ProcurementComplianceDecisionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementComplianceDecisionRequest request, string _, CancellationToken _) => Decision(request.RequestedMethod));
            _sodGuard.Setup(item => item.CheckAsync(It.IsAny<ProcurementSodGuardRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementSodGuardRequest request, string _, CancellationToken _) => SodDecision(request));
            _sodGuard.Setup(item => item.EnforceAsync(It.IsAny<ProcurementSodGuardRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementSodGuardRequest request, string _, CancellationToken _) => SodDecision(request));
            _accessControl.Setup(item => item.CheckCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementAccessCapabilityRequest request, string _, CancellationToken _) => CapabilityDecision(request));
            _accessControl.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementAccessCapabilityRequest request, string _, CancellationToken _) => CapabilityDecision(request));

            _unitOfWork = new UnitOfWork(Context);
            var events = new ProcurementControlEventService(
                _unitOfWork, _currentUser.Object, NullLogger<ProcurementControlEventService>.Instance);
            Service = new ProcurementSourcingCaseService(
                _unitOfWork, _currentUser.Object, _accessControl.Object,
                _compliance.Object, events, _sodGuard.Object, _releases.Object,
                NullLogger<ProcurementSourcingCaseService>.Instance);
        }

        public Guid TenantId { get; set; }
        public Guid PolicySetId { get => _policySetId; set => _policySetId = value; }
        public Guid MethodRuleId { get; }
        public Guid ThresholdRuleId { get; }
        public Guid AuthorityRouteId { get; }
        public Guid OverrideRuleId { get; private set; }
        public Guid OverrideWorkflowId { get; private set; }
        public Guid OverrideApproverId => _overrideApproverId;
        public Guid SourceEntityId { get; } = Guid.NewGuid();
        public ProcurementMethodType RecommendedMethod { get; set; } = ProcurementMethodType.RequestForQuotation;
        public bool AllowMethod { get; set; } = true;
        public ApplicationDbContext Context { get; }
        public PurchaseRequisition Requisition { get; }
        public List<PurchaseRequisitionItem> Items { get; }
        public PurchaseRequisitionSourcingReleaseDto ReleaseDto { get; }
        public PurchaseRequisitionSourcingReadinessDto Readiness { get; }
        public ProcurementSourcingCaseService Service { get; }

        public void MarkReadyWithoutRelease()
        {
            Readiness.IsCompliant = true;
            Readiness.IsReleased = false;
            Readiness.CanRelease = true;
            Readiness.DecisionCode = "PR_SOURCING_READY";
            Readiness.Message = "Ready for sourcing.";
            Readiness.CurrentRelease = null;
        }

        public void VerifyAutomaticRelease() =>
            _releases.Verify(item => item.ReleaseAsync(
                    Requisition.Id,
                    It.Is<string>(reason => reason.StartsWith("System-generated release for sourcing case")),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

        public void VerifyNoSourceReleaseWasIssued() =>
            _releases.Verify(item => item.EnforceSourcingAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);

        public void AdvanceToNewRelease()
        {
            var fingerprint = new string('c', 64);
            Readiness.ControlFingerprint = fingerprint;
            Readiness.IsReleased = true;
            Readiness.CanRelease = false;
            Readiness.CurrentRelease = new PurchaseRequisitionSourcingReleaseDto
            {
                Id = Guid.NewGuid(),
                RequisitionId = Requisition.Id,
                RequisitionNumber = Requisition.RequisitionNumber,
                AttemptNumber = ReleaseDto.AttemptNumber + 1,
                ReleaseReference = $"SRL-{Requisition.RequisitionNumber}-A{ReleaseDto.AttemptNumber + 1}",
                ReleasedAtUtc = DateTime.UtcNow,
                ReleasedById = Guid.NewGuid(),
                ReleasedByName = "Procurement Controller",
                ReleaseReason = "Updated sourcing controls.",
                CorrelationId = "trace-new-release",
                ControlFingerprint = fingerprint,
                IntegrityHash = new string('d', 64)
            };
        }

        public void VerifyEvaluatorRecoveryAuthorization() =>
            VerifyRecoveryAuthorization("procurement.tender.evaluate");

        public void VerifyRecoveryAuthorization(string permission) =>
            VerifyRecoveryAuthorization(permission, Times.Once());

        public void VerifyRecoveryAuthorization(string permission, Times times) =>
            _accessControl.Verify(item => item.EnforceCapabilityAsync(
                    It.Is<ProcurementAccessCapabilityRequest>(request =>
                        request.PermissionCode == permission),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()),
                times);

        public void MakeSourceUnavailable() =>
            _releases.Setup(item => item.GetLinkedControlReadinessAsync(
                    Requisition.Id, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ProcurementRequisitionSourcingNotFoundException(
                    "PR_NOT_FOUND", "The purchase requisition is no longer operationally available."));

        public void RemoveLegacyAdvancedLineage()
        {
            Requisition.SourcePlanId = null;
            Requisition.SourcePlanItemId = null;
            _release.SourcePlanId = null;
            _release.SourcePlanItemId = null;
            _release.AuthorityRouteId = null;
            _release.AuthorityRouteReference = null;
            ReleaseDto.SourcePlanId = null;
            ReleaseDto.SourcePlanItemId = null;
            ReleaseDto.AuthorityRouteId = null;
            ReleaseDto.AuthorityRouteReference = null;
            Context.SaveChanges();
        }

        public void DenyOverrideCapability()
        {
            _isAdministrator = false;
            _allowOverrideCapability = false;
        }

        public void DenyEvaluatorRecovery() => _allowEvaluatorRecovery = false;

        public CreateProcurementSourcingCaseRequest ValidRequest() => new()
        {
            RequisitionId = Requisition.Id,
            SelectedMethod = null,
            Justification = "Request quotations for approved operational equipment.",
            Lots =
            [
                new CreateProcurementSourcingCaseLotRequest
                {
                    LotCode = "LOT-ICT", Title = "Laptop equipment",
                    PurchaseRequisitionItemIds = [Items[0].Id]
                },
                new CreateProcurementSourcingCaseLotRequest
                {
                    LotCode = "LOT-ACC", Title = "Accessories",
                    PurchaseRequisitionItemIds = [Items[1].Id]
                }
            ]
        };

        public CreateProcurementSourcingCaseRequest OverrideRequest()
        {
            var request = ValidRequest();
            request.SelectedMethod = ProcurementMethodType.SingleSource;
            request.MethodOverrideReason = "Use the independently approved compatibility exception.";
            return request;
        }

        public void EnableApprovedOverride(
            ProcurementMethodType method,
            bool selfApproved = false,
            bool requesterApproved = false)
        {
            OverrideRuleId = Guid.NewGuid();
            OverrideWorkflowId = Guid.NewGuid();
            _overrideWorkflowDefinitionId = Guid.NewGuid();
            var entityType = new WorkflowEntityType
            {
                Id = Guid.NewGuid(), TenantId = TenantId, Code = "PROCUREMENT_EXCEPTION",
                Name = "Procurement Exception", IsActive = true
            };
            var definition = new WorkflowDefinition
            {
                Id = _overrideWorkflowDefinitionId, TenantId = TenantId, DefinitionKey = Guid.NewGuid(),
                Name = "Procurement exception approval", EntityTypeId = entityType.Id,
                EntityType = entityType, Version = 1,
                LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published, IsActive = true,
                PublishedAt = DateTime.UtcNow.AddDays(-2), PublishedById = Guid.NewGuid()
            };
            var step = new WorkflowStep
            {
                Id = Guid.NewGuid(), TenantId = TenantId, WorkflowDefinitionId = definition.Id,
                WorkflowDefinition = definition, Name = "Approve method override",
                StepType = WorkflowStepType.Approval, Order = 1, IsStartStep = true, IsEndStep = true
            };
            var workflow = new WorkflowInstance
            {
                Id = OverrideWorkflowId, TenantId = TenantId, WorkflowDefinitionId = definition.Id,
                WorkflowDefinition = definition, EntityId = Requisition.Id, EntityTypeId = entityType.Id,
                EntityType = entityType, Status = WorkflowInstanceStatus.Completed,
                InitiatedById = Guid.NewGuid(), CreatedDate = DateTime.UtcNow.AddDays(-1),
                StartedDate = DateTime.UtcNow.AddHours(-8), CompletedDate = DateTime.UtcNow.AddHours(-2)
            };
            var stepInstance = new WorkflowStepInstance
            {
                Id = Guid.NewGuid(), TenantId = TenantId, WorkflowInstanceId = workflow.Id,
                WorkflowInstance = workflow, WorkflowStepId = step.Id, WorkflowStep = step,
                Status = WorkflowStepInstanceStatus.Completed, CreatedDate = workflow.CreatedDate,
                StartedDate = workflow.StartedDate, CompletedDate = workflow.CompletedDate
            };
            var approvalActor = requesterApproved ? Requisition.RequestedById : selfApproved ? _userId : _overrideApproverId;
            var approval = new WorkflowApproval
            {
                Id = Guid.NewGuid(), TenantId = TenantId, StepInstanceId = stepInstance.Id,
                StepInstance = stepInstance, ApproverRole = "TDC_HEAD_OF_PROCUREMENT",
                Status = WorkflowApprovalStatus.Approved, RequestedDate = workflow.StartedDate!.Value,
                ProcessedDate = workflow.CompletedDate, ProcessedById = approvalActor
            };
            stepInstance.Approvals.Add(approval);
            workflow.StepInstances.Add(stepInstance);
            definition.Steps.Add(step);

            var policy = new ProcurementPolicySet
            {
                Id = PolicySetId, TenantId = TenantId, PolicyKey = Guid.NewGuid(),
                Code = "TDC-SOURCING", Name = "TDC sourcing controls", Version = 4,
                LifecycleStatus = ProcurementPolicyLifecycleStatus.Published,
                ScopeType = ProcurementPolicyScopeType.TenantBaseline,
                SourceConfigurationProfileId = Guid.NewGuid(), DefaultCurrencyCode = "GHS",
                EffectiveFrom = DateTime.UtcNow.AddDays(-30), IsDefault = true,
                PublishedAt = DateTime.UtcNow.AddDays(-30), PublishedById = Guid.NewGuid()
            };
            var rule = new ProcurementPolicyExceptionRule
            {
                Id = OverrideRuleId, TenantId = TenantId, PolicySetId = policy.Id, PolicySet = policy,
                RuleCode = "EX-METHOD-OVERRIDE", ExceptionName = "Approved method override",
                ExceptionType = "METHOD_OVERRIDE", Category = ProcurementCategoryClass.Goods,
                Method = method, Disposition = ProcurementExceptionDisposition.ApprovalRequired,
                JustificationRequired = true, EvidenceRequired = true,
                ApproverRole = "TDC_HEAD_OF_PROCUREMENT", WorkflowDefinitionId = definition.Id,
                MaximumDurationDays = 30, SourceDecisionKey = "DEC-006", IsEnabled = true,
                EffectiveFrom = DateTime.UtcNow.AddDays(-30)
            };
            policy.ExceptionRules.Add(rule);

            Requisition.ApprovedExceptionRuleId = rule.Id;
            Requisition.ExceptionWorkflowInstanceId = workflow.Id;
            Requisition.ExceptionApprovalReference = "EXC-APPROVAL-0001";
            Requisition.ExceptionEvidenceReference = "EVIDENCE-METHOD-OVERRIDE-0001";
            Requisition.ExceptionApprovedAtUtc = workflow.CompletedDate;
            Requisition.ExceptionApprovedById = approvalActor;
            _release.ApprovedExceptionRuleId = rule.Id;
            _release.ExceptionWorkflowInstanceId = workflow.Id;
            _release.ExceptionApprovalReference = Requisition.ExceptionApprovalReference;
            ReleaseDto.ApprovedExceptionRuleId = rule.Id;
            ReleaseDto.ExceptionApprovalReference = Requisition.ExceptionApprovalReference;
            Readiness.ApprovedExceptionRuleId = rule.Id;
            Readiness.ExceptionWorkflowInstanceId = workflow.Id;
            Readiness.ExceptionApprovalReference = Requisition.ExceptionApprovalReference;

            Context.AddRange(entityType, definition, step, workflow, stepInstance, approval, policy, rule);
            Context.SaveChanges();
        }

        public void ClearOverrideEvidence()
        {
            Requisition.ExceptionEvidenceReference = null;
            Context.SaveChanges();
        }

        public void ExpireOverrideApproval()
        {
            var workflow = Context.WorkflowInstances.Single(item => item.Id == OverrideWorkflowId);
            var completedAt = DateTime.UtcNow.AddDays(-40);
            workflow.CompletedDate = completedAt;
            foreach (var step in Context.WorkflowStepInstances.Where(item => item.WorkflowInstanceId == workflow.Id))
                step.CompletedDate = completedAt;
            foreach (var approval in Context.WorkflowApprovals.Where(item =>
                         Context.WorkflowStepInstances.Where(step => step.WorkflowInstanceId == workflow.Id)
                             .Select(step => step.Id).Contains(item.StepInstanceId)))
                approval.ProcessedDate = completedAt;
            Requisition.ExceptionApprovedAtUtc = completedAt;
            Context.SaveChanges();
        }

        private ProcurementSodGuardDecisionDto SodDecision(ProcurementSodGuardRequest request)
        {
            var conflict = request.ProhibitedActorUserIds.Contains(_userId);
            return new ProcurementSodGuardDecisionDto
            {
                Allowed = !conflict,
                Code = conflict ? "SOD_CONFLICT" : "SOD_ALLOWED",
                Message = conflict
                    ? "The current user participated in the linked approval and cannot create this override case."
                    : "No segregation-of-duties conflict was found.",
                ControlCode = request.ControlCode,
                SourceType = request.SourceType,
                SourceReference = request.SourceReference,
                ActorUserId = _userId
            };
        }

        private ProcurementAccessCapabilityDecisionDto CapabilityDecision(ProcurementAccessCapabilityRequest request)
        {
            var allowed = (!string.Equals(request.PermissionCode, "procurement.sourcing.approve", StringComparison.Ordinal) ||
                    _allowOverrideCapability) &&
                (!string.Equals(request.PermissionCode, "procurement.tender.evaluate", StringComparison.Ordinal) ||
                    _allowEvaluatorRecovery);
            return new ProcurementAccessCapabilityDecisionDto
            {
                Allowed = allowed,
                Code = allowed ? "CAPABILITY_ALLOWED" : "CAPABILITY_DENIED",
                Message = allowed ? "Capability is available." : "The sourcing approval capability is required.",
                ActorUserId = _userId,
                TenantId = TenantId,
                PermissionCode = request.PermissionCode,
                EvaluatedAtUtc = DateTime.UtcNow
            };
        }

        private ProcurementComplianceDecisionDto Decision(ProcurementMethodType? requested) => new()
        {
            EvaluationId = Guid.NewGuid(),
            EvaluatedAtUtc = DateTime.UtcNow,
            PolicyDateUtc = DateTime.UtcNow,
            CorrelationId = "trace-policy",
            Outcome = AllowMethod ? ProcurementComplianceOutcome.Allowed : ProcurementComplianceOutcome.Blocked,
            Policy = new ProcurementCompliancePolicySelectionDto
            {
                PolicySetId = PolicySetId,
                PolicyCode = "TDC-SOURCING",
                PolicyName = "TDC sourcing controls",
                Version = 4,
                CurrencyCode = "GHS"
            },
            Category = ProcurementCategoryClass.Goods,
            Amount = Requisition.TotalAmount,
            CurrencyCode = "GHS",
            SourceType = "ProcurementSourcingCase",
            SourceReference = Requisition.RequisitionNumber,
            ActorUserId = _userId,
            ActorRoles = ["TenantAdmin"],
            RequestedMethod = requested,
            SelectedMethod = requested ?? RecommendedMethod,
            MethodCandidates =
            [
                new ProcurementComplianceMethodCandidateDto
                {
                    Method = requested ?? RecommendedMethod,
                    IsAllowed = AllowMethod,
                    MethodRuleCode = $"METHOD-{requested ?? RecommendedMethod}",
                    ThresholdRuleCode = $"THRESHOLD-{requested ?? RecommendedMethod}",
                    MatchesAmount = true
                }
            ],
            HardStops = AllowMethod ? [] :
            [
                new ProcurementComplianceFindingDto
                {
                    Code = "METHOD_BLOCKED", Message = "RFQ is not allowed.",
                    Severity = ProcurementComplianceFindingSeverity.HardStop
                }
            ],
            MatchedRules =
            [
                new ProcurementComplianceRuleReferenceDto
                {
                    PolicySetId = PolicySetId, PolicyCode = "TDC-SOURCING", PolicyVersion = 4,
                    RuleId = MethodRuleId, RuleKind = ProcurementPolicyRuleKind.Method,
                    RuleCode = $"METHOD-{requested ?? RecommendedMethod}", RuleName = "Recommended method", SourceDecisionKey = "DEC-001"
                },
                new ProcurementComplianceRuleReferenceDto
                {
                    PolicySetId = PolicySetId, PolicyCode = "TDC-SOURCING", PolicyVersion = 4,
                    RuleId = ThresholdRuleId, RuleKind = ProcurementPolicyRuleKind.Threshold,
                    RuleCode = $"THRESHOLD-{requested ?? RecommendedMethod}", RuleName = "Method threshold", SourceDecisionKey = "DEC-001"
                }
            ],
            SelectedException = requested.HasValue && requested != RecommendedMethod && OverrideRuleId != Guid.Empty
                ? new ProcurementComplianceExceptionDto
                {
                    RuleId = OverrideRuleId, RuleCode = "EX-METHOD-OVERRIDE", ExceptionName = "Approved method override",
                    ExceptionType = "METHOD_OVERRIDE", Disposition = ProcurementExceptionDisposition.ApprovalRequired,
                    JustificationRequired = true, EvidenceRequired = true, WorkflowDefinitionId = _overrideWorkflowDefinitionId,
                    ApproverRole = "TDC_HEAD_OF_PROCUREMENT", SourceDecisionKey = "DEC-006"
                }
                : null
        };

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }
}
