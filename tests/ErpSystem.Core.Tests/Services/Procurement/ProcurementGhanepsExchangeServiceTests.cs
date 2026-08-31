using System.Data;
using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementGhanepsExchangeServiceTests
{
    [Fact]
    public async Task MissingEffectiveDec009IsAdvisoryForOptionsButMutationsRemainStrict()
    {
        await using var fixture = new Fixture();
        fixture.Profile.LifecycleStatus = ProcurementConfigurationProfileStatus.Retired;
        fixture.Context.Update(fixture.Profile);
        await fixture.Context.SaveChangesAsync();

        var options = await fixture.Service.GetOptionsAsync(
            ProcurementGhanepsSourceType.Tender, fixture.Tender.Id);
        var mutation = () => fixture.PrepareAsync("missing-profile", "PUB");

        options.IsConfigured.Should().BeFalse();
        options.AllowedActions.Should().BeEmpty();
        options.ConfigurationMessage.Should().Contain("optional");
        await mutation.Should().ThrowAsync<ProcurementGhanepsExchangeConflictException>()
            .Where(exception => exception.Code == "GHANEPS_PROFILE_NOT_EFFECTIVE");
    }

    [Fact]
    public async Task OptionsAndExportUseOneEffectiveDec009ProfileAndServerDerivedSource()
    {
        await using var fixture = new Fixture();

        var options = await fixture.Service.GetOptionsAsync(
            ProcurementGhanepsSourceType.Tender, fixture.Tender.Id);
        var first = await fixture.PrepareAsync("export-1", "PUB");
        var replay = await fixture.PrepareAsync("export-1", "PUB");

        options.ConfigurationProfileId.Should().Be(fixture.Profile.Id);
        options.IsConfigured.Should().BeTrue();
        options.ConfigurationDecisionId.Should().Be(fixture.Decision.Id);
        options.Mappings.Should().Contain(item =>
            item.MappingKey == "PUB" &&
            item.TemplateReference == "config://ghaneps/tender-publication");
        first.Id.Should().Be(replay.Id);
        first.SourceReference.Should().Be(fixture.Tender.TenderNumber);
        first.SourceVariant.Should().Be("FormalTender");
        first.EventReference.Should().Be(fixture.Tender.TenderNumber);
        first.Payloads.Should().ContainSingle();
        first.Payloads[0].PayloadChecksumSha256.Should().HaveLength(64);
        first.ConfigurationValueHash.Should().HaveLength(64);
        first.MappingIntegrityHash.Should().HaveLength(64);
        fixture.ControlEvents.Should().ContainSingle(item =>
            item.Action == "ExportPrepared" &&
            item.DecisionKeys.SequenceEqual(new[] { "DEC-009" }) &&
            item.RuleId == fixture.Decision.Id);
    }

    [Fact]
    public async Task UniqueDefaultDec009WinsWhenAnotherEffectiveProfileExists()
    {
        await using var fixture = new Fixture();
        await fixture.AddSecondEffectiveProfileAsync();

        var options = await fixture.Service.GetOptionsAsync(
            ProcurementGhanepsSourceType.Tender, fixture.Tender.Id);

        options.ConfigurationProfileId.Should().Be(fixture.Profile.Id);
        options.ConfigurationDecisionId.Should().Be(fixture.Decision.Id);
    }

    [Fact]
    public async Task MultipleDefaultEffectiveDec009ProfilesFailClosed()
    {
        await using var fixture = new Fixture();
        await fixture.AddSecondEffectiveProfileAsync(isDefault: true);

        var action = () => fixture.Service.GetOptionsAsync(
            ProcurementGhanepsSourceType.Tender, fixture.Tender.Id);

        await action.Should().ThrowAsync<ProcurementGhanepsExchangeConflictException>()
            .Where(exception => exception.Code == "GHANEPS_PROFILE_AMBIGUOUS");
    }

    [Fact]
    public async Task SoleNonDefaultEffectiveDec009ProfileIsUsed()
    {
        await using var fixture = new Fixture();
        fixture.Profile.IsDefault = false;
        fixture.Context.Update(fixture.Profile);
        await fixture.Context.SaveChangesAsync();

        var options = await fixture.Service.GetOptionsAsync(
            ProcurementGhanepsSourceType.Tender, fixture.Tender.Id);

        options.ConfigurationProfileId.Should().Be(fixture.Profile.Id);
    }

    [Fact]
    public async Task MultipleNonDefaultEffectiveDec009ProfilesFailClosed()
    {
        await using var fixture = new Fixture();
        fixture.Profile.IsDefault = false;
        fixture.Context.Update(fixture.Profile);
        await fixture.Context.SaveChangesAsync();
        await fixture.AddSecondEffectiveProfileAsync();

        var action = () => fixture.Service.GetOptionsAsync(
            ProcurementGhanepsSourceType.Tender, fixture.Tender.Id);

        await action.Should().ThrowAsync<ProcurementGhanepsExchangeConflictException>()
            .Where(exception => exception.Code == "GHANEPS_PROFILE_AMBIGUOUS");
    }

    [Fact]
    public async Task UndefinedConfiguredEnumFailsBeforeAnyExchangeWrite()
    {
        await using var fixture = new Fixture();
        fixture.ReplaceMappings([
            fixture.Mapping("BAD", (ProcurementGhanepsEventFamily)99,
                ProcurementGhanepsExchangeDirection.Export,
                [ProcurementGhanepsSourceType.Tender])
        ]);
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.GetOptionsAsync(
            ProcurementGhanepsSourceType.Tender, fixture.Tender.Id);

        await action.Should().ThrowAsync<ProcurementGhanepsExchangeValidationException>()
            .Where(exception => exception.Code == "GHANEPS_MAPPING_EVENT_FAMILY_INVALID");
        fixture.Context.Set<ProcurementGhanepsExchangeEvent>().Should().BeEmpty();
    }

    [Theory]
    [InlineData("EventFamily", "GHANEPS_MAPPING_EVENT_FAMILY_INVALID")]
    [InlineData("Direction", "GHANEPS_MAPPING_DIRECTION_INVALID")]
    public async Task OmittedRequiredConfiguredEnumFailsBeforeAnyExchangeWrite(
        string omittedProperty,
        string expectedCode)
    {
        await using var fixture = new Fixture();
        var properties = JsonSerializer
            .Deserialize<Dictionary<string, JsonElement>>(
                JsonSerializer.Serialize(fixture.Mapping(
                    "OMITTED",
                    ProcurementGhanepsEventFamily.TenderPublication,
                    ProcurementGhanepsExchangeDirection.Export,
                    [ProcurementGhanepsSourceType.Tender])))!;
        properties.Remove(omittedProperty).Should().BeTrue();
        fixture.ReplaceRawMappings([JsonSerializer.Serialize(properties)]);
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.GetOptionsAsync(
            ProcurementGhanepsSourceType.Tender, fixture.Tender.Id);

        await action.Should().ThrowAsync<ProcurementGhanepsExchangeValidationException>()
            .Where(exception => exception.Code == expectedCode);
        fixture.Context.Set<ProcurementGhanepsExchangeEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task RejectedAcknowledgementCanBeCorrectedRetriedAcceptedAndReconciled()
    {
        await using var fixture = new Fixture();
        var prepared = await fixture.PrepareAsync("chain-export", "PUB");
        var failed = await fixture.Service.RecordAttemptAsync(prepared.Id,
            new RecordProcurementGhanepsAttemptRequest
            {
                PayloadId = prepared.Payloads[0].Id,
                Outcome = ProcurementGhanepsAttemptOutcome.Failed,
                FailureCode = "NETWORK",
                FailureMessage = "Transport unavailable",
                EvidenceReference = "ops://attempt/failure",
                IdempotencyKey = "attempt-failed",
                ExpectedRowVersion = prepared.RowVersion
            }, "attempt-failed");
        var firstRetry = await fixture.Service.RetryAsync(prepared.Id,
            new RetryProcurementGhanepsExchangeRequest
            {
                Outcome = ProcurementGhanepsAttemptOutcome.Succeeded,
                TransportReference = "GH-TRANSFER-001",
                ReplacementPayloadContent = """{"version":2,"notice":"corrected"}""",
                FileName = "notice-v2.json",
                EvidenceReference = "ops://attempt/retry-1",
                IdempotencyKey = "retry-1",
                ExpectedRowVersion = failed.RowVersion
            }, "retry-1");

        var acknowledgementActor = Guid.NewGuid();
        fixture.SwitchActor(acknowledgementActor);
        var rejected = await fixture.Service.RecordAcknowledgementAsync(prepared.Id,
            fixture.Acknowledgement(
                ProcurementGhanepsAcknowledgementOutcome.Rejected,
                "GH-ACK-REJECTED", "ack-rejected", firstRetry.RowVersion),
            "ack-rejected");

        var corrected = await fixture.Service.RetryAsync(prepared.Id,
            new RetryProcurementGhanepsExchangeRequest
            {
                Outcome = ProcurementGhanepsAttemptOutcome.Succeeded,
                TransportReference = "GH-TRANSFER-002",
                ReplacementPayloadContent = """{"version":3,"notice":"accepted"}""",
                FileName = "notice-v3.json",
                EvidenceReference = "ops://attempt/retry-2",
                IdempotencyKey = "retry-2",
                ExpectedRowVersion = rejected.RowVersion
            }, "retry-2");
        corrected.Payloads.OrderBy(item => item.Version).Last().RecordedByUserId
            .Should().Be(acknowledgementActor);

        fixture.SwitchActor(Guid.NewGuid());
        var accepted = await fixture.Service.RecordAcknowledgementAsync(prepared.Id,
            fixture.Acknowledgement(
                ProcurementGhanepsAcknowledgementOutcome.Accepted,
                "GH-ACK-ACCEPTED", "ack-accepted", corrected.RowVersion),
            "ack-accepted");
        accepted.Acknowledgements.Should().HaveCount(2);
        accepted.Acknowledgements.Select(item => item.Outcome).Should().Equal(
            ProcurementGhanepsAcknowledgementOutcome.Rejected,
            ProcurementGhanepsAcknowledgementOutcome.Accepted);
        accepted.Acknowledgements[1].AttemptId.Should().Be(
            accepted.Attempts.OrderBy(item => item.AttemptNumber).Last().Id);

        fixture.SwitchActor(Guid.NewGuid());
        var authoritativePayload = accepted.Payloads.Single(item =>
            item.Id == accepted.Acknowledgements[1].PayloadId);
        var reconciled = await fixture.Service.ReconcileAsync(prepared.Id,
            new ReconcileProcurementGhanepsExchangeRequest
            {
                ActualReference = fixture.Tender.TenderNumber,
                ActualChecksumSha256 = authoritativePayload.PayloadChecksumSha256,
                EvidenceReference = "reconciliation://matched",
                IdempotencyKey = "reconcile-matched",
                ExpectedRowVersion = accepted.RowVersion
            }, "reconcile-matched");

        reconciled.Status.Should().Be(ProcurementGhanepsExchangeStatus.Reconciled);
        reconciled.Reconciliations.Should().ContainSingle(item =>
            item.Outcome == ProcurementGhanepsReconciliationOutcome.Matched);
        reconciled.History.Should().Contain(item => item.Kind == "RetryAttempt");
        fixture.Notifications.Select(item => item.TopicKey).Should().Contain(
            "procurement.ghaneps.exchange.ack-rejected");
    }

    [Fact]
    public async Task PayloadOrAttemptActorCannotAcknowledgeOwnExchange()
    {
        await using var fixture = new Fixture();
        var prepared = await fixture.PrepareAsync("sod-export", "PUB");
        var sent = await fixture.Service.RecordAttemptAsync(prepared.Id,
            new RecordProcurementGhanepsAttemptRequest
            {
                PayloadId = prepared.Payloads[0].Id,
                Outcome = ProcurementGhanepsAttemptOutcome.Succeeded,
                TransportReference = "GH-SOD-001",
                EvidenceReference = "ops://sod/send",
                IdempotencyKey = "sod-send",
                ExpectedRowVersion = prepared.RowVersion
            }, "sod-send");

        var action = () => fixture.Service.RecordAcknowledgementAsync(prepared.Id,
            fixture.Acknowledgement(ProcurementGhanepsAcknowledgementOutcome.Accepted,
                "GH-SOD-ACK", "sod-ack", sent.RowVersion), "sod-ack");

        await action.Should().ThrowAsync<ProcurementGhanepsExchangeAuthorizationException>();
        fixture.Context.Set<ProcurementGhanepsExchangeAcknowledgement>().Should().BeEmpty();
        fixture.SodRequests.Should().ContainSingle(request =>
            request.ProhibitedActorUserIds.Contains(fixture.CurrentActorId));
    }

    [Fact]
    public async Task MismatchRequiresIndependentResolutionAndTerminalOutcomeCannotReopen()
    {
        await using var fixture = new Fixture();
        var prepared = await fixture.PrepareAsync("mismatch-export", "REF",
            ProcurementGhanepsEventFamily.TenderReference);
        var sent = await fixture.Service.RecordAttemptAsync(prepared.Id,
            new RecordProcurementGhanepsAttemptRequest
            {
                PayloadId = prepared.Payloads[0].Id,
                Outcome = ProcurementGhanepsAttemptOutcome.Succeeded,
                TransportReference = "GH-REF-001",
                EvidenceReference = "ops://reference/send",
                IdempotencyKey = "reference-send",
                ExpectedRowVersion = prepared.RowVersion
            }, "reference-send");
        fixture.SwitchActor(Guid.NewGuid());
        var acknowledged = await fixture.Service.RecordAcknowledgementAsync(prepared.Id,
            fixture.Acknowledgement(ProcurementGhanepsAcknowledgementOutcome.Accepted,
                "GH-REF-ACK", "reference-ack", sent.RowVersion), "reference-ack");
        var mismatchActor = Guid.NewGuid();
        fixture.SwitchActor(mismatchActor);
        var mismatch = await fixture.Service.ReconcileAsync(prepared.Id,
            new ReconcileProcurementGhanepsExchangeRequest
            {
                ActualReference = "DIFFERENT-REFERENCE",
                ActualChecksumSha256 = new string('A', 64),
                EvidenceReference = "reconciliation://mismatch",
                IdempotencyKey = "mismatch",
                ExpectedRowVersion = acknowledged.RowVersion
            }, "mismatch");

        fixture.SwitchActor(Guid.NewGuid());
        var bypass = () => fixture.Service.ReconcileAsync(prepared.Id,
            new ReconcileProcurementGhanepsExchangeRequest
            {
                ActualReference = fixture.Tender.TenderNumber,
                ActualChecksumSha256 = mismatch.Payloads[0].PayloadChecksumSha256,
                EvidenceReference = "reconciliation://bypass",
                IdempotencyKey = "mismatch-bypass",
                ExpectedRowVersion = mismatch.RowVersion
            }, "mismatch-bypass");
        await bypass.Should().ThrowAsync<ProcurementGhanepsExchangeConflictException>()
            .Where(exception => exception.Code == "GHANEPS_RECONCILIATION_RESOLUTION_REQUIRED");

        var resolved = await fixture.Service.ReconcileAsync(prepared.Id,
            new ReconcileProcurementGhanepsExchangeRequest
            {
                ResolveExistingMismatch = true,
                ActualReference = "RESOLUTION-REFERENCE",
                ActualChecksumSha256 = new string('B', 64),
                EvidenceReference = "reconciliation://resolved",
                IdempotencyKey = "mismatch-resolved",
                ExpectedRowVersion = mismatch.RowVersion
            }, "mismatch-resolved");
        resolved.Reconciliations.Last().Outcome.Should()
            .Be(ProcurementGhanepsReconciliationOutcome.Resolved);

        var reopen = () => fixture.Service.ReconcileAsync(prepared.Id,
            new ReconcileProcurementGhanepsExchangeRequest
            {
                ActualReference = fixture.Tender.TenderNumber,
                ActualChecksumSha256 = resolved.Payloads[0].PayloadChecksumSha256,
                EvidenceReference = "reconciliation://reopen",
                IdempotencyKey = "reopen",
                ExpectedRowVersion = resolved.RowVersion
            }, "reopen");
        await reopen.Should().ThrowAsync<ProcurementGhanepsExchangeConflictException>()
            .Where(exception => exception.Code == "GHANEPS_RECONCILIATION_TERMINAL");
    }

    [Fact]
    public async Task NewerBlockedReadinessMakesAwardNotificationFailClosed()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAwardLineageAsync(blockLatest: true);

        var action = () => fixture.PrepareAsync("award-export", "AWARD",
            ProcurementGhanepsEventFamily.AwardNotification);

        await action.Should().ThrowAsync<ProcurementGhanepsExchangeConflictException>()
            .Where(exception => exception.Code == "GHANEPS_AWARD_READINESS_STALE");
        fixture.Context.Set<ProcurementGhanepsExchangeEvent>().Should().BeEmpty();
    }

    [Theory]
    [InlineData("rfq", ProcurementGhanepsSourceType.RequestForQuotation, "RequestForQuotation")]
    [InlineData("exceptional", ProcurementGhanepsSourceType.ExceptionalSourcing, "ExceptionalSourcing")]
    public async Task EveryControlledSourceFamilyResolvesWithExactServerVariant(
        string kind,
        ProcurementGhanepsSourceType expectedType,
        string expectedVariant)
    {
        await using var fixture = new Fixture();
        var source = await fixture.SeedAdditionalSourceAsync(kind);

        var prepared = await fixture.Service.PrepareExportAsync(
            new PrepareProcurementGhanepsExportRequest
            {
                SourceType = source.Type,
                SourceId = source.Id,
                EventFamily = ProcurementGhanepsEventFamily.TenderReference,
                MappingKey = source.MappingKey,
                EventReference = source.Reference,
                PayloadContent = """{"reference":"configured"}""",
                EvidenceReference = $"evidence://{kind}",
                IdempotencyKey = $"source-{kind}"
            }, $"source-{kind}");

        prepared.SourceType.Should().Be(expectedType);
        prepared.SourceVariant.Should().Be(expectedVariant);
        prepared.SourceReference.Should().Be(source.Reference);
    }

    [Fact]
    public async Task ExceptionalSourceCannotBeAddressedAsOrdinaryTender()
    {
        await using var fixture = new Fixture();
        var source = await fixture.SeedAdditionalSourceAsync("exceptional");

        var action = () => fixture.Service.GetOverviewAsync(
            ProcurementGhanepsSourceType.Tender, source.Id);

        await action.Should().ThrowAsync<ProcurementGhanepsExchangeValidationException>()
            .Where(exception => exception.Code == "GHANEPS_SOURCE_TYPE_MISMATCH");
    }

    [Fact]
    public async Task ValidAwardNotificationBindsExactLatestReadyAndCommunicationLineage()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAwardLineageAsync(blockLatest: false);

        var prepared = await fixture.PrepareAsync("valid-award", "AWARD",
            ProcurementGhanepsEventFamily.AwardNotification);

        prepared.SourceVariant.Should().Be("FormalTender");
        prepared.EventFamily.Should().Be(ProcurementGhanepsEventFamily.AwardNotification);
        prepared.SourceIntegrityHash.Should().HaveLength(64);
        prepared.History.Should().ContainSingle(item => item.Kind == "Exchange");
    }

    [Fact]
    public async Task AwardComplianceFailsClosedUntilEveryConfiguredTerminalEvidenceExists()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAwardLineageAsync(blockLatest: false);
        var prepared = await fixture.PrepareAsync(
            "compliance-incomplete",
            "AWARD",
            ProcurementGhanepsEventFamily.AwardNotification);

        var result = await fixture.Service.GetAwardComplianceAsync(
            ProcurementGhanepsSourceType.Tender,
            fixture.Tender.Id);

        result.IsCompliant.Should().BeFalse();
        result.Code.Should().Be("PO_GHANEPS_EVIDENCE_INCOMPLETE");
        result.Mappings.Should().ContainSingle(item =>
            item.ExchangeEventId == prepared.Id &&
            !item.SuccessfulTransfer &&
            !item.AcceptedAcknowledgement &&
            !item.CompletedReconciliation);
    }

    [Fact]
    public async Task AwardCompliancePassesOnlyForCurrentSuccessfulAcknowledgedReconciledEvidence()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAwardLineageAsync(blockLatest: false);
        var prepared = await fixture.PrepareAsync(
            "compliance-complete",
            "AWARD",
            ProcurementGhanepsEventFamily.AwardNotification);
        var sent = await fixture.Service.RecordAttemptAsync(
            prepared.Id,
            new RecordProcurementGhanepsAttemptRequest
            {
                PayloadId = prepared.Payloads[0].Id,
                Outcome = ProcurementGhanepsAttemptOutcome.Succeeded,
                TransportReference = "GH-COMPLIANCE-001",
                EvidenceReference = "evidence://compliance/transfer",
                IdempotencyKey = "compliance-transfer",
                ExpectedRowVersion = prepared.RowVersion
            },
            "compliance-transfer");
        fixture.SwitchActor(Guid.NewGuid());
        var accepted = await fixture.Service.RecordAcknowledgementAsync(
            prepared.Id,
            fixture.Acknowledgement(
                ProcurementGhanepsAcknowledgementOutcome.Accepted,
                "GH-COMPLIANCE-ACK",
                "compliance-ack",
                sent.RowVersion),
            "compliance-ack");
        fixture.SwitchActor(Guid.NewGuid());
        var payload = accepted.Payloads.Single(item =>
            item.Id == accepted.Acknowledgements.Single().PayloadId);
        _ = await fixture.Service.ReconcileAsync(
            prepared.Id,
            new ReconcileProcurementGhanepsExchangeRequest
            {
                ActualReference = fixture.Tender.TenderNumber,
                ActualChecksumSha256 = payload.PayloadChecksumSha256,
                EvidenceReference = "evidence://compliance/reconciliation",
                IdempotencyKey = "compliance-reconciliation",
                ExpectedRowVersion = accepted.RowVersion
            },
            "compliance-reconciliation");
        await fixture.PublishReplacementProfileWithSameMappingAsync();

        var result = await fixture.Service.GetAwardComplianceAsync(
            ProcurementGhanepsSourceType.Tender,
            fixture.Tender.Id);

        result.IsCompliant.Should().BeTrue();
        result.Code.Should().Be("PO_GHANEPS_EVIDENCE_CURRENT");
        result.Mappings.Should().ContainSingle(item =>
            item.SuccessfulTransfer &&
            item.AcceptedAcknowledgement &&
            item.CompletedReconciliation &&
            item.EvidenceAvailable);
    }

    [Theory]
    [InlineData("rfq", ProcurementGhanepsSourceType.RequestForQuotation,
        "RequestForQuotation")]
    [InlineData("exceptional", ProcurementGhanepsSourceType.ExceptionalSourcing,
        "ExceptionalSourcing")]
    public async Task RfqAndExceptionalAwardNotificationsRequireExactReadyCommunicationLineage(
        string kind,
        ProcurementGhanepsSourceType expectedType,
        string expectedVariant)
    {
        await using var fixture = new Fixture();
        var source = await fixture.SeedAdditionalAwardSourceAsync(kind);

        var prepared = await fixture.Service.PrepareExportAsync(
            new PrepareProcurementGhanepsExportRequest
            {
                SourceType = source.Type,
                SourceId = source.Id,
                EventFamily = ProcurementGhanepsEventFamily.AwardNotification,
                MappingKey = source.MappingKey,
                EventReference = source.Reference,
                PayloadContent = """{"award":"controlled"}""",
                EvidenceReference = $"evidence://award/{kind}",
                IdempotencyKey = $"award-{kind}"
            }, $"award-{kind}");

        prepared.SourceType.Should().Be(expectedType);
        prepared.SourceVariant.Should().Be(expectedVariant);
        prepared.EventFamily.Should().Be(ProcurementGhanepsEventFamily.AwardNotification);
        prepared.SourceIntegrityHash.Should().HaveLength(64);
    }

    [Fact]
    public async Task SameIdempotencyKeyWithDifferentPayloadConflicts()
    {
        await using var fixture = new Fixture();
        _ = await fixture.PrepareAsync("same-key", "PUB");

        var action = () => fixture.Service.PrepareExportAsync(
            new PrepareProcurementGhanepsExportRequest
            {
                SourceType = ProcurementGhanepsSourceType.Tender,
                SourceId = fixture.Tender.Id,
                EventFamily = ProcurementGhanepsEventFamily.TenderPublication,
                MappingKey = "PUB",
                EventReference = fixture.Tender.TenderNumber,
                PayloadContent = """{"notice":"different"}""",
                EvidenceReference = "evidence://different",
                IdempotencyKey = "same-key"
            }, "same-key");

        await action.Should().ThrowAsync<ProcurementGhanepsExchangeConflictException>()
            .Where(exception => exception.Code == "GHANEPS_IDEMPOTENCY_CONFLICT");
        (await fixture.Context.Set<ProcurementGhanepsExchangeEvent>().CountAsync())
            .Should().Be(1);
    }

    [Fact]
    public async Task RouteBoundCreateMismatchIsDeniedAuditedAndDoesNotMutate()
    {
        await using var fixture = new Fixture();

        var action = () => fixture.Service.PrepareExportAsync(
            new PrepareProcurementGhanepsExportRequest
            {
                RouteSourceType = ProcurementGhanepsSourceType.RequestForQuotation,
                RouteSourceId = fixture.Tender.Id,
                SourceType = ProcurementGhanepsSourceType.Tender,
                SourceId = fixture.Tender.Id,
                EventFamily = ProcurementGhanepsEventFamily.TenderPublication,
                MappingKey = "PUB",
                EventReference = fixture.Tender.TenderNumber,
                PayloadContent = """{"notice":"route mismatch"}""",
                EvidenceReference = "evidence://route/create-mismatch",
                IdempotencyKey = "route-create-mismatch"
            }, "route-create-mismatch");

        await action.Should()
            .ThrowAsync<ProcurementGhanepsExchangeValidationException>()
            .Where(exception =>
                exception.Code == "GHANEPS_EXCHANGE_SOURCE_ROUTE_MISMATCH");
        fixture.Context.Set<ProcurementGhanepsExchangeEvent>().Should().BeEmpty();
        fixture.ControlEvents.Should().ContainSingle(item =>
            item.Action == "PrepareExportDenied" &&
            item.Result == ProcurementControlEventResult.Denied);
    }

    [Fact]
    public async Task RouteBoundChildMismatchIsDeniedAuditedAndDoesNotMutate()
    {
        await using var fixture = new Fixture();
        var prepared = await fixture.PrepareAsync("route-child-event", "PUB");
        fixture.ControlEvents.Clear();

        var action = () => fixture.Service.RecordAttemptAsync(
            prepared.Id,
            new RecordProcurementGhanepsAttemptRequest
            {
                RouteSourceType = ProcurementGhanepsSourceType.Tender,
                RouteSourceId = Guid.NewGuid(),
                PayloadId = prepared.Payloads.Single().Id,
                Outcome = ProcurementGhanepsAttemptOutcome.Succeeded,
                TransportReference = "GH-ROUTE-CHILD",
                EvidenceReference = "evidence://route/child-mismatch",
                IdempotencyKey = "route-child-mismatch",
                ExpectedRowVersion = prepared.RowVersion
            },
            "route-child-mismatch");

        await action.Should()
            .ThrowAsync<ProcurementGhanepsExchangeNotFoundException>()
            .Where(exception =>
                exception.Code ==
                "GHANEPS_EXCHANGE_ROUTE_RESOURCE_NOT_FOUND");
        fixture.Context.Set<ProcurementGhanepsExchangeAttempt>().Should()
            .BeEmpty();
        fixture.ControlEvents.Should().ContainSingle(item =>
            item.Action == "RecordAttemptDenied" &&
            item.Result == ProcurementControlEventResult.Denied);
    }

    [Fact]
    public async Task StaleRowVersionFailsBeforeAttemptWrite()
    {
        await using var fixture = new Fixture();
        var prepared = await fixture.PrepareAsync("stale-row", "PUB");

        var action = () => fixture.Service.RecordAttemptAsync(prepared.Id,
            new RecordProcurementGhanepsAttemptRequest
            {
                PayloadId = prepared.Payloads[0].Id,
                Outcome = ProcurementGhanepsAttemptOutcome.Succeeded,
                TransportReference = "GH-STALE-001",
                EvidenceReference = "evidence://stale",
                IdempotencyKey = "stale-attempt",
                ExpectedRowVersion = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            }, "stale-attempt");

        await action.Should().ThrowAsync<ProcurementGhanepsExchangeConflictException>()
            .Where(exception => exception.Code == "GHANEPS_EXCHANGE_CONCURRENCY_CONFLICT");
        fixture.Context.Set<ProcurementGhanepsExchangeAttempt>().Should().BeEmpty();
    }

    [Fact]
    public async Task ChildMutationReplaysIgnoreStaleRowVersionButRejectChangedInputs()
    {
        await using var fixture = new Fixture();
        var prepared = await fixture.PrepareAsync("replay-chain", "PUB");
        var attemptRequest = new RecordProcurementGhanepsAttemptRequest
        {
            PayloadId = prepared.Payloads[0].Id,
            Outcome = ProcurementGhanepsAttemptOutcome.Failed,
            FailureCode = "NETWORK",
            FailureMessage = "Unavailable",
            EvidenceReference = "evidence://attempt",
            IdempotencyKey = "replay-attempt",
            ExpectedRowVersion = prepared.RowVersion
        };
        var failed = await fixture.Service.RecordAttemptAsync(prepared.Id, attemptRequest,
            "replay-attempt");
        var afterAttemptAdvance = await fixture.AdvanceRowVersionAsync(prepared.Id);
        (await fixture.Service.RecordAttemptAsync(prepared.Id, attemptRequest,
            "replay-attempt")).Id.Should().Be(prepared.Id);
        var changedAttempt = () => fixture.Service.RecordAttemptAsync(prepared.Id,
            new RecordProcurementGhanepsAttemptRequest
            {
                PayloadId = attemptRequest.PayloadId,
                Outcome = attemptRequest.Outcome,
                FailureCode = attemptRequest.FailureCode,
                FailureMessage = "Changed",
                EvidenceReference = attemptRequest.EvidenceReference,
                IdempotencyKey = attemptRequest.IdempotencyKey,
                ExpectedRowVersion = prepared.RowVersion
            }, "replay-attempt-changed");
        await changedAttempt.Should().ThrowAsync<ProcurementGhanepsExchangeConflictException>()
            .Where(exception => exception.Code == "GHANEPS_IDEMPOTENCY_CONFLICT");

        var retryRequest = new RetryProcurementGhanepsExchangeRequest
        {
            Outcome = ProcurementGhanepsAttemptOutcome.Succeeded,
            TransportReference = "GH-RETRY-001",
            ReplacementPayloadContent = """{"notice":"corrected"}""",
            FileName = "corrected.json",
            EvidenceReference = "evidence://retry",
            IdempotencyKey = "replay-retry",
            ExpectedRowVersion = afterAttemptAdvance.RowVersion
        };
        var retried = await fixture.Service.RetryAsync(prepared.Id, retryRequest,
            "replay-retry");
        var afterRetryAdvance = await fixture.AdvanceRowVersionAsync(prepared.Id);
        (await fixture.Service.RetryAsync(prepared.Id, retryRequest, "replay-retry"))
            .Attempts.Should().HaveCount(2);
        var changedRetry = () => fixture.Service.RetryAsync(prepared.Id,
            new RetryProcurementGhanepsExchangeRequest
            {
                Outcome = retryRequest.Outcome,
                TransportReference = "GH-RETRY-CHANGED",
                ReplacementPayloadContent = retryRequest.ReplacementPayloadContent,
                FileName = retryRequest.FileName,
                EvidenceReference = retryRequest.EvidenceReference,
                IdempotencyKey = retryRequest.IdempotencyKey,
                ExpectedRowVersion = retried.RowVersion
            }, "replay-retry-changed");
        await changedRetry.Should().ThrowAsync<ProcurementGhanepsExchangeConflictException>()
            .Where(exception => exception.Code == "GHANEPS_IDEMPOTENCY_CONFLICT");

        fixture.SwitchActor(Guid.NewGuid());
        var acknowledgementRequest = fixture.Acknowledgement(
            ProcurementGhanepsAcknowledgementOutcome.Accepted,
            "GH-ACK-REPLAY", "replay-ack", afterRetryAdvance.RowVersion);
        var acknowledged = await fixture.Service.RecordAcknowledgementAsync(prepared.Id,
            acknowledgementRequest, "replay-ack");
        var afterAcknowledgementAdvance = await fixture.AdvanceRowVersionAsync(prepared.Id);
        (await fixture.Service.RecordAcknowledgementAsync(prepared.Id,
            acknowledgementRequest, "replay-ack")).Acknowledgements.Should().ContainSingle();
        var changedAcknowledgement = () =>
            fixture.Service.RecordAcknowledgementAsync(prepared.Id,
                fixture.Acknowledgement(
                    ProcurementGhanepsAcknowledgementOutcome.Accepted,
                    "GH-ACK-CHANGED", "replay-ack", acknowledged.RowVersion),
                "replay-ack-changed");
        await changedAcknowledgement.Should()
            .ThrowAsync<ProcurementGhanepsExchangeConflictException>()
            .Where(exception => exception.Code == "GHANEPS_IDEMPOTENCY_CONFLICT");

        fixture.SwitchActor(Guid.NewGuid());
        var reconciliationRequest = new ReconcileProcurementGhanepsExchangeRequest
        {
            ActualReference = fixture.Tender.TenderNumber,
            ActualChecksumSha256 = acknowledged.Payloads.Last().PayloadChecksumSha256,
            EvidenceReference = "evidence://reconciliation",
            IdempotencyKey = "replay-reconciliation",
            ExpectedRowVersion = afterAcknowledgementAdvance.RowVersion
        };
        var reconciled = await fixture.Service.ReconcileAsync(prepared.Id,
            reconciliationRequest, "replay-reconciliation");
        _ = await fixture.AdvanceRowVersionAsync(prepared.Id);
        (await fixture.Service.ReconcileAsync(prepared.Id, reconciliationRequest,
            "replay-reconciliation")).Reconciliations.Should().ContainSingle();
        var changedReconciliation = () => fixture.Service.ReconcileAsync(prepared.Id,
            new ReconcileProcurementGhanepsExchangeRequest
            {
                ActualReference = "CHANGED",
                ActualChecksumSha256 = reconciliationRequest.ActualChecksumSha256,
                EvidenceReference = reconciliationRequest.EvidenceReference,
                IdempotencyKey = reconciliationRequest.IdempotencyKey,
                ExpectedRowVersion = reconciled.RowVersion
            }, "replay-reconciliation-changed");
        await changedReconciliation.Should()
            .ThrowAsync<ProcurementGhanepsExchangeConflictException>()
            .Where(exception => exception.Code == "GHANEPS_IDEMPOTENCY_CONFLICT");

        fixture.Context.Set<ProcurementGhanepsExchangeAttempt>().Should().HaveCount(2);
        fixture.Context.Set<ProcurementGhanepsExchangeAcknowledgement>().Should().ContainSingle();
        fixture.Context.Set<ProcurementGhanepsExchangeReconciliation>().Should().ContainSingle();
    }

    [Fact]
    public async Task RepeatedConcurrencyDenialsAreIndependentlyAuditedWithoutBusinessWrites()
    {
        await using var fixture = new Fixture();
        var prepared = await fixture.PrepareAsync("repeat-denial", "PUB");
        var request = new RecordProcurementGhanepsAttemptRequest
        {
            PayloadId = prepared.Payloads[0].Id,
            Outcome = ProcurementGhanepsAttemptOutcome.Succeeded,
            TransportReference = "GH-DENIED",
            EvidenceReference = "evidence://denied",
            IdempotencyKey = "repeat-denied-attempt",
            ExpectedRowVersion = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
        };

        for (var iteration = 0; iteration < 2; iteration++)
        {
            var action = () => fixture.Service.RecordAttemptAsync(prepared.Id, request,
                "same-denial-correlation");
            await action.Should().ThrowAsync<ProcurementGhanepsExchangeConflictException>()
                .Where(exception => exception.Code ==
                    "GHANEPS_EXCHANGE_CONCURRENCY_CONFLICT");
        }

        fixture.Context.Set<ProcurementGhanepsExchangeAttempt>().Should().BeEmpty();
        fixture.ControlEvents.Where(item => item.Action == "RecordAttemptDenied")
            .Should().HaveCount(2)
            .And.OnlyContain(item =>
                item.Result == ProcurementControlEventResult.Denied &&
                item.RuleId == fixture.Decision.Id);
        fixture.ControlEvents.Where(item => item.Action == "RecordAttemptDenied")
            .Select(item => item.EventKey).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task MalformedOverlongInputPreservesValidationCodeAndWritesBoundedDenialAudit()
    {
        await using var fixture = new Fixture();
        var action = () => fixture.Service.PrepareExportAsync(
            new PrepareProcurementGhanepsExportRequest
            {
                SourceType = ProcurementGhanepsSourceType.Tender,
                SourceId = fixture.Tender.Id,
                EventFamily = ProcurementGhanepsEventFamily.TenderPublication,
                MappingKey = "PUB",
                EventReference = fixture.Tender.TenderNumber,
                PayloadContent = "{}",
                EvidenceReference = new string('E', 501),
                IdempotencyKey = "overlong-evidence"
            }, new string('C', 101));

        await action.Should().ThrowAsync<ProcurementGhanepsExchangeValidationException>()
            .Where(exception => exception.Code == "GHANEPS_EVIDENCE_REFERENCE_TOO_LONG");
        fixture.Context.Set<ProcurementGhanepsExchangeEvent>().Should().BeEmpty();
        fixture.ControlEvents.Should().ContainSingle(item =>
            item.Action == "PrepareExportDenied" &&
            item.Result == ProcurementControlEventResult.Denied &&
            item.CorrelationId.Length == 100 &&
            item.Evidence.Single().Reference!.Length == 200);
    }

    [Fact]
    public async Task ExternalAndCapabilityDeniedActorsCannotCreateExchangeAndAreAudited()
    {
        await using var externalFixture = new Fixture();
        externalFixture.SetExternalUser();
        var externalAction = () => externalFixture.PrepareAsync(
            "external-denied", "PUB");
        await externalAction.Should()
            .ThrowAsync<ProcurementGhanepsExchangeAuthorizationException>();
        externalFixture.Context.Set<ProcurementGhanepsExchangeEvent>().Should().BeEmpty();
        externalFixture.ControlEvents.Should().ContainSingle(item =>
            item.Action == "PrepareExportDenied");

        await using var capabilityFixture = new Fixture();
        capabilityFixture.SetRoles("TDC_PROCUREMENT_OFFICER");
        capabilityFixture.DenyCapabilities();
        var capabilityAction = () => capabilityFixture.PrepareAsync(
            "capability-denied", "PUB");
        await capabilityAction.Should()
            .ThrowAsync<ProcurementGhanepsExchangeAuthorizationException>();
        capabilityFixture.Context.Set<ProcurementGhanepsExchangeEvent>().Should().BeEmpty();
        capabilityFixture.ControlEvents.Should().ContainSingle(item =>
            item.Action == "PrepareExportDenied" &&
            item.Result == ProcurementControlEventResult.Denied);
    }

    [Fact]
    public async Task CrossTenantChildMutationIsDeniedWithoutChildWriteAndIsAudited()
    {
        await using var fixture = new Fixture();
        var prepared = await fixture.PrepareAsync("cross-tenant-mutation", "PUB");
        fixture.SwitchTenant(Guid.NewGuid());

        var action = () => fixture.Service.RecordAttemptAsync(prepared.Id,
            new RecordProcurementGhanepsAttemptRequest
            {
                PayloadId = prepared.Payloads[0].Id,
                Outcome = ProcurementGhanepsAttemptOutcome.Succeeded,
                TransportReference = "GH-CROSS-TENANT",
                EvidenceReference = "evidence://cross-tenant",
                IdempotencyKey = "cross-tenant-attempt",
                ExpectedRowVersion = prepared.RowVersion
            }, "cross-tenant-attempt");

        await action.Should().ThrowAsync<ProcurementGhanepsExchangeNotFoundException>()
            .Where(exception => exception.Code == "GHANEPS_EXCHANGE_NOT_FOUND");
        fixture.Context.Set<ProcurementGhanepsExchangeAttempt>().Should().BeEmpty();
        fixture.ControlEvents.Should().ContainSingle(item =>
            item.Action == "RecordAttemptDenied" &&
            item.SourceId == prepared.Id &&
            item.RuleId == null);
    }

    [Fact]
    public async Task RetryLimitIsEnforcedFromImmutableMappingAndAuditedWithoutRetryWrite()
    {
        await using var fixture = new Fixture();
        await fixture.ConfigureMappingAsync("PUB",
            mapping => mapping.MaximumRetryAttempts = 0);
        var prepared = await fixture.PrepareAsync("retry-limit", "PUB");
        var failed = await fixture.Service.RecordAttemptAsync(prepared.Id,
            new RecordProcurementGhanepsAttemptRequest
            {
                PayloadId = prepared.Payloads[0].Id,
                Outcome = ProcurementGhanepsAttemptOutcome.Failed,
                FailureCode = "NETWORK",
                FailureMessage = "Unavailable",
                EvidenceReference = "evidence://retry-limit/failure",
                IdempotencyKey = "retry-limit-failure",
                ExpectedRowVersion = prepared.RowVersion
            }, "retry-limit-failure");

        var action = () => fixture.Service.RetryAsync(prepared.Id,
            new RetryProcurementGhanepsExchangeRequest
            {
                Outcome = ProcurementGhanepsAttemptOutcome.Succeeded,
                TransportReference = "GH-RETRY-DENIED",
                EvidenceReference = "evidence://retry-limit/denied",
                IdempotencyKey = "retry-limit-denied",
                ExpectedRowVersion = failed.RowVersion
            }, "retry-limit-denied");

        await action.Should().ThrowAsync<ProcurementGhanepsExchangeConflictException>()
            .Where(exception => exception.Code == "GHANEPS_RETRY_LIMIT_EXHAUSTED");
        fixture.Context.Set<ProcurementGhanepsExchangeAttempt>().Should().ContainSingle();
        fixture.ControlEvents.Should().ContainSingle(item =>
            item.Action == "RetryDenied" &&
            item.Result == ProcurementControlEventResult.Denied);
    }

    [Fact]
    public async Task ConfiguredCsvRepresentationIsNormalizedChecksummedAndExposed()
    {
        await using var fixture = new Fixture();
        await fixture.ConfigureMappingAsync("PUB", mapping =>
        {
            mapping.PayloadContentType = "text/csv";
            mapping.AcknowledgementContentType = "text/plain";
        });

        var prepared = await fixture.Service.PrepareExportAsync(
            new PrepareProcurementGhanepsExportRequest
            {
                SourceType = ProcurementGhanepsSourceType.Tender,
                SourceId = fixture.Tender.Id,
                EventFamily = ProcurementGhanepsEventFamily.TenderPublication,
                MappingKey = "PUB",
                EventReference = fixture.Tender.TenderNumber,
                PayloadContent = "reference,value\r\nTDC-TND-001,10\r\n",
                FileName = "publication.csv",
                EvidenceReference = "evidence://csv",
                IdempotencyKey = "csv-export"
            }, "csv-export");

        prepared.PayloadContentType.Should().Be("text/csv");
        prepared.AcknowledgementContentType.Should().Be("text/plain");
        prepared.Payloads.Should().ContainSingle(item =>
            item.ContentType == "text/csv" &&
            item.PayloadContent == "reference,value\nTDC-TND-001,10\n" &&
            item.PayloadChecksumSha256.Length == 64);
        var options = await fixture.Service.GetOptionsAsync(
            ProcurementGhanepsSourceType.Tender, fixture.Tender.Id);
        options.Mappings.Single(item => item.MappingKey == "PUB")
            .PayloadContentType.Should().Be("text/csv");
    }

    [Theory]
    [InlineData("application/json", "reference,value", "GHANEPS_PAYLOAD_CONTENT_INVALID")]
    [InlineData("application/octet-stream", "binary", "GHANEPS_MAPPING_PAYLOAD_CONTENT_TYPE_INVALID")]
    public async Task ConfiguredRepresentationMismatchOrUnsupportedTypeFailsClosed(
        string contentType,
        string content,
        string expectedCode)
    {
        await using var fixture = new Fixture();
        await fixture.ConfigureMappingAsync("PUB",
            mapping => mapping.PayloadContentType = contentType);

        var action = () => fixture.Service.PrepareExportAsync(
            new PrepareProcurementGhanepsExportRequest
            {
                SourceType = ProcurementGhanepsSourceType.Tender,
                SourceId = fixture.Tender.Id,
                EventFamily = ProcurementGhanepsEventFamily.TenderPublication,
                MappingKey = "PUB",
                EventReference = fixture.Tender.TenderNumber,
                PayloadContent = content,
                EvidenceReference = "evidence://format-denied",
                IdempotencyKey = $"format-{contentType}"
            }, $"format-{contentType}");

        await action.Should().ThrowAsync<ProcurementGhanepsExchangeValidationException>()
            .Where(exception => exception.Code == expectedCode);
        fixture.Context.Set<ProcurementGhanepsExchangeEvent>().Should().BeEmpty();
        fixture.ControlEvents.Should().ContainSingle(item =>
            item.Action == "PrepareExportDenied" &&
            item.Result == ProcurementControlEventResult.Denied);
    }

    [Theory]
    [InlineData("null", "GHANEPS_MAPPING_COLLECTION_REQUIRED")]
    [InlineData("[null]", "GHANEPS_MAPPING_ENTRY_REQUIRED")]
    public async Task NullMappingCollectionShapesReturnStableAuditedValidation(
        string mappingsJson,
        string expectedCode)
    {
        await using var fixture = new Fixture();
        fixture.Decision.ValueJson =
            $$"""
              {
                "effectiveFrom": "{{DateTime.UtcNow.AddDays(-30):O}}",
                "profileCode": "TDC-GHANEPS",
                "fileTemplateMappings": {{mappingsJson}},
                "frequency": "Configured",
                "owner": "Procurement ICT/PPA",
                "acknowledgementRule": "Configured",
                "reconciliationRule": "Configured"
              }
              """;
        fixture.Context.Update(fixture.Decision);
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.PrepareAsync($"null-shape-{expectedCode}", "PUB");

        await action.Should().ThrowAsync<ProcurementGhanepsExchangeValidationException>()
            .Where(exception => exception.Code == expectedCode);
        fixture.Context.Set<ProcurementGhanepsExchangeEvent>().Should().BeEmpty();
        fixture.ControlEvents.Should().ContainSingle(item =>
            item.Action == "PrepareExportDenied");
    }

    [Fact]
    public async Task NestedDuplicateJsonPropertyIsRejectedBeforeChecksumAndAudited()
    {
        await using var fixture = new Fixture();

        var action = () => fixture.Service.PrepareExportAsync(
            new PrepareProcurementGhanepsExportRequest
            {
                SourceType = ProcurementGhanepsSourceType.Tender,
                SourceId = fixture.Tender.Id,
                EventFamily = ProcurementGhanepsEventFamily.TenderPublication,
                MappingKey = "PUB",
                EventReference = fixture.Tender.TenderNumber,
                PayloadContent = """{"outer":{"reference":"one","reference":"two"}}""",
                EvidenceReference = "evidence://duplicate-json",
                IdempotencyKey = "duplicate-json"
            }, "duplicate-json");

        await action.Should().ThrowAsync<ProcurementGhanepsExchangeValidationException>()
            .Where(exception => exception.Code ==
                "GHANEPS_PAYLOAD_CONTENT_DUPLICATE_PROPERTY");
        fixture.Context.Set<ProcurementGhanepsExchangeEvent>().Should().BeEmpty();
        fixture.ControlEvents.Should().ContainSingle(item =>
            item.Action == "PrepareExportDenied");
    }

    [Fact]
    public async Task NestedDuplicateDec009PropertyIsRejectedBeforeDecisionHash()
    {
        await using var fixture = new Fixture();
        var value = JsonSerializer.Deserialize<JsonElement>(fixture.Decision.ValueJson);
        fixture.Decision.ValueJson =
            $$"""
              {
                "effectiveFrom": "{{DateTime.UtcNow.AddDays(-30):O}}",
                "profileCode": "TDC-GHANEPS",
                "fileTemplateMappings": {{value.GetProperty("FileTemplateMappings").GetRawText()}},
                "frequency": "Configured",
                "owner": "Procurement ICT/PPA",
                "acknowledgementRule": "Configured",
                "reconciliationRule": "Configured",
                "metadata": {"owner":"one","owner":"two"}
              }
              """;
        fixture.Context.Update(fixture.Decision);
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.PrepareAsync("duplicate-dec009", "PUB");

        await action.Should().ThrowAsync<ProcurementGhanepsExchangeValidationException>()
            .Where(exception => exception.Code ==
                "GHANEPS_PROFILE_VALUE_DUPLICATE_PROPERTY");
        fixture.Context.Set<ProcurementGhanepsExchangeEvent>().Should().BeEmpty();
        fixture.ControlEvents.Should().ContainSingle(item =>
            item.Action == "PrepareExportDenied");
    }

    [Fact]
    public async Task CreateReplayUsesRetainedFingerprintAfterProfileAndReadinessDrift()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAwardLineageAsync(blockLatest: false);
        var first = await fixture.PrepareAsync("drift-replay", "AWARD",
            ProcurementGhanepsEventFamily.AwardNotification);
        fixture.Profile.LifecycleStatus = ProcurementConfigurationProfileStatus.Retired;
        fixture.Context.Update(fixture.Profile);
        fixture.Context.Add(new ProcurementAwardReadinessDecision
        {
            TenantId = fixture.TenantId,
            SourceType = ProcurementAwardReadinessSourceType.Tender,
            SourceId = fixture.Tender.Id,
            SourceReference = fixture.Tender.TenderNumber,
            Method = ProcurementMethodType.NationalCompetitiveTendering,
            DecisionSequence = 2,
            Status = ProcurementAwardReadinessDecisionStatus.Blocked,
            RecommendationSubjectType = "TenderBid",
            SourceIntegrityHash = new string('E', 64),
            IntegrityHash = new string('F', 64),
            IdempotencyKey = "drift-blocked",
            CorrelationId = "drift-blocked",
            EvaluatedAtUtc = DateTime.UtcNow,
            EvaluatedByUserId = Guid.NewGuid(),
            EvaluatedByName = "Evaluator"
        });
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        var replay = await fixture.PrepareAsync("drift-replay", "AWARD",
            ProcurementGhanepsEventFamily.AwardNotification);

        replay.Id.Should().Be(first.Id);
        fixture.Context.Set<ProcurementGhanepsExchangeEvent>().Should().ContainSingle();
    }

    [Theory]
    [InlineData("rfq")]
    [InlineData("exceptional")]
    public async Task SameSourceStaleAwardCommunicationRegisterIsRejected(string kind)
    {
        await using var fixture = new Fixture();
        var source = await fixture.SeedAdditionalAwardSourceAsync(kind);
        var register = await fixture.Context
            .Set<ProcurementBidderCommunicationRegister>()
            .SingleAsync(item => item.SourceId == source.Id);
        register.AwardReference = "STALE-AWARD";
        fixture.Context.Update(register);
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.PrepareExportAsync(
            new PrepareProcurementGhanepsExportRequest
            {
                SourceType = source.Type,
                SourceId = source.Id,
                EventFamily = ProcurementGhanepsEventFamily.AwardNotification,
                MappingKey = source.MappingKey,
                EventReference = source.Reference,
                PayloadContent = """{"award":"controlled"}""",
                EvidenceReference = $"evidence://stale/{kind}",
                IdempotencyKey = $"stale-{kind}"
            }, $"stale-{kind}");

        await action.Should().ThrowAsync<ProcurementGhanepsExchangeConflictException>()
            .Where(exception => exception.Code == "GHANEPS_AWARD_LINEAGE_STALE");
        fixture.Context.Set<ProcurementGhanepsExchangeEvent>().Should().BeEmpty();
        fixture.ControlEvents.Should().ContainSingle(item =>
            item.Action == "PrepareExportDenied");
    }

    [Fact]
    public async Task LateUniqueRaceRecoversIdenticalCreateAndChildButRejectsChangedChild()
    {
        await using var fixture = new Fixture();
        var createRequest = new PrepareProcurementGhanepsExportRequest
        {
            SourceType = ProcurementGhanepsSourceType.Tender,
            SourceId = fixture.Tender.Id,
            EventFamily = ProcurementGhanepsEventFamily.TenderPublication,
            MappingKey = "PUB",
            EventReference = fixture.Tender.TenderNumber,
            PayloadContent = """{"notice":"race"}""",
            EvidenceReference = "evidence://race/create",
            IdempotencyKey = "race-create"
        };
        await using (var peerContext = fixture.CreatePeerContext())
        {
            using var peerUnit = new UnitOfWork(peerContext);
            var peerService = fixture.CreateService(peerUnit);
            using var faulting = new FaultingUnitOfWork(
                new UnitOfWork(fixture.Context),
                async () => _ = await peerService.PrepareExportAsync(
                    createRequest, "race-create-winner"));
            var loserService = fixture.CreateService(faulting);

            var recovered = await loserService.PrepareExportAsync(
                createRequest, "race-create-loser");

            recovered.Id.Should().NotBeEmpty();
        }
        fixture.Context.ChangeTracker.Clear();
        fixture.Context.Set<ProcurementGhanepsExchangeEvent>().Should().ContainSingle();
        fixture.ControlEvents.Count(item => item.Action == "ExportPrepared").Should().Be(1);
        fixture.Notifications.Count(item =>
            item.TopicKey == "procurement.ghaneps.exchange.export-prepared").Should().Be(1);

        var prepared = await fixture.PrepareAsync("race-child-event", "REF",
            ProcurementGhanepsEventFamily.TenderReference);
        var childRequest = new RecordProcurementGhanepsAttemptRequest
        {
            PayloadId = prepared.Payloads.Single().Id,
            Outcome = ProcurementGhanepsAttemptOutcome.Succeeded,
            TransportReference = "GH-RACE-CHILD",
            EvidenceReference = "evidence://race/child",
            IdempotencyKey = "race-child",
            ExpectedRowVersion = prepared.RowVersion
        };
        await using (var peerContext = fixture.CreatePeerContext())
        {
            using var peerUnit = new UnitOfWork(peerContext);
            var peerService = fixture.CreateService(peerUnit);
            using var faulting = new FaultingUnitOfWork(
                new UnitOfWork(fixture.Context),
                async () => _ = await peerService.RecordAttemptAsync(
                    prepared.Id, childRequest, "race-child-winner"));
            var loserService = fixture.CreateService(faulting);

            var recovered = await loserService.RecordAttemptAsync(
                prepared.Id, childRequest, "race-child-loser");

            recovered.Attempts.Should().ContainSingle();
        }
        fixture.Context.ChangeTracker.Clear();
        fixture.Context.Set<ProcurementGhanepsExchangeAttempt>().Should().ContainSingle();
        fixture.ControlEvents.Count(item => item.Action == "AttemptRecorded").Should().Be(1);
        fixture.Notifications.Count(item =>
            item.TopicKey == "procurement.ghaneps.exchange.attempt-succeeded").Should().Be(1);

        await fixture.AddConfiguredMappingAsync(fixture.Mapping("PUB-RACE",
            ProcurementGhanepsEventFamily.TenderPublication,
            ProcurementGhanepsExchangeDirection.Export,
            [ProcurementGhanepsSourceType.Tender]));
        var changedPrepared = await fixture.PrepareAsync(
            "race-changed-event", "PUB-RACE");
        var loserRequest = new RecordProcurementGhanepsAttemptRequest
        {
            PayloadId = changedPrepared.Payloads.Single().Id,
            Outcome = ProcurementGhanepsAttemptOutcome.Failed,
            FailureCode = "NETWORK",
            FailureMessage = "Loser input",
            EvidenceReference = "evidence://race/changed",
            IdempotencyKey = "race-changed",
            ExpectedRowVersion = changedPrepared.RowVersion
        };
        var winnerRequest = new RecordProcurementGhanepsAttemptRequest
        {
            PayloadId = loserRequest.PayloadId,
            Outcome = loserRequest.Outcome,
            FailureCode = loserRequest.FailureCode,
            FailureMessage = "Winner different input",
            EvidenceReference = loserRequest.EvidenceReference,
            IdempotencyKey = loserRequest.IdempotencyKey,
            ExpectedRowVersion = loserRequest.ExpectedRowVersion
        };
        await using (var peerContext = fixture.CreatePeerContext())
        {
            using var peerUnit = new UnitOfWork(peerContext);
            var peerService = fixture.CreateService(peerUnit);
            using var faulting = new FaultingUnitOfWork(
                new UnitOfWork(fixture.Context),
                async () => _ = await peerService.RecordAttemptAsync(
                    changedPrepared.Id, winnerRequest, "race-changed-winner"));
            var loserService = fixture.CreateService(faulting);

            var action = () => loserService.RecordAttemptAsync(
                changedPrepared.Id, loserRequest, "race-changed-loser");

            await action.Should().ThrowAsync<ProcurementGhanepsExchangeConflictException>()
                .Where(exception => exception.Code == "GHANEPS_IDEMPOTENCY_CONFLICT");
        }
        fixture.Context.ChangeTracker.Clear();
        fixture.Context.Set<ProcurementGhanepsExchangeAttempt>()
            .Count(item => item.ExchangeEventId == changedPrepared.Id).Should().Be(1);
        fixture.ControlEvents.Count(item =>
            item.Action == "RecordAttemptDenied").Should().Be(1);
    }

    [Fact]
    public async Task InboundEventWithoutAcknowledgementUsesTransferredState()
    {
        await using var fixture = new Fixture();

        var imported = await fixture.Service.RecordImportAsync(
            new RecordProcurementGhanepsImportRequest
            {
                SourceType = ProcurementGhanepsSourceType.Tender,
                SourceId = fixture.Tender.Id,
                EventFamily = ProcurementGhanepsEventFamily.TenderReference,
                MappingKey = "INBOUND",
                EventReference = fixture.Tender.TenderNumber,
                PayloadContent = """{"reference":"TDC-TND-001"}""",
                TransportReference = "GH-INBOUND-001",
                EvidenceReference = "inbound://evidence",
                IdempotencyKey = "inbound-1"
            }, "inbound-1");

        imported.Status.Should().Be(ProcurementGhanepsExchangeStatus.Transferred);
        imported.Attempts.Should().ContainSingle(item =>
            item.Outcome == ProcurementGhanepsAttemptOutcome.Succeeded);
        imported.AllowedActions.Should().NotContain("Reconcile");
        fixture.SwitchActor(Guid.NewGuid());
        (await fixture.Service.GetAsync(imported.Id)).AllowedActions.Should().Contain("Reconcile");
    }

    [Fact]
    public async Task MaximumLengthImportKeyProducesBoundedDeterministicAttemptKey()
    {
        await using var fixture = new Fixture();
        var idempotencyKey = new string('I', 100);
        var request = new RecordProcurementGhanepsImportRequest
        {
            SourceType = ProcurementGhanepsSourceType.Tender,
            SourceId = fixture.Tender.Id,
            EventFamily = ProcurementGhanepsEventFamily.TenderReference,
            MappingKey = "INBOUND",
            EventReference = fixture.Tender.TenderNumber,
            PayloadContent = """{"reference":"TDC-TND-001"}""",
            TransportReference = "GH-INBOUND-MAX-KEY",
            EvidenceReference = "inbound://max-key",
            IdempotencyKey = idempotencyKey
        };

        var imported = await fixture.Service.RecordImportAsync(
            request, "inbound-max-key");
        var replay = await fixture.Service.RecordImportAsync(
            request, "inbound-max-key-replay");
        var storedAttempt = await fixture.Context
            .Set<ProcurementGhanepsExchangeAttempt>()
            .SingleAsync(item => item.ExchangeEventId == imported.Id);
        var storedEvent = await fixture.Context
            .Set<ProcurementGhanepsExchangeEvent>()
            .SingleAsync(item => item.Id == imported.Id);

        replay.Id.Should().Be(imported.Id);
        storedEvent.IdempotencyKey.Should().Be(idempotencyKey);
        storedAttempt.IdempotencyKey.Should().HaveLength(73);
        storedAttempt.IdempotencyKey.Should().StartWith("received:");
    }

    [Fact]
    public async Task UnrelatedAuthenticatedRoleCannotReadSensitiveExchangeRegister()
    {
        await using var fixture = new Fixture();
        fixture.SetUnrelatedRole();

        var action = () => fixture.Service.GetOptionsAsync(
            ProcurementGhanepsSourceType.Tender, fixture.Tender.Id);

        await action.Should().ThrowAsync<ProcurementGhanepsExchangeAuthorizationException>();
    }

    [Fact]
    public async Task CrossTenantOverviewCannotDiscoverExchange()
    {
        await using var fixture = new Fixture();
        _ = await fixture.PrepareAsync("tenant-export", "PUB");
        fixture.SwitchTenant(Guid.NewGuid());

        var action = () => fixture.Service.GetOverviewAsync(
            ProcurementGhanepsSourceType.Tender, fixture.Tender.Id);

        await action.Should().ThrowAsync<ProcurementGhanepsExchangeNotFoundException>()
            .Where(exception => exception.Code == "GHANEPS_SOURCE_NOT_FOUND");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly Mock<ICurrentUserProvider> _current = new();
        private readonly Mock<IProcurementAccessControlService> _access = new();
        private readonly Mock<IProcurementSodGuardService> _sod = new();
        private readonly Mock<IProcurementControlEventService> _controlEvents = new();
        private readonly Mock<INotificationTopicPublisher> _notificationPublisher = new();
        private readonly InMemoryDatabaseRoot _databaseRoot = new();
        private readonly string _databaseName = Guid.NewGuid().ToString("N");
        private Guid _tenantId;
        private Guid _actorId;
        private IReadOnlyList<string> _roles = ["TDC_PROCUREMENT_OFFICER"];
        private bool _external;

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            _tenantId = TenantId;
            _actorId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(_databaseName, _databaseRoot)
                .ConfigureWarnings(warnings =>
                {
                    warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning);
                    warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                })
                .Options;
            Context = new ApplicationDbContext(options);
            Context.Add(new Tenant
            {
                Id = TenantId,
                Code = "TDC",
                Name = "TDC",
                Status = TenantStatus.Active
            });
            Tender = new Tender
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderNumber = "TDC-TND-001",
                Title = "Controlled Tender",
                TenderType = "ITB",
                Status = "Published",
                PublishDate = DateTime.UtcNow.AddDays(-3),
                SubmissionDeadline = DateTime.UtcNow.AddDays(7),
                Currency = "GHS",
                EstimatedValue = 500_000m,
                SourcingCaseId = Guid.NewGuid()
            };
            Control = new ProcurementTenderControl
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderId = Tender.Id,
                SourcingCaseId = Tender.SourcingCaseId!.Value,
                MethodRuleId = Guid.NewGuid(),
                AuthorityRouteId = Guid.NewGuid(),
                Method = ProcurementMethodType.NationalCompetitiveTendering,
                MethodRuleCode = "NCT-001",
                AuthorityRouteReference = "AUTH-001",
                Status = ProcurementTenderControlStatus.Advertised,
                AdvertisementReference = "GH-PUBLICATION-001",
                PublicationChannel = "GHANEPS",
                TenderDocumentReference = "DOC-001",
                TenderDocumentVersion = "1",
                AdvertisementEvidenceReference = "evidence://publication",
                AdvertisedAtUtc = Tender.PublishDate.Value,
                SubmissionDeadlineUtc = Tender.SubmissionDeadline!.Value,
                OpeningScheduledAtUtc = Tender.SubmissionDeadline.Value.AddHours(1),
                LifecycleSnapshotJson = "{}",
                IntegrityHash = new string('1', 64),
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            Profile = new ProcurementConfigurationProfile
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ProfileKey = Guid.NewGuid(),
                ProfileCode = "TDC-PROCUREMENT",
                Name = "Published TDC profile",
                Version = 4,
                LifecycleStatus = ProcurementConfigurationProfileStatus.Published,
                EffectiveFrom = DateTime.UtcNow.AddDays(-30),
                IsDefault = true,
                PublishedAt = DateTime.UtcNow.AddDays(-2),
                PublishedById = Guid.NewGuid(),
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            Decision = new ProcurementConfigurationDecision
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ProfileId = Profile.Id,
                DecisionKey = "DEC-009",
                SchemaVersion = 1,
                OwnerGroup = "Procurement + ICT/PPA",
                Status = ProcurementConfigurationDecisionStatus.Approved,
                ApprovalStatus = ProcurementConfigurationApprovalStatus.Approved,
                EvidenceStatus = ProcurementConfigurationEvidenceStatus.Verified,
                DecisionDate = DateTime.UtcNow.AddDays(-2),
                EffectiveFrom = DateTime.UtcNow.AddDays(-30),
                ApprovedById = Guid.NewGuid(),
                ApprovedAt = DateTime.UtcNow.AddDays(-2),
                ApprovalReference = "DEC009-MINUTE-001",
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            ReplaceMappings(DefaultMappings());
            var evidence = new ProcurementConfigurationEvidenceLink
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ProfileId = Profile.Id,
                DecisionId = Decision.Id,
                EvidenceType = "ApprovalMinute",
                ExternalReference = "evidence://dec009/approval",
                Checksum = new string('9', 64),
                UploadedById = Guid.NewGuid(),
                UploadedAt = DateTime.UtcNow.AddDays(-2)
            };
            Context.AddRange(Tender, Control, Profile, Decision, evidence);
            Context.SaveChanges();

            _current.SetupGet(item => item.TenantId).Returns(() => _tenantId);
            _current.SetupGet(item => item.UserId).Returns(() => _actorId);
            _current.SetupGet(item => item.IsAuthenticated).Returns(true);
            _current.SetupGet(item => item.IsExternalUser).Returns(() => _external);
            _current.SetupGet(item => item.Username).Returns("operator@tdc.test");
            _current.SetupGet(item => item.FullName).Returns("TDC Operator");
            _current.SetupGet(item => item.Roles).Returns(() => _roles);
            _current.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns((string role) => _roles.Any(value =>
                    string.Equals(value, role, StringComparison.OrdinalIgnoreCase)));
            _access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Message = "Allowed"
                });
            _access.Setup(item => item.CheckCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Message = "Allowed"
                });
            _sod.Setup(item => item.EnforceAsync(
                    It.IsAny<ProcurementSodGuardRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ProcurementSodGuardRequest, string, CancellationToken>(
                    (request, _, _) => SodRequests.Add(request))
                .ReturnsAsync((ProcurementSodGuardRequest request, string _, CancellationToken _) =>
                    new ProcurementSodGuardDecisionDto
                    {
                        Allowed = !request.ProhibitedActorUserIds.Contains(_actorId),
                        Message = request.ProhibitedActorUserIds.Contains(_actorId)
                            ? "SOD conflict"
                            : "Allowed"
                    });
            _controlEvents.Setup(item => item.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ProcurementControlEventWriteRequest, CancellationToken>(
                    (request, _) => ControlEvents.Add(request))
                .ReturnsAsync(new ProcurementControlEventDto());
            _notificationPublisher.Setup(item => item.PublishAsync(
                    It.IsAny<NotificationTopicEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<NotificationTopicEvent, CancellationToken>(
                    (request, _) => Notifications.Add(request))
                .Returns(Task.CompletedTask);
            _unitOfWork = new UnitOfWork(Context);
            Service = new ProcurementGhanepsExchangeService(_unitOfWork, _current.Object,
                _access.Object, _sod.Object, _controlEvents.Object,
                _notificationPublisher.Object);
        }

        public Guid TenantId { get; }
        public Guid CurrentActorId => _actorId;
        public ApplicationDbContext Context { get; }
        public Tender Tender { get; }
        public ProcurementTenderControl Control { get; }
        public ProcurementConfigurationProfile Profile { get; }
        public ProcurementConfigurationDecision Decision { get; }
        public ProcurementGhanepsExchangeService Service { get; }
        public List<ProcurementSodGuardRequest> SodRequests { get; } = [];
        public List<ProcurementControlEventWriteRequest> ControlEvents { get; } = [];
        public List<NotificationTopicEvent> Notifications { get; } = [];

        public Task<ProcurementGhanepsExchangeEventDto> PrepareAsync(
            string key,
            string mappingKey,
            ProcurementGhanepsEventFamily eventFamily =
                ProcurementGhanepsEventFamily.TenderPublication) =>
            Service.PrepareExportAsync(new PrepareProcurementGhanepsExportRequest
            {
                SourceType = ProcurementGhanepsSourceType.Tender,
                SourceId = Tender.Id,
                EventFamily = eventFamily,
                MappingKey = mappingKey,
                EventReference = Tender.TenderNumber,
                PayloadContent = """{"notice":"controlled"}""",
                FileName = $"{key}.json",
                EvidenceReference = $"evidence://{key}",
                IdempotencyKey = key
            }, key);

        public RecordProcurementGhanepsAcknowledgementRequest Acknowledgement(
            ProcurementGhanepsAcknowledgementOutcome outcome,
            string reference,
            string key,
            string rowVersion) => new()
        {
            Outcome = outcome,
            AcknowledgementReference = reference,
            ExternalStatusCode = outcome.ToString().ToUpperInvariant(),
            AcknowledgementContent = JsonSerializer.Serialize(new { reference, outcome }),
            EvidenceReference = $"evidence://{key}",
            IdempotencyKey = key,
            ExpectedRowVersion = rowVersion
        };

        public ProcurementGhanepsConfiguredMappingDto Mapping(
            string key,
            ProcurementGhanepsEventFamily family,
            ProcurementGhanepsExchangeDirection direction,
            List<ProcurementGhanepsSourceType> sourceTypes,
            bool acknowledgementRequired = true,
            bool reconciliationRequired = true) => new()
        {
            MappingKey = key,
            EventFamily = family,
            Direction = direction,
            SourceTypes = sourceTypes,
            SourceVariants =
                ["FormalTender", "LegacyTender", "ExceptionalSourcing", "RequestForQuotation"],
            ExternalEventCode = $"configured-{key}",
            TemplateReference = family == ProcurementGhanepsEventFamily.TenderPublication
                ? "config://ghaneps/tender-publication"
                : $"config://ghaneps/{key.ToLowerInvariant()}",
            SchemaReference = $"config://ghaneps/schema/{key.ToLowerInvariant()}",
            PayloadVersion = "configured-v1",
            ReferenceField = "configuredTenderReference",
            PayloadContentType = "application/json",
            AcknowledgementContentType = "application/json",
            AcknowledgementPermissionCode = "procurement.tender.approve",
            ReconciliationPermissionCode = "procurement.tender.approve",
            AcknowledgementRequired = acknowledgementRequired,
            ReconciliationRequired = reconciliationRequired,
            MaximumRetryAttempts = 3
        };

        public void ReplaceMappings(IEnumerable<ProcurementGhanepsConfiguredMappingDto> mappings)
        {
            ReplaceRawMappings(mappings.Select(item => JsonSerializer.Serialize(item)));
        }

        public void ReplaceRawMappings(IEnumerable<string> mappings)
        {
            Decision.ValueJson = JsonSerializer.Serialize(new ProcurementGhanepsDecisionValueDto
            {
                EffectiveFrom = DateTime.UtcNow.AddDays(-30),
                ProfileCode = "TDC-GHANEPS-CONFIGURED",
                FileTemplateMappings = mappings.ToList(),
                Frequency = "Configured",
                Owner = "Procurement ICT/PPA",
                AcknowledgementRule = "Configured external acknowledgement",
                ReconciliationRule = "Configured independent reconciliation"
            });
        }

        public void SwitchActor(Guid actorId) => _actorId = actorId;
        public void SwitchTenant(Guid tenantId) => _tenantId = tenantId;
        public void SetUnrelatedRole() => _roles = ["Employee"];
        public void SetRoles(params string[] roles) => _roles = roles;
        public void SetExternalUser() => _external = true;
        public void DenyCapabilities() =>
            _access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = false,
                    Message = "Denied by capability policy"
                });

        public async Task<ProcurementGhanepsExchangeEventDto> AdvanceRowVersionAsync(
            Guid exchangeEventId)
        {
            Context.ChangeTracker.Clear();
            var item = await Context.Set<ProcurementGhanepsExchangeEvent>()
                .SingleAsync(value => value.Id == exchangeEventId);
            item.RowVersion = Guid.NewGuid().ToByteArray();
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
            return await Service.GetAsync(exchangeEventId);
        }

        public async Task ConfigureMappingAsync(
            string mappingKey,
            Action<ProcurementGhanepsConfiguredMappingDto> configure)
        {
            var value = JsonSerializer.Deserialize<ProcurementGhanepsDecisionValueDto>(
                Decision.ValueJson)!;
            var mappings = value.FileTemplateMappings.Select(raw =>
                JsonSerializer.Deserialize<ProcurementGhanepsConfiguredMappingDto>(raw)!)
                .ToList();
            var mapping = mappings.Single(item =>
                string.Equals(item.MappingKey, mappingKey,
                    StringComparison.OrdinalIgnoreCase));
            configure(mapping);
            value.FileTemplateMappings = mappings.Select(item =>
                JsonSerializer.Serialize(item)).ToList();
            Decision.ValueJson = JsonSerializer.Serialize(value);
            Context.Update(Decision);
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        public async Task AddConfiguredMappingAsync(
            ProcurementGhanepsConfiguredMappingDto mapping)
        {
            AddMapping(mapping);
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        public ApplicationDbContext CreatePeerContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(_databaseName, _databaseRoot)
                .ConfigureWarnings(warnings =>
                {
                    warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning);
                    warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                })
                .Options;
            return new ApplicationDbContext(options);
        }

        public ProcurementGhanepsExchangeService CreateService(IUnitOfWork unitOfWork) =>
            new(unitOfWork, _current.Object, _access.Object, _sod.Object,
                _controlEvents.Object, _notificationPublisher.Object);

        public async Task<(ProcurementGhanepsSourceType Type, Guid Id, string Reference,
            string MappingKey)> SeedAdditionalSourceAsync(string kind)
        {
            if (kind == "rfq")
            {
                var rfq = new RequestForQuotation
                {
                    TenantId = TenantId,
                    RfqNumber = "TDC-RFQ-001",
                    Title = "Controlled RFQ",
                    Status = "Sent",
                    SentAt = DateTime.UtcNow.AddDays(-1),
                    SubmissionDeadline = DateTime.UtcNow.AddDays(5),
                    Currency = "GHS",
                    EstimatedValue = 50_000m,
                    SourcingCaseId = Guid.NewGuid()
                };
                Context.Add(rfq);
                AddMapping(Mapping("RFQ-REF",
                    ProcurementGhanepsEventFamily.TenderReference,
                    ProcurementGhanepsExchangeDirection.Export,
                    [ProcurementGhanepsSourceType.RequestForQuotation]));
                await Context.SaveChangesAsync();
                return (ProcurementGhanepsSourceType.RequestForQuotation, rfq.Id,
                    rfq.RfqNumber, "RFQ-REF");
            }
            if (kind == "exceptional")
            {
                var tender = new Tender
                {
                    TenantId = TenantId,
                    TenderNumber = "TDC-EXC-001",
                    Title = "Exceptional source",
                    TenderType = "SOLE",
                    Status = "Published",
                    PublishDate = DateTime.UtcNow.AddDays(-2),
                    Currency = "GHS",
                    SourcingCaseId = Guid.NewGuid()
                };
                var control = new ProcurementExceptionalSourcingControl
                {
                    TenantId = TenantId,
                    TenderId = tender.Id,
                    SourcingCaseId = tender.SourcingCaseId!.Value,
                    MethodRuleId = Guid.NewGuid(),
                    ExceptionRuleId = Guid.NewGuid(),
                    AuthorityRouteId = Guid.NewGuid(),
                    Method = ProcurementMethodType.SingleSource,
                    MethodRuleCode = "SS-001",
                    ExceptionRuleCode = "EXC-001",
                    AuthorityRouteReference = "AUTH-EXC-001",
                    Status = ProcurementExceptionalSourcingControlStatus.Approved,
                    Justification = "Controlled exceptional source",
                    JustificationEvidenceReference = "evidence://exception/justification",
                    SupplierSelectionEvidenceReference = "evidence://exception/supplier",
                    PreparedAtUtc = DateTime.UtcNow.AddDays(-3),
                    PreparedById = Guid.NewGuid(),
                    WorkflowDefinitionId = Guid.NewGuid(),
                    ApprovedAtUtc = DateTime.UtcNow.AddDays(-2),
                    ApprovedById = Guid.NewGuid(),
                    LifecycleSnapshotJson = "{}",
                    IntegrityHash = new string('8', 64),
                    RowVersion = Guid.NewGuid().ToByteArray()
                };
                Context.AddRange(tender, control);
                AddMapping(Mapping("EXC-REF",
                    ProcurementGhanepsEventFamily.TenderReference,
                    ProcurementGhanepsExchangeDirection.Export,
                    [ProcurementGhanepsSourceType.ExceptionalSourcing]));
                await Context.SaveChangesAsync();
                return (ProcurementGhanepsSourceType.ExceptionalSourcing, tender.Id,
                    tender.TenderNumber, "EXC-REF");
            }
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        public async Task<(ProcurementGhanepsSourceType Type, Guid Id, string Reference,
            string MappingKey)> SeedAdditionalAwardSourceAsync(string kind)
        {
            ProcurementGhanepsSourceType exchangeType;
            ProcurementAwardReadinessSourceType readinessType;
            ProcurementBidderCommunicationAwardFamily awardFamily;
            ProcurementMethodType method;
            Guid sourceId;
            string sourceReference;
            Guid awardId;
            string awardReference;
            DateTime awardedAt;
            string mappingKey;

            if (kind == "rfq")
            {
                var rfq = new RequestForQuotation
                {
                    TenantId = TenantId,
                    RfqNumber = "TDC-RFQ-AWARD-001",
                    Title = "Awarded RFQ",
                    Status = "Awarded",
                    SentAt = DateTime.UtcNow.AddDays(-3),
                    AwardedAt = DateTime.UtcNow.AddDays(-1),
                    SubmissionDeadline = DateTime.UtcNow.AddDays(-2),
                    Currency = "GHS",
                    EstimatedValue = 40_000m,
                    SourcingCaseId = Guid.NewGuid(),
                    AwardedBusinessPartnerId = Guid.NewGuid()
                };
                var award = new RequestForQuotationAwardLine
                {
                    TenantId = TenantId,
                    RfqId = rfq.Id,
                    RfqItemId = Guid.NewGuid(),
                    BusinessPartnerId = rfq.AwardedBusinessPartnerId.Value,
                    QuoteId = Guid.NewGuid(),
                    UnitPrice = 40_000m,
                    LineTotal = 40_000m,
                    AwardReason = "Controlled evaluated award"
                };
                Context.AddRange(rfq, award);
                exchangeType = ProcurementGhanepsSourceType.RequestForQuotation;
                readinessType = ProcurementAwardReadinessSourceType.RequestForQuotation;
                awardFamily = ProcurementBidderCommunicationAwardFamily.RequestForQuotation;
                method = ProcurementMethodType.RequestForQuotation;
                sourceId = rfq.Id;
                sourceReference = rfq.RfqNumber;
                awardId = rfq.Id;
                awardReference = $"{rfq.RfqNumber}-AWARD";
                awardedAt = rfq.AwardedAt.Value;
                mappingKey = "RFQ-AWARD";
            }
            else if (kind == "exceptional")
            {
                var tender = new Tender
                {
                    TenantId = TenantId,
                    TenderNumber = "TDC-EXC-AWARD-001",
                    Title = "Awarded exceptional source",
                    TenderType = "SOLE",
                    Status = "Awarded",
                    PublishDate = DateTime.UtcNow.AddDays(-3),
                    AwardDate = DateTime.UtcNow.AddDays(-1),
                    Currency = "GHS",
                    SourcingCaseId = Guid.NewGuid()
                };
                var control = new ProcurementExceptionalSourcingControl
                {
                    TenantId = TenantId,
                    TenderId = tender.Id,
                    SourcingCaseId = tender.SourcingCaseId.Value,
                    MethodRuleId = Guid.NewGuid(),
                    ExceptionRuleId = Guid.NewGuid(),
                    AuthorityRouteId = Guid.NewGuid(),
                    Method = ProcurementMethodType.SingleSource,
                    MethodRuleCode = "SS-AWARD",
                    ExceptionRuleCode = "EXC-AWARD",
                    AuthorityRouteReference = "AUTH-EXC-AWARD",
                    Status = ProcurementExceptionalSourcingControlStatus.Awarded,
                    Justification = "Controlled exceptional award",
                    JustificationEvidenceReference = "evidence://exception/justification",
                    SupplierSelectionEvidenceReference = "evidence://exception/supplier",
                    PreparedAtUtc = DateTime.UtcNow.AddDays(-4),
                    PreparedById = Guid.NewGuid(),
                    WorkflowDefinitionId = Guid.NewGuid(),
                    ApprovedAtUtc = DateTime.UtcNow.AddDays(-3),
                    ApprovedById = Guid.NewGuid(),
                    AwardBidId = Guid.NewGuid(),
                    AwardReference = "EXC-AWARD-001",
                    AwardEvidenceReference = "evidence://exception/award",
                    AwardedAtUtc = tender.AwardDate,
                    LifecycleSnapshotJson = "{}",
                    IntegrityHash = new string('8', 64),
                    RowVersion = Guid.NewGuid().ToByteArray()
                };
                Context.AddRange(tender, control);
                exchangeType = ProcurementGhanepsSourceType.ExceptionalSourcing;
                readinessType = ProcurementAwardReadinessSourceType.ExceptionalSourcing;
                awardFamily = ProcurementBidderCommunicationAwardFamily.ExceptionalSourcing;
                method = ProcurementMethodType.SingleSource;
                sourceId = tender.Id;
                sourceReference = tender.TenderNumber;
                awardId = control.Id;
                awardReference = control.AwardReference;
                awardedAt = control.AwardedAtUtc.Value;
                mappingKey = "EXC-AWARD";
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            var readiness = new ProcurementAwardReadinessDecision
            {
                TenantId = TenantId,
                SourceType = readinessType,
                SourceId = sourceId,
                SourceReference = sourceReference,
                Method = method,
                DecisionSequence = 1,
                Status = ProcurementAwardReadinessDecisionStatus.Ready,
                RecommendationSubjectType = kind == "rfq" ? "Quote" : "TenderBid",
                SourceIntegrityHash = new string('A', 64),
                IntegrityHash = new string('B', 64),
                IdempotencyKey = $"ready-{kind}",
                CorrelationId = $"ready-{kind}",
                EvaluatedAtUtc = awardedAt.AddHours(-2),
                EvaluatedByUserId = Guid.NewGuid(),
                EvaluatedByName = "Independent evaluator"
            };
            var communication = new ProcurementBidderCommunicationRegister
            {
                TenantId = TenantId,
                SourceType = readinessType,
                SourceId = sourceId,
                SourceReference = sourceReference,
                AwardFamily = awardFamily,
                AwardId = awardId,
                AwardReference = awardReference,
                AwardedAtUtc = awardedAt,
                AwardReadinessDecisionId = readiness.Id,
                AwardReadinessDecisionSequence = 1,
                AwardReadinessIntegrityHash = readiness.IntegrityHash,
                AwardReadinessSourceIntegrityHash = readiness.SourceIntegrityHash,
                StandstillStartsAtUtc = awardedAt,
                StandstillEndsAtUtc = awardedAt.AddDays(1),
                AppealWindowEndsAtUtc = awardedAt.AddDays(2),
                StandstillAuthorityReference = "DEC-009",
                RecipientSnapshotHash = new string('C', 64),
                IdempotencyKey = $"communication-{kind}",
                CorrelationId = $"communication-{kind}",
                InitializedAtUtc = awardedAt,
                InitializedByUserId = Guid.NewGuid(),
                InitializedByName = "Communication officer",
                IntegrityHash = new string('D', 64),
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            Context.AddRange(readiness, communication);
            AddMapping(Mapping(mappingKey,
                ProcurementGhanepsEventFamily.AwardNotification,
                ProcurementGhanepsExchangeDirection.Export, [exchangeType]));
            await Context.SaveChangesAsync();
            return (exchangeType, sourceId, sourceReference, mappingKey);
        }

        private void AddMapping(ProcurementGhanepsConfiguredMappingDto mapping)
        {
            var value = JsonSerializer.Deserialize<ProcurementGhanepsDecisionValueDto>(
                Decision.ValueJson)!;
            value.FileTemplateMappings.Add(JsonSerializer.Serialize(mapping));
            Decision.ValueJson = JsonSerializer.Serialize(value);
            Context.Update(Decision);
        }

        public async Task AddSecondEffectiveProfileAsync(bool isDefault = false)
        {
            var profile = new ProcurementConfigurationProfile
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ProfileKey = Guid.NewGuid(),
                ProfileCode = "TDC-PROCUREMENT-SECOND",
                Name = "Second effective profile",
                Version = 1,
                LifecycleStatus = ProcurementConfigurationProfileStatus.Published,
                EffectiveFrom = DateTime.UtcNow.AddDays(-1),
                IsDefault = isDefault,
                PublishedAt = DateTime.UtcNow.AddHours(-1),
                PublishedById = Guid.NewGuid(),
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            var decision = new ProcurementConfigurationDecision
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ProfileId = profile.Id,
                DecisionKey = "DEC-009",
                SchemaVersion = 1,
                OwnerGroup = "Procurement ICT/PPA",
                Status = ProcurementConfigurationDecisionStatus.Approved,
                ApprovalStatus = ProcurementConfigurationApprovalStatus.Approved,
                EvidenceStatus = ProcurementConfigurationEvidenceStatus.Verified,
                ValueJson = Decision.ValueJson,
                DecisionDate = DateTime.UtcNow.AddHours(-1),
                EffectiveFrom = DateTime.UtcNow.AddDays(-1),
                ApprovedById = Guid.NewGuid(),
                ApprovedAt = DateTime.UtcNow.AddHours(-1),
                ApprovalReference = "DEC009-SECOND",
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            Context.AddRange(profile, decision, new ProcurementConfigurationEvidenceLink
            {
                TenantId = TenantId,
                ProfileId = profile.Id,
                DecisionId = decision.Id,
                EvidenceType = "ApprovalMinute",
                ExternalReference = "evidence://dec009/second",
                UploadedById = Guid.NewGuid(),
                UploadedAt = DateTime.UtcNow
            });
            await Context.SaveChangesAsync();
        }

        public async Task PublishReplacementProfileWithSameMappingAsync()
        {
            Profile.LifecycleStatus = ProcurementConfigurationProfileStatus.Retired;
            Profile.EffectiveTo = DateTime.UtcNow.AddSeconds(-1);
            var replacement = new ProcurementConfigurationProfile
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ProfileKey = Profile.ProfileKey,
                ProfileCode = Profile.ProfileCode,
                Name = Profile.Name,
                Version = Profile.Version + 1,
                LifecycleStatus = ProcurementConfigurationProfileStatus.Published,
                EffectiveFrom = DateTime.UtcNow.AddMinutes(-1),
                IsDefault = true,
                PublishedAt = DateTime.UtcNow,
                PublishedById = Guid.NewGuid(),
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            var replacementDecision = new ProcurementConfigurationDecision
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ProfileId = replacement.Id,
                DecisionKey = "DEC-009",
                SchemaVersion = Decision.SchemaVersion,
                OwnerGroup = Decision.OwnerGroup,
                Status = ProcurementConfigurationDecisionStatus.Approved,
                ApprovalStatus = ProcurementConfigurationApprovalStatus.Approved,
                EvidenceStatus = ProcurementConfigurationEvidenceStatus.Verified,
                ValueJson = Decision.ValueJson,
                DecisionDate = DateTime.UtcNow,
                EffectiveFrom = DateTime.UtcNow.AddMinutes(-1),
                ApprovedById = Guid.NewGuid(),
                ApprovedAt = DateTime.UtcNow,
                ApprovalReference = "DEC009-REPLACEMENT",
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            Context.Update(Profile);
            Context.AddRange(replacement, replacementDecision,
                new ProcurementConfigurationEvidenceLink
                {
                    TenantId = TenantId,
                    ProfileId = replacement.Id,
                    DecisionId = replacementDecision.Id,
                    EvidenceType = "ApprovalMinute",
                    ExternalReference = "evidence://dec009/replacement",
                    Checksum = new string('8', 64),
                    UploadedById = Guid.NewGuid(),
                    UploadedAt = DateTime.UtcNow
                });
            await Context.SaveChangesAsync();
        }

        public async Task SeedAwardLineageAsync(bool blockLatest)
        {
            Tender.Status = "Awarded";
            Tender.AwardDate = DateTime.UtcNow.AddHours(-1);
            Control.Status = ProcurementTenderControlStatus.Awarded;
            Control.AwardBidId = Guid.NewGuid();
            Control.AwardReference = "AWARD-001";
            Control.AwardEvidenceReference = "evidence://award";
            Control.AwardedAtUtc = Tender.AwardDate;
            Context.UpdateRange(Tender, Control);
            var ready = new ProcurementAwardReadinessDecision
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                SourceType = ProcurementAwardReadinessSourceType.Tender,
                SourceId = Tender.Id,
                SourceReference = Tender.TenderNumber,
                Method = ProcurementMethodType.NationalCompetitiveTendering,
                DecisionSequence = 1,
                Status = ProcurementAwardReadinessDecisionStatus.Ready,
                RecommendationSubjectType = "TenderBid",
                SourceIntegrityHash = new string('2', 64),
                IntegrityHash = new string('3', 64),
                IdempotencyKey = "ready-1",
                CorrelationId = "ready-1",
                EvaluatedAtUtc = DateTime.UtcNow.AddHours(-2),
                EvaluatedByUserId = Guid.NewGuid(),
                EvaluatedByName = "Evaluator"
            };
            Context.Add(ready);
            Context.Add(new ProcurementBidderCommunicationRegister
            {
                TenantId = TenantId,
                SourceType = ProcurementAwardReadinessSourceType.Tender,
                SourceId = Tender.Id,
                SourceReference = Tender.TenderNumber,
                AwardFamily = ProcurementBidderCommunicationAwardFamily.FormalTender,
                AwardId = Control.Id,
                AwardReference = Control.AwardReference,
                AwardedAtUtc = Control.AwardedAtUtc!.Value,
                AwardReadinessDecisionId = ready.Id,
                AwardReadinessDecisionSequence = 1,
                AwardReadinessIntegrityHash = ready.IntegrityHash,
                AwardReadinessSourceIntegrityHash = ready.SourceIntegrityHash,
                StandstillStartsAtUtc = Tender.AwardDate.Value,
                StandstillEndsAtUtc = Tender.AwardDate.Value.AddDays(1),
                AppealWindowEndsAtUtc = Tender.AwardDate.Value.AddDays(2),
                StandstillAuthorityReference = "CONFIG",
                RecipientSnapshotHash = new string('4', 64),
                IdempotencyKey = "communication-1",
                CorrelationId = "communication-1",
                InitializedAtUtc = DateTime.UtcNow,
                InitializedByUserId = Guid.NewGuid(),
                InitializedByName = "Operator",
                IntegrityHash = new string('5', 64),
                RowVersion = Guid.NewGuid().ToByteArray()
            });
            if (blockLatest)
                Context.Add(new ProcurementAwardReadinessDecision
                {
                    TenantId = TenantId,
                    SourceType = ProcurementAwardReadinessSourceType.Tender,
                    SourceId = Tender.Id,
                    SourceReference = Tender.TenderNumber,
                    Method = ProcurementMethodType.NationalCompetitiveTendering,
                    DecisionSequence = 2,
                    Status = ProcurementAwardReadinessDecisionStatus.Blocked,
                    RecommendationSubjectType = "TenderBid",
                    SourceIntegrityHash = new string('6', 64),
                    IntegrityHash = new string('7', 64),
                    IdempotencyKey = "blocked-2",
                    CorrelationId = "blocked-2",
                    EvaluatedAtUtc = DateTime.UtcNow,
                    EvaluatedByUserId = Guid.NewGuid(),
                    EvaluatedByName = "Evaluator"
                });
            await Context.SaveChangesAsync();
        }

        private List<ProcurementGhanepsConfiguredMappingDto> DefaultMappings() =>
        [
            Mapping("PUB", ProcurementGhanepsEventFamily.TenderPublication,
                ProcurementGhanepsExchangeDirection.Bidirectional,
                [ProcurementGhanepsSourceType.Tender]),
            Mapping("REF", ProcurementGhanepsEventFamily.TenderReference,
                ProcurementGhanepsExchangeDirection.Export,
                [ProcurementGhanepsSourceType.Tender]),
            Mapping("AWARD", ProcurementGhanepsEventFamily.AwardNotification,
                ProcurementGhanepsExchangeDirection.Export,
                [ProcurementGhanepsSourceType.Tender]),
            Mapping("INBOUND", ProcurementGhanepsEventFamily.TenderReference,
                ProcurementGhanepsExchangeDirection.Import,
                [ProcurementGhanepsSourceType.Tender],
                acknowledgementRequired: false)
        ];

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }

    private sealed class FaultingUnitOfWork(
        IUnitOfWork inner,
        Func<Task> commitConcurrentWinner) : IUnitOfWork
    {
        private int _faulted;

        public IAccountRepository Accounts => inner.Accounts;
        public IAccountSegmentStructureRepository AccountSegmentStructures =>
            inner.AccountSegmentStructures;
        public IAccountSegmentValueRepository AccountSegmentValues =>
            inner.AccountSegmentValues;
        public ISegmentLookupValueRepository SegmentLookupValues =>
            inner.SegmentLookupValues;
        public bool HasActiveTransaction => inner.HasActiveTransaction;

        public async Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _faulted, 1) == 0)
            {
                await commitConcurrentWinner();
                throw new DbUpdateException("Synthetic late unique-key race.");
            }
            return await inner.SaveChangesAsync(cancellationToken);
        }

        public int SaveChanges() => inner.SaveChanges();
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            inner.BeginTransactionAsync(cancellationToken);
        public Task BeginTransactionAsync(
            IsolationLevel isolationLevel,
            CancellationToken cancellationToken = default) =>
            inner.BeginTransactionAsync(isolationLevel, cancellationToken);
        public Task AcquireTransactionLockAsync(
            string resource,
            CancellationToken cancellationToken = default) =>
            inner.AcquireTransactionLockAsync(resource, cancellationToken);
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            inner.CommitAsync(cancellationToken);
        public Task RollbackAsync(CancellationToken cancellationToken = default) =>
            inner.RollbackAsync(cancellationToken);
        public void ClearTrackedChanges() => inner.ClearTrackedChanges();
        public IGenericRepository<T> Repository<T>() where T : BaseEntity =>
            inner.Repository<T>();
        public Task ExecuteInStrategyAsync(
            Func<Task> operation,
            CancellationToken cancellationToken = default) =>
            inner.ExecuteInStrategyAsync(operation, cancellationToken);
        public Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default) =>
            inner.ExecuteInTransactionAsync(operation, cancellationToken);
        public void ClearChangeTracker() => inner.ClearChangeTracker();
        public Task<T> ExecuteInStrategyAsync<T>(
            Func<Task<T>> operation,
            CancellationToken cancellationToken = default) =>
            inner.ExecuteInStrategyAsync(operation, cancellationToken);
        public void Dispose()
        {
            // Non-owning test decorator; the fixture owns the delegated context/UoW lifetime.
        }
    }
}
