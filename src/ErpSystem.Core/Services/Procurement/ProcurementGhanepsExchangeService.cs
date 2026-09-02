using ErpSystem.Shared;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementGhanepsExchangeService : IProcurementGhanepsExchangeService
{
    private const string EventType = "ProcurementGhanepsExchange";
    private const string ManagePermission = "procurement.tender.administer";
    private const string ApprovePermission = "procurement.tender.approve";
    private const string InitiatorApproverSod = "SOD-INITIATOR-APPROVER";
    private const int MaximumContentBytes = 5_000_000;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementSodGuardService _sodGuard;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly INotificationTopicPublisher _notifications;

    public ProcurementGhanepsExchangeService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementSodGuardService sodGuard,
        IProcurementControlEventService controlEvents,
        INotificationTopicPublisher notifications)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _sodGuard = sodGuard;
        _controlEvents = controlEvents;
        _notifications = notifications;
    }

    private IGenericRepository<ProcurementGhanepsExchangeEvent> Events =>
        _unitOfWork.Repository<ProcurementGhanepsExchangeEvent>();
    private IGenericRepository<ProcurementGhanepsExchangePayload> Payloads =>
        _unitOfWork.Repository<ProcurementGhanepsExchangePayload>();
    private IGenericRepository<ProcurementGhanepsExchangeAttempt> Attempts =>
        _unitOfWork.Repository<ProcurementGhanepsExchangeAttempt>();
    private IGenericRepository<ProcurementGhanepsExchangeAcknowledgement> Acknowledgements =>
        _unitOfWork.Repository<ProcurementGhanepsExchangeAcknowledgement>();
    private IGenericRepository<ProcurementGhanepsExchangeReconciliation> Reconciliations =>
        _unitOfWork.Repository<ProcurementGhanepsExchangeReconciliation>();

    public async Task<ProcurementGhanepsExchangeOptionsDto> GetOptionsAsync(
        ProcurementGhanepsSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var source = await ResolveSourceAsync(sourceType, sourceId, null, cancellationToken);
        ResolvedProfile profile;
        try
        {
            profile = await ResolveProfileAsync(DateTime.UtcNow, cancellationToken);
        }
        catch (ProcurementGhanepsExchangeConflictException exception)
            when (exception.Code == "GHANEPS_PROFILE_NOT_EFFECTIVE")
        {
            return new ProcurementGhanepsExchangeOptionsDto
            {
                IsConfigured = false,
                ConfigurationMessage =
                    "GHANEPS exchange is optional and has not been configured for this tenant.",
                SourceType = source.Type,
                SourceId = source.Id,
                SourceReference = source.Reference,
                SourceVariant = source.Variant,
                BlockedReasons =
                [
                    "Configure and publish one effective DEC-009 profile before using GHANEPS exchange."
                ]
            };
        }
        var mappings = profile.Mappings
            .Where(item => MappingApplies(item, source))
            .OrderBy(item => item.EventFamily)
            .ThenBy(item => item.MappingKey, StringComparer.Ordinal)
            .ToList();
        var capabilities = await GetCapabilitiesAsync(source.Reference, cancellationToken);
        var allowed = new List<string>();
        var blocked = new List<string>();
        if (capabilities.Manage && mappings.Any(item =>
                item.Direction is ProcurementGhanepsExchangeDirection.Export or
                    ProcurementGhanepsExchangeDirection.Bidirectional))
            allowed.Add("PrepareExport");
        if (capabilities.Manage && mappings.Any(item =>
                item.Direction is ProcurementGhanepsExchangeDirection.Import or
                    ProcurementGhanepsExchangeDirection.Bidirectional))
            allowed.Add("RecordImport");
        if (!capabilities.Manage)
            blocked.Add("The current actor lacks the required GHANEPS exchange capability.");
        if (mappings.Count == 0)
            blocked.Add("Configure an applicable DEC-009 mapping before using GHANEPS exchange.");

        return new ProcurementGhanepsExchangeOptionsDto
        {
            IsConfigured = mappings.Count > 0,
            ConfigurationMessage = mappings.Count == 0
                ? "No GHANEPS mapping is configured for this procurement source."
                : null,
            SourceType = source.Type,
            SourceId = source.Id,
            SourceReference = source.Reference,
            SourceVariant = source.Variant,
            ConfigurationProfileId = profile.Profile.Id,
            ConfigurationProfileCode = profile.Profile.ProfileCode,
            ConfigurationProfileVersion = profile.Profile.Version,
            ConfigurationDecisionId = profile.Decision.Id,
            ExchangeProfileCode = profile.Value.ProfileCode,
            ConfigurationValueHash = profile.ValueHash,
            EffectiveFromUtc = profile.EffectiveFromUtc,
            EffectiveToUtc = profile.EffectiveToUtc,
            Frequency = profile.Value.Frequency,
            Owner = profile.Value.Owner,
            AcknowledgementRule = profile.Value.AcknowledgementRule,
            ReconciliationRule = profile.Value.ReconciliationRule,
            Mappings = mappings.Select(MapOption).ToList(),
            AllowedActions = allowed,
            BlockedReasons = blocked
        };
    }

    public async Task<ProcurementGhanepsExchangeOverviewDto> GetOverviewAsync(
        ProcurementGhanepsSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var source = await ResolveSourceAsync(sourceType, sourceId, null, cancellationToken);
        var items = await EventQuery()
            .Where(item => item.SourceType == sourceType && item.SourceId == sourceId)
            .AsNoTracking()
            .OrderByDescending(item => item.PreparedAtUtc)
            .ToListAsync(cancellationToken);
        var events = new List<ProcurementGhanepsExchangeEventDto>();
        foreach (var item in items)
        {
            var itemCapabilities = await GetCapabilitiesAsync(item.SourceReference,
                item.AcknowledgementPermissionCode, item.ReconciliationPermissionCode,
                cancellationToken);
            events.Add(MapEvent(item, itemCapabilities));
        }
        return new ProcurementGhanepsExchangeOverviewDto
        {
            SourceType = source.Type,
            SourceId = source.Id,
            SourceReference = source.Reference,
            SourceVariant = source.Variant,
            Events = events
        };
    }

    public async Task<ProcurementGhanepsComplianceDto> GetAwardComplianceAsync(
        ProcurementGhanepsSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated ||
            _currentUser.TenantId == Guid.Empty ||
            _currentUser.UserId == Guid.Empty ||
            _currentUser.IsExternalUser)
        {
            throw Authorization(
                "An authenticated internal tenant context is required to evaluate GHANEPS compliance.");
        }
        var source = await ResolveSourceAsync(
            sourceType, sourceId, null, cancellationToken);
        var profile = await ResolveProfileAsync(DateTime.UtcNow, cancellationToken);
        var mappings = profile.Mappings
            .Where(item => item.EventFamily ==
                           ProcurementGhanepsEventFamily.AwardNotification &&
                           MappingApplies(item, source))
            .OrderBy(item => item.MappingKey, StringComparer.Ordinal)
            .ToList();
        if (mappings.Count == 0)
        {
            return new ProcurementGhanepsComplianceDto
            {
                SourceType = source.Type,
                SourceId = source.Id,
                SourceReference = source.Reference,
                ConfigurationProfileId = profile.Profile.Id,
                ConfigurationDecisionId = profile.Decision.Id,
                ConfigurationValueHash = profile.ValueHash,
                HasApplicableMapping = false,
                IsCompliant = false,
                Code = "PO_GHANEPS_AWARD_MAPPING_MISSING",
                Message =
                    "The effective DEC-009 profile has no applicable award-notification mapping for this source."
            };
        }

        var events = await EventQuery()
            .Where(item =>
                item.SourceType == sourceType &&
                item.SourceId == sourceId &&
                item.EventFamily ==
                ProcurementGhanepsEventFamily.AwardNotification)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var results = mappings.Select(mapping =>
        {
            var currentMappingHash = Hash(Serialize(mapping));
            var exchange = events
                .Where(item =>
                    item.MappingKey == mapping.MappingKey &&
                    item.MappingIntegrityHash == currentMappingHash)
                .OrderByDescending(item => item.PreparedAtUtc)
                .ThenByDescending(item => item.CreatedAt)
                .FirstOrDefault();
            if (exchange is null)
            {
                return new ProcurementGhanepsComplianceMappingDto
                {
                    MappingKey = mapping.MappingKey,
                    AcknowledgementRequired = mapping.AcknowledgementRequired,
                    ReconciliationRequired = mapping.ReconciliationRequired,
                    Message =
                        "No award-notification exchange exists for the effective DEC-009 mapping definition."
                };
            }

            var successfulTransfer = exchange.Attempts.Any(item =>
                item.Outcome == ProcurementGhanepsAttemptOutcome.Succeeded);
            var acceptedAcknowledgement =
                !mapping.AcknowledgementRequired ||
                exchange.Acknowledgements.Any(item =>
                    item.Outcome ==
                    ProcurementGhanepsAcknowledgementOutcome.Accepted);
            var latestReconciliation = exchange.Reconciliations
                .OrderByDescending(item => item.Sequence)
                .FirstOrDefault();
            var completedReconciliation =
                !mapping.ReconciliationRequired ||
                latestReconciliation?.Outcome is
                    ProcurementGhanepsReconciliationOutcome.Matched or
                    ProcurementGhanepsReconciliationOutcome.Resolved;
            var evidenceAvailable =
                !string.IsNullOrWhiteSpace(exchange.EvidenceReference) ||
                exchange.Attempts.Any(item =>
                    !string.IsNullOrWhiteSpace(item.EvidenceReference)) ||
                exchange.Acknowledgements.Any(item =>
                    !string.IsNullOrWhiteSpace(item.EvidenceReference)) ||
                exchange.Reconciliations.Any(item =>
                    !string.IsNullOrWhiteSpace(item.EvidenceReference));
            var compliant = successfulTransfer &&
                            acceptedAcknowledgement &&
                            completedReconciliation &&
                            evidenceAvailable &&
                            exchange.Status is not
                                ProcurementGhanepsExchangeStatus.Failed and not
                                ProcurementGhanepsExchangeStatus.ReconciliationException;
            return new ProcurementGhanepsComplianceMappingDto
            {
                MappingKey = mapping.MappingKey,
                AcknowledgementRequired = mapping.AcknowledgementRequired,
                ReconciliationRequired = mapping.ReconciliationRequired,
                ExchangeEventId = exchange.Id,
                EventReference = exchange.EventReference,
                Status = exchange.Status,
                SuccessfulTransfer = successfulTransfer,
                AcceptedAcknowledgement = acceptedAcknowledgement,
                CompletedReconciliation = completedReconciliation,
                EvidenceAvailable = evidenceAvailable,
                IsCompliant = compliant,
                Message = compliant
                    ? "The current award-notification exchange has successful transfer and all configured evidence."
                    : "The current award-notification exchange is missing successful transfer, required acknowledgement/reconciliation, or retained evidence."
            };
        }).ToList();
        var ready = results.All(item => item.IsCompliant);
        return new ProcurementGhanepsComplianceDto
        {
            SourceType = source.Type,
            SourceId = source.Id,
            SourceReference = source.Reference,
            ConfigurationProfileId = profile.Profile.Id,
            ConfigurationDecisionId = profile.Decision.Id,
            ConfigurationValueHash = profile.ValueHash,
            HasApplicableMapping = true,
            IsCompliant = ready,
            Code = ready
                ? "PO_GHANEPS_EVIDENCE_CURRENT"
                : "PO_GHANEPS_EVIDENCE_INCOMPLETE",
            Message = ready
                ? "Every applicable DEC-009 award-notification mapping has current terminal evidence."
                : "One or more applicable DEC-009 award-notification mappings are incomplete.",
            Mappings = results
        };
    }

    public async Task<ProcurementGhanepsExchangeEventDto> GetAsync(
        Guid exchangeEventId,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var item = await LoadEventAsync(exchangeEventId, cancellationToken);
        var capabilities = await GetCapabilitiesAsync(item.SourceReference,
            item.AcknowledgementPermissionCode, item.ReconciliationPermissionCode,
            cancellationToken);
        return MapEvent(item, capabilities);
    }

    public Task<ProcurementGhanepsExchangeEventDto> PrepareExportAsync(
        PrepareProcurementGhanepsExportRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync(
            DenialContext.ForCreate("PrepareExport", request),
            correlationId,
            () => CreateExchangeAsync(
                request,
                ProcurementGhanepsExchangeDirection.Export,
                null,
                correlationId,
                cancellationToken),
            cancellationToken);

    public Task<ProcurementGhanepsExchangeEventDto> RecordImportAsync(
        RecordProcurementGhanepsImportRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync(
            DenialContext.ForCreate("RecordImport", request),
            correlationId,
            () => CreateExchangeAsync(
                request,
                ProcurementGhanepsExchangeDirection.Import,
                request.TransportReference,
                correlationId,
                cancellationToken),
            cancellationToken);

    public Task<ProcurementGhanepsExchangeEventDto> RecordAttemptAsync(
        Guid exchangeEventId,
        RecordProcurementGhanepsAttemptRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync(
            DenialContext.ForChild("RecordAttempt", exchangeEventId,
                request.IdempotencyKey, request.EvidenceReference,
                request.RouteSourceType, request.RouteSourceId),
            correlationId,
            () => RecordAttemptCoreAsync(exchangeEventId, request, correlationId,
                cancellationToken),
            cancellationToken);

    private async Task<ProcurementGhanepsExchangeEventDto> RecordAttemptCoreAsync(
        Guid exchangeEventId,
        RecordProcurementGhanepsAttemptRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        RequireIdempotency(request.IdempotencyKey);
        if (!request.PayloadId.HasValue || request.PayloadId.Value == Guid.Empty)
            throw Validation("GHANEPS_PAYLOAD_ID_REQUIRED",
                "A non-empty payload identifier is required.");
        if (!request.Outcome.HasValue || !Enum.IsDefined(request.Outcome.Value))
            throw Validation("GHANEPS_ATTEMPT_OUTCOME_INVALID",
                "The attempt outcome is required and must be supported.");
        RequireMaximum(request.EvidenceReference, 500,
            "GHANEPS_EVIDENCE_REFERENCE_TOO_LONG");
        var payloadId = request.PayloadId.Value;
        var outcome = request.Outcome.Value;
        var correlation = NormalizeCorrelation(correlationId);
        var item = await LoadEventTrackedAsync(exchangeEventId, cancellationToken);
        ValidateChildRoute(item, request);
        await EnsureCapabilityAsync(ManagePermission, item.SourceReference, correlation, cancellationToken);
        ValidateAttempt(outcome, request.TransportReference, request.FailureCode,
            request.FailureMessage);
        var requestFingerprint = Fingerprint(new
        {
            action = "RecordAttempt",
            PayloadId = payloadId,
            Outcome = outcome,
            transportReference = TrimOrNull(request.TransportReference, 200),
            failureCode = TrimOrNull(request.FailureCode, 100),
            failureMessage = TrimOrNull(request.FailureMessage, 2000),
            evidenceReference = TrimOrNull(request.EvidenceReference, 500)
        });

        var replay = await AttemptQuery().AsNoTracking().SingleOrDefaultAsync(value =>
            value.ExchangeEventId == item.Id &&
            value.IdempotencyKey == request.IdempotencyKey!.Trim(), cancellationToken);
        if (replay is not null)
        {
            AssertReplayFingerprint(replay.RequestFingerprint, requestFingerprint);
            return await LoadAndMapAsync(item.Id, cancellationToken);
        }
        AssertRowVersion(item, request.ExpectedRowVersion);
        if (item.Attempts.Count != 0)
            throw Conflict("GHANEPS_ATTEMPT_ALREADY_RECORDED",
                "The initial exchange attempt is already recorded. Use the retry action after a failure.");

        var payload = item.Payloads.SingleOrDefault(value => value.Id == payloadId)
            ?? throw NotFound("GHANEPS_PAYLOAD_NOT_FOUND",
                "The payload does not belong to this exchange event in the current tenant.");
        var attempt = CreateAttempt(item, payload, outcome, false, null,
            request.TransportReference, request.FailureCode, request.FailureMessage,
            request.EvidenceReference, request.IdempotencyKey!, requestFingerprint);

        item.Status = StatusAfterAttempt(item, outcome);
        item.UpdatedAt = DateTime.UtcNow;
        item.LastModifiedById = _currentUser.UserId;
        if (!await PersistMutationAsync(item, attempt, "AttemptRecorded", correlation,
            outcome == ProcurementGhanepsAttemptOutcome.Succeeded
                ? ProcurementControlEventResult.Succeeded
                : ProcurementControlEventResult.Failed,
            request.FailureMessage, request.EvidenceReference, cancellationToken))
            return await LoadAndMapAsync(item.Id, cancellationToken);
        await PublishOperationalAsync(item, outcome == ProcurementGhanepsAttemptOutcome.Failed
            ? "procurement.ghaneps.exchange.failed"
            : "procurement.ghaneps.exchange.attempt-succeeded", new
        {
            attempt.Id,
            attempt.AttemptNumber,
            attempt.Outcome,
            attempt.TransportReference,
            attempt.FailureCode
        }, cancellationToken);
        return await LoadAndMapAsync(item.Id, cancellationToken);
    }

    public Task<ProcurementGhanepsExchangeEventDto> RetryAsync(
        Guid exchangeEventId,
        RetryProcurementGhanepsExchangeRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync(
            DenialContext.ForChild("Retry", exchangeEventId,
                request.IdempotencyKey, request.EvidenceReference,
                request.RouteSourceType, request.RouteSourceId),
            correlationId,
            () => RetryCoreAsync(exchangeEventId, request, correlationId, cancellationToken),
            cancellationToken);

    private async Task<ProcurementGhanepsExchangeEventDto> RetryCoreAsync(
        Guid exchangeEventId,
        RetryProcurementGhanepsExchangeRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        RequireIdempotency(request.IdempotencyKey);
        if (!request.Outcome.HasValue || !Enum.IsDefined(request.Outcome.Value))
            throw Validation("GHANEPS_ATTEMPT_OUTCOME_INVALID",
                "The retry outcome is required and must be supported.");
        RequireMaximum(request.FileName, 260, "GHANEPS_FILE_NAME_TOO_LONG");
        RequireMaximum(request.EvidenceReference, 500,
            "GHANEPS_EVIDENCE_REFERENCE_TOO_LONG");
        var outcome = request.Outcome.Value;
        var correlation = NormalizeCorrelation(correlationId);
        var item = await LoadEventTrackedAsync(exchangeEventId, cancellationToken);
        ValidateChildRoute(item, request);
        await EnsureCapabilityAsync(ManagePermission, item.SourceReference, correlation, cancellationToken);
        ValidateAttempt(outcome, request.TransportReference, request.FailureCode,
            request.FailureMessage);
        var normalizedReplacementContent = string.IsNullOrWhiteSpace(
            request.ReplacementPayloadContent)
            ? null
            : NormalizeContent(request.ReplacementPayloadContent, item.PayloadContentType,
                "GHANEPS_PAYLOAD");
        var replacementChecksum = normalizedReplacementContent is null
            ? null
            : Hash(normalizedReplacementContent);
        if (replacementChecksum is not null)
            AssertOptionalHash(request.ExpectedPayloadChecksumSha256, replacementChecksum,
                "GHANEPS_PAYLOAD_CHECKSUM_MISMATCH");
        else if (!string.IsNullOrWhiteSpace(request.ExpectedPayloadChecksumSha256))
            throw Validation("GHANEPS_RETRY_CHECKSUM_WITHOUT_REPLACEMENT",
                "An expected replacement checksum requires replacement payload content.");
        var requestFingerprint = Fingerprint(new
        {
            action = "Retry",
            request.PayloadId,
            Outcome = outcome,
            transportReference = TrimOrNull(request.TransportReference, 200),
            failureCode = TrimOrNull(request.FailureCode, 100),
            failureMessage = TrimOrNull(request.FailureMessage, 2000),
            item.PayloadContentType,
            replacementChecksum,
            fileName = TrimOrNull(request.FileName, 260),
            evidenceReference = TrimOrNull(request.EvidenceReference, 500)
        });

        var replay = await AttemptQuery().AsNoTracking().SingleOrDefaultAsync(value =>
            value.ExchangeEventId == item.Id &&
            value.IdempotencyKey == request.IdempotencyKey!.Trim(), cancellationToken);
        if (replay is not null)
        {
            AssertReplayFingerprint(replay.RequestFingerprint, requestFingerprint);
            return await LoadAndMapAsync(item.Id, cancellationToken);
        }
        AssertRowVersion(item, request.ExpectedRowVersion);

        var latest = item.Attempts.OrderByDescending(value => value.AttemptNumber).FirstOrDefault()
            ?? throw Conflict("GHANEPS_RETRY_WITHOUT_ATTEMPT",
                "An initial failed exchange attempt is required before retry.");
        var latestAcknowledgement = item.Acknowledgements
            .OrderByDescending(value => value.Sequence).FirstOrDefault();
        var retryAfterRejectedAcknowledgement =
            latestAcknowledgement?.Outcome == ProcurementGhanepsAcknowledgementOutcome.Rejected &&
            latestAcknowledgement.AttemptId == latest.Id;
        if (latest.Outcome != ProcurementGhanepsAttemptOutcome.Failed &&
            !retryAfterRejectedAcknowledgement)
            throw Conflict("GHANEPS_RETRY_NOT_FAILED",
                "Only a failed attempt or rejected acknowledgement can be retried.");
        if (retryAfterRejectedAcknowledgement &&
            string.IsNullOrWhiteSpace(request.ReplacementPayloadContent))
            throw Validation("GHANEPS_REJECTED_ACK_REQUIRES_REPLACEMENT",
                "Retry after a rejected acknowledgement requires a corrected replacement payload.");
        var retryCount = item.Attempts.Count(value => value.IsRetry);
        if (retryCount >= item.MaximumRetryAttempts)
            throw Conflict("GHANEPS_RETRY_LIMIT_EXHAUSTED",
                "The maximum retry count configured by the effective DEC-009 mapping has been reached.");
        ProcurementGhanepsExchangePayload payload;
        if (normalizedReplacementContent is not null)
        {
            payload = CreatePayload(item, normalizedReplacementContent,
                item.PayloadContentType, request.FileName,
            request.EvidenceReference, request.IdempotencyKey!,
                item.Payloads.Max(value => value.Version) + 1);
            await Payloads.AddAsync(payload);
        }
        else
        {
            var payloadId = request.PayloadId ?? latest.PayloadId;
            payload = item.Payloads.SingleOrDefault(value => value.Id == payloadId)
                ?? throw NotFound("GHANEPS_PAYLOAD_NOT_FOUND",
                    "The retry payload does not belong to this exchange event.");
        }

        var attempt = CreateAttempt(item, payload, outcome, true, latest.Id,
            request.TransportReference, request.FailureCode, request.FailureMessage,
            request.EvidenceReference, request.IdempotencyKey!, requestFingerprint);
        item.Status = StatusAfterAttempt(item, outcome);
        item.UpdatedAt = DateTime.UtcNow;
        item.LastModifiedById = _currentUser.UserId;
        if (!await PersistMutationAsync(item, attempt, "RetryRecorded", correlation,
            outcome == ProcurementGhanepsAttemptOutcome.Succeeded
                ? ProcurementControlEventResult.Succeeded
                : ProcurementControlEventResult.Failed,
            request.FailureMessage, request.EvidenceReference, cancellationToken))
            return await LoadAndMapAsync(item.Id, cancellationToken);
        var exhausted = outcome == ProcurementGhanepsAttemptOutcome.Failed &&
            retryCount + 1 >= item.MaximumRetryAttempts;
        await PublishOperationalAsync(item, exhausted
            ? "procurement.ghaneps.exchange.retry-exhausted"
            : "procurement.ghaneps.exchange.retry", new
        {
            attempt.Id,
            attempt.AttemptNumber,
            attempt.Outcome,
            retryCount = retryCount + 1,
            item.MaximumRetryAttempts
        }, cancellationToken);
        return await LoadAndMapAsync(item.Id, cancellationToken);
    }

    public Task<ProcurementGhanepsExchangeEventDto> RecordAcknowledgementAsync(
        Guid exchangeEventId,
        RecordProcurementGhanepsAcknowledgementRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync(
            DenialContext.ForChild("RecordAcknowledgement", exchangeEventId,
                request.IdempotencyKey, request.EvidenceReference,
                request.RouteSourceType, request.RouteSourceId),
            correlationId,
            () => RecordAcknowledgementCoreAsync(exchangeEventId, request, correlationId,
                cancellationToken),
            cancellationToken);

    private async Task<ProcurementGhanepsExchangeEventDto> RecordAcknowledgementCoreAsync(
        Guid exchangeEventId,
        RecordProcurementGhanepsAcknowledgementRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        RequireIdempotency(request.IdempotencyKey);
        Require(request.AcknowledgementReference, "GHANEPS_ACKNOWLEDGEMENT_REFERENCE_REQUIRED",
            "The acknowledgement reference is required.");
        if (!request.Outcome.HasValue || !Enum.IsDefined(request.Outcome.Value))
            throw Validation("GHANEPS_ACKNOWLEDGEMENT_OUTCOME_INVALID",
                "The acknowledgement outcome is required and must be supported.");
        var outcome = request.Outcome.Value;
        RequireMaximum(request.AcknowledgementReference, 200,
            "GHANEPS_ACKNOWLEDGEMENT_REFERENCE_TOO_LONG");
        RequireMaximum(request.ExternalStatusCode, 100,
            "GHANEPS_ACKNOWLEDGEMENT_STATUS_TOO_LONG");
        Require(request.EvidenceReference, "GHANEPS_ACKNOWLEDGEMENT_EVIDENCE_REQUIRED",
            "Acknowledgement evidence is required.");
        RequireMaximum(request.EvidenceReference, 500,
            "GHANEPS_ACKNOWLEDGEMENT_EVIDENCE_TOO_LONG");
        var correlation = NormalizeCorrelation(correlationId);
        var item = await LoadEventTrackedAsync(exchangeEventId, cancellationToken);
        ValidateChildRoute(item, request);
        await EnsureCapabilityAsync(item.AcknowledgementPermissionCode,
            item.SourceReference, correlation, cancellationToken);
        var acknowledgementContent = NormalizeContent(request.AcknowledgementContent,
            item.AcknowledgementContentType, "GHANEPS_ACKNOWLEDGEMENT");
        var checksum = Hash(acknowledgementContent);
        AssertOptionalHash(request.ExpectedAcknowledgementChecksumSha256, checksum,
            "GHANEPS_ACKNOWLEDGEMENT_CHECKSUM_MISMATCH");
        var requestFingerprint = Fingerprint(new
        {
            action = "RecordAcknowledgement",
            Outcome = outcome,
            acknowledgementReference = request.AcknowledgementReference!.Trim(),
            externalStatusCode = TrimOrNull(request.ExternalStatusCode, 100),
            item.AcknowledgementContentType,
            acknowledgementChecksumSha256 = checksum,
            evidenceReference = request.EvidenceReference!.Trim()
        });

        var replay = await AcknowledgementQuery().AsNoTracking().SingleOrDefaultAsync(value =>
            value.ExchangeEventId == item.Id &&
            value.IdempotencyKey == request.IdempotencyKey!.Trim(), cancellationToken);
        if (replay is not null)
        {
            AssertReplayFingerprint(replay.RequestFingerprint, requestFingerprint);
            return await LoadAndMapAsync(item.Id, cancellationToken);
        }
        AssertRowVersion(item, request.ExpectedRowVersion);
        if (!item.AcknowledgementRequired)
            throw Conflict("GHANEPS_ACKNOWLEDGEMENT_NOT_CONFIGURED",
                "The immutable DEC-009 mapping does not require acknowledgement for this event.");
        if (!item.Attempts.Any(value => value.Outcome == ProcurementGhanepsAttemptOutcome.Succeeded))
            throw Conflict("GHANEPS_ACKNOWLEDGEMENT_BEFORE_SUCCESS",
                "A successful exchange attempt is required before acknowledgement.");
        if (item.Acknowledgements.Any(value =>
                value.Outcome == ProcurementGhanepsAcknowledgementOutcome.Accepted))
            throw Conflict("GHANEPS_ACKNOWLEDGEMENT_TERMINAL",
                "The exchange event already has an accepted terminal acknowledgement.");
        var successfulAttempt = item.Attempts
            .Where(value => value.Outcome == ProcurementGhanepsAttemptOutcome.Succeeded)
            .OrderByDescending(value => value.AttemptNumber)
            .First();
        if (item.Acknowledgements.Any(value => value.AttemptId == successfulAttempt.Id))
            throw Conflict("GHANEPS_ACKNOWLEDGEMENT_ATTEMPT_ALREADY_RECORDED",
                "The latest successful attempt already has an acknowledgement.");
        var prohibitedActors = ActorLineage(item);
        await EnforceSodAsync(item.SourceReference, prohibitedActors,
            correlation, cancellationToken);

        var acknowledgement = new ProcurementGhanepsExchangeAcknowledgement
        {
            TenantId = _currentUser.TenantId,
            ExchangeEventId = item.Id,
            AttemptId = successfulAttempt.Id,
            PayloadId = successfulAttempt.PayloadId,
            Sequence = item.Acknowledgements.Count + 1,
            Outcome = outcome,
            AcknowledgementReference = request.AcknowledgementReference!.Trim(),
            ExternalStatusCode = TrimOrNull(request.ExternalStatusCode, 100),
            ContentType = item.AcknowledgementContentType,
            AcknowledgementContent = acknowledgementContent,
            AcknowledgementChecksumSha256 = checksum,
            RequestFingerprint = requestFingerprint,
            IdempotencyKey = request.IdempotencyKey!.Trim(),
            AcknowledgedByUserId = _currentUser.UserId,
            AcknowledgedByName = ActorName(),
            AcknowledgedAtUtc = DateTime.UtcNow,
            EvidenceReference = request.EvidenceReference!.Trim()
        };
        acknowledgement.IntegrityHash = Hash(Serialize(new
        {
            acknowledgement.TenantId,
            acknowledgement.ExchangeEventId,
            acknowledgement.AttemptId,
            acknowledgement.PayloadId,
            acknowledgement.Sequence,
            acknowledgement.Outcome,
            acknowledgement.AcknowledgementReference,
            acknowledgement.ExternalStatusCode,
            acknowledgement.ContentType,
            acknowledgement.AcknowledgementChecksumSha256,
            acknowledgement.RequestFingerprint,
            acknowledgement.AcknowledgedByUserId,
            acknowledgement.AcknowledgedAtUtc,
            acknowledgement.EvidenceReference
        }));
        item.Status = outcome == ProcurementGhanepsAcknowledgementOutcome.Accepted
            ? ProcurementGhanepsExchangeStatus.Acknowledged
            : ProcurementGhanepsExchangeStatus.Failed;
        item.UpdatedAt = acknowledgement.AcknowledgedAtUtc;
        item.LastModifiedById = _currentUser.UserId;
        if (!await PersistMutationAsync(item, acknowledgement, "AcknowledgementRecorded", correlation,
            outcome == ProcurementGhanepsAcknowledgementOutcome.Accepted
                ? ProcurementControlEventResult.Succeeded
                : ProcurementControlEventResult.Rejected,
            request.ExternalStatusCode, request.EvidenceReference, cancellationToken))
            return await LoadAndMapAsync(item.Id, cancellationToken);
        await PublishOperationalAsync(item,
            outcome == ProcurementGhanepsAcknowledgementOutcome.Accepted
                ? "procurement.ghaneps.exchange.acknowledged"
                : "procurement.ghaneps.exchange.ack-rejected",
            new
            {
                acknowledgement.Id,
                acknowledgement.Outcome,
                acknowledgement.AcknowledgementReference,
                acknowledgement.ExternalStatusCode
            }, cancellationToken);
        return await LoadAndMapAsync(item.Id, cancellationToken);
    }

    public Task<ProcurementGhanepsExchangeEventDto> ReconcileAsync(
        Guid exchangeEventId,
        ReconcileProcurementGhanepsExchangeRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        ExecuteMutationAsync(
            DenialContext.ForChild("Reconcile", exchangeEventId,
                request.IdempotencyKey, request.EvidenceReference,
                request.RouteSourceType, request.RouteSourceId),
            correlationId,
            () => ReconcileCoreAsync(exchangeEventId, request, correlationId,
                cancellationToken),
            cancellationToken);

    private async Task<ProcurementGhanepsExchangeEventDto> ReconcileCoreAsync(
        Guid exchangeEventId,
        ReconcileProcurementGhanepsExchangeRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        RequireIdempotency(request.IdempotencyKey);
        Require(request.ActualReference, "GHANEPS_RECONCILIATION_REFERENCE_REQUIRED",
            "The observed GHANEPS reference is required.");
        RequireMaximum(request.ActualReference, 200,
            "GHANEPS_RECONCILIATION_REFERENCE_TOO_LONG");
        Require(request.EvidenceReference, "GHANEPS_RECONCILIATION_EVIDENCE_REQUIRED",
            "Reconciliation evidence is required.");
        RequireMaximum(request.EvidenceReference, 500,
            "GHANEPS_RECONCILIATION_EVIDENCE_TOO_LONG");
        RequireMaximum(request.Notes, 2000, "GHANEPS_RECONCILIATION_NOTES_TOO_LONG");
        Require(request.ActualChecksumSha256, "GHANEPS_RECONCILIATION_CHECKSUM_REQUIRED",
            "The observed payload checksum is required.");
        var actualChecksum = NormalizeHash(request.ActualChecksumSha256);
        var correlation = NormalizeCorrelation(correlationId);
        var item = await LoadEventTrackedAsync(exchangeEventId, cancellationToken);
        ValidateChildRoute(item, request);
        await EnsureCapabilityAsync(item.ReconciliationPermissionCode,
            item.SourceReference, correlation, cancellationToken);
        var requestFingerprint = Fingerprint(new
        {
            action = "Reconcile",
            request.ResolveExistingMismatch,
            actualReference = request.ActualReference!.Trim(),
            actualChecksumSha256 = actualChecksum,
            notes = TrimOrNull(request.Notes, 2000),
            evidenceReference = request.EvidenceReference!.Trim()
        });

        var replay = await ReconciliationQuery().AsNoTracking().SingleOrDefaultAsync(value =>
            value.ExchangeEventId == item.Id &&
            value.IdempotencyKey == request.IdempotencyKey!.Trim(), cancellationToken);
        if (replay is not null)
        {
            AssertReplayFingerprint(replay.RequestFingerprint, requestFingerprint);
            return await LoadAndMapAsync(item.Id, cancellationToken);
        }
        AssertRowVersion(item, request.ExpectedRowVersion);
        if (!item.ReconciliationRequired)
            throw Conflict("GHANEPS_RECONCILIATION_NOT_CONFIGURED",
                "The immutable DEC-009 mapping does not require reconciliation for this event.");
        var authoritativeAttempt = item.Attempts
            .Where(value => value.Outcome == ProcurementGhanepsAttemptOutcome.Succeeded)
            .OrderByDescending(value => value.AttemptNumber)
            .FirstOrDefault();
        if (authoritativeAttempt is null)
            throw Conflict("GHANEPS_RECONCILIATION_BEFORE_SUCCESS",
                "A successful exchange attempt is required before reconciliation.");
        if (item.AcknowledgementRequired && !item.Acknowledgements.Any(value =>
                value.Outcome == ProcurementGhanepsAcknowledgementOutcome.Accepted &&
                value.AttemptId == authoritativeAttempt.Id &&
                value.PayloadId == authoritativeAttempt.PayloadId))
            throw Conflict("GHANEPS_RECONCILIATION_BEFORE_ACKNOWLEDGEMENT",
                "An accepted acknowledgement bound to the authoritative successful attempt and payload is required before reconciliation.");

        var authoritativePayload = item.Payloads.SingleOrDefault(value =>
            value.Id == authoritativeAttempt.PayloadId)
            ?? throw Conflict("GHANEPS_ATTEMPT_PAYLOAD_LINEAGE_INVALID",
                "The authoritative successful attempt has no same-event payload lineage.");
        var latestReconciliation = item.Reconciliations
            .OrderByDescending(value => value.Sequence).FirstOrDefault();
        if (latestReconciliation?.Outcome is ProcurementGhanepsReconciliationOutcome.Matched or
            ProcurementGhanepsReconciliationOutcome.Resolved)
            throw Conflict("GHANEPS_RECONCILIATION_TERMINAL",
                "A matched or independently resolved reconciliation is terminal.");
        if (latestReconciliation?.Outcome == ProcurementGhanepsReconciliationOutcome.Mismatch &&
            !request.ResolveExistingMismatch)
            throw Conflict("GHANEPS_RECONCILIATION_RESOLUTION_REQUIRED",
                "The latest mismatch can only proceed through independent resolution.");
        if (latestReconciliation is null && request.ResolveExistingMismatch)
            throw Conflict("GHANEPS_RECONCILIATION_RESOLUTION_NOT_ALLOWED",
                "There is no prior mismatch to resolve.");
        ProcurementGhanepsReconciliationOutcome outcome;
        var prohibitedActors = ActorLineage(item);
        if (request.ResolveExistingMismatch)
        {
            if (latestReconciliation?.Outcome != ProcurementGhanepsReconciliationOutcome.Mismatch)
                throw Conflict("GHANEPS_RECONCILIATION_RESOLUTION_NOT_ALLOWED",
                    "Only the latest unresolved mismatch can be independently resolved.");
            prohibitedActors.Add(latestReconciliation.ReconciledByUserId);
            outcome = ProcurementGhanepsReconciliationOutcome.Resolved;
        }
        else
        {
            var matches = string.Equals(item.EventReference, request.ActualReference!.Trim(),
                              StringComparison.Ordinal) &&
                          string.Equals(authoritativePayload.PayloadChecksumSha256, actualChecksum,
                              StringComparison.OrdinalIgnoreCase);
            outcome = matches
                ? ProcurementGhanepsReconciliationOutcome.Matched
                : ProcurementGhanepsReconciliationOutcome.Mismatch;
        }
        await EnforceSodAsync(item.SourceReference, prohibitedActors.Distinct().ToList(),
            correlation, cancellationToken);

        var reconciliation = new ProcurementGhanepsExchangeReconciliation
        {
            TenantId = _currentUser.TenantId,
            ExchangeEventId = item.Id,
            AttemptId = authoritativeAttempt.Id,
            PayloadId = authoritativePayload.Id,
            Sequence = item.Reconciliations.Count + 1,
            Outcome = outcome,
            ExpectedReference = item.EventReference,
            ActualReference = request.ActualReference!.Trim(),
            ExpectedChecksumSha256 = authoritativePayload.PayloadChecksumSha256,
            ActualChecksumSha256 = actualChecksum,
            RequestFingerprint = requestFingerprint,
            Notes = TrimOrNull(request.Notes, 2000),
            IdempotencyKey = request.IdempotencyKey!.Trim(),
            ReconciledByUserId = _currentUser.UserId,
            ReconciledByName = ActorName(),
            ReconciledAtUtc = DateTime.UtcNow,
            EvidenceReference = request.EvidenceReference!.Trim()
        };
        reconciliation.IntegrityHash = Hash(Serialize(new
        {
            reconciliation.TenantId,
            reconciliation.ExchangeEventId,
            reconciliation.AttemptId,
            reconciliation.PayloadId,
            reconciliation.Sequence,
            reconciliation.Outcome,
            reconciliation.ExpectedReference,
            reconciliation.ActualReference,
            reconciliation.ExpectedChecksumSha256,
            reconciliation.ActualChecksumSha256,
            reconciliation.RequestFingerprint,
            reconciliation.Notes,
            reconciliation.ReconciledByUserId,
            reconciliation.ReconciledAtUtc,
            reconciliation.EvidenceReference
        }));
        item.Status = outcome == ProcurementGhanepsReconciliationOutcome.Mismatch
            ? ProcurementGhanepsExchangeStatus.ReconciliationException
            : ProcurementGhanepsExchangeStatus.Reconciled;
        item.UpdatedAt = reconciliation.ReconciledAtUtc;
        item.LastModifiedById = _currentUser.UserId;
        if (!await PersistMutationAsync(item, reconciliation, "ReconciliationRecorded", correlation,
            outcome == ProcurementGhanepsReconciliationOutcome.Mismatch
                ? ProcurementControlEventResult.Warning
                : ProcurementControlEventResult.Succeeded,
            request.Notes, request.EvidenceReference, cancellationToken))
            return await LoadAndMapAsync(item.Id, cancellationToken);
        await PublishOperationalAsync(item,
            outcome == ProcurementGhanepsReconciliationOutcome.Mismatch
                ? "procurement.ghaneps.exchange.reconciliation-exception"
                : "procurement.ghaneps.exchange.reconciled",
            new
            {
                reconciliation.Id,
                reconciliation.Outcome,
                reconciliation.ExpectedReference,
                reconciliation.ActualReference,
                reconciliation.ExpectedChecksumSha256,
                reconciliation.ActualChecksumSha256
            }, cancellationToken);
        return await LoadAndMapAsync(item.Id, cancellationToken);
    }

    private async Task<ProcurementGhanepsExchangeEventDto> CreateExchangeAsync(
        ProcurementGhanepsPayloadRequest request,
        ProcurementGhanepsExchangeDirection requestedDirection,
        string? transportReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!request.SourceType.HasValue || !Enum.IsDefined(request.SourceType.Value))
            throw Validation("GHANEPS_SOURCE_TYPE_INVALID",
                "The GHANEPS source type is required and must be supported.");
        if (!request.SourceId.HasValue || request.SourceId.Value == Guid.Empty)
            throw Validation("GHANEPS_SOURCE_ID_REQUIRED",
                "A non-empty GHANEPS source identifier is required.");
        if (!request.EventFamily.HasValue || !Enum.IsDefined(request.EventFamily.Value))
            throw Validation("GHANEPS_EVENT_FAMILY_INVALID",
                "The requested GHANEPS event family is required and must be supported.");
        RequireIdempotency(request.IdempotencyKey);
        Require(request.MappingKey, "GHANEPS_MAPPING_KEY_REQUIRED", "A DEC-009 mapping key is required.");
        RequireMaximum(request.MappingKey, 100, "GHANEPS_MAPPING_KEY_TOO_LONG");
        Require(request.EventReference, "GHANEPS_EVENT_REFERENCE_REQUIRED",
            "The exact source event reference is required.");
        RequireMaximum(request.EventReference, 200, "GHANEPS_EVENT_REFERENCE_TOO_LONG");
        RequireMaximum(request.FileName, 260, "GHANEPS_FILE_NAME_TOO_LONG");
        RequireMaximum(request.EvidenceReference, 500, "GHANEPS_EVIDENCE_REFERENCE_TOO_LONG");
        RequireMaximum(transportReference, 200, "GHANEPS_TRANSPORT_REFERENCE_TOO_LONG");
        if (requestedDirection == ProcurementGhanepsExchangeDirection.Import)
            Require(transportReference, "GHANEPS_TRANSPORT_REFERENCE_REQUIRED",
                "The inbound transport reference is required.");
        var sourceType = request.SourceType.Value;
        var sourceId = request.SourceId.Value;
        var eventFamily = request.EventFamily.Value;
        ValidateCreateRoute(request, sourceType, sourceId);
        var mappingKey = request.MappingKey!.Trim();
        var eventReference = request.EventReference!.Trim();
        var correlation = NormalizeCorrelation(correlationId);
        var idempotency = request.IdempotencyKey!.Trim();
        var replay = await EventQuery().AsNoTracking().SingleOrDefaultAsync(item =>
            item.SourceType == sourceType && item.SourceId == sourceId &&
            item.IdempotencyKey == idempotency, cancellationToken);
        if (replay is not null)
        {
            await EnsureCapabilityAsync(ManagePermission, replay.SourceReference, correlation,
                cancellationToken);
            var replayContent = NormalizeContent(request.PayloadContent,
                replay.PayloadContentType, "GHANEPS_PAYLOAD");
            var replayChecksum = Hash(replayContent);
            AssertOptionalHash(request.ExpectedPayloadChecksumSha256, replayChecksum,
                "GHANEPS_PAYLOAD_CHECKSUM_MISMATCH");
            var replayFingerprint = CreateExchangeRequestFingerprint(request,
                requestedDirection, transportReference, eventReference,
                mappingKey, replay.PayloadContentType, replayChecksum,
                replay.ConfigurationProfileId, replay.ConfigurationProfileVersion,
                replay.ConfigurationDecisionId, replay.MappingIntegrityHash);
            AssertReplayFingerprint(replay.RequestFingerprint, replayFingerprint);
            var replayCapabilities = await GetCapabilitiesAsync(replay.SourceReference,
                replay.AcknowledgementPermissionCode, replay.ReconciliationPermissionCode,
                cancellationToken);
            return MapEvent(replay, replayCapabilities);
        }
        var source = await ResolveSourceAsync(sourceType, sourceId,
            eventFamily, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, source.Reference, correlation, cancellationToken);
        if (!string.Equals(source.Reference, eventReference, StringComparison.Ordinal))
            throw Validation("GHANEPS_EVENT_REFERENCE_MISMATCH",
                "The event reference must exactly match the server-derived source reference.");
        var profile = await ResolveProfileAsync(DateTime.UtcNow, cancellationToken);
        var mapping = profile.Mappings.SingleOrDefault(item =>
            string.Equals(item.MappingKey, mappingKey, StringComparison.OrdinalIgnoreCase) &&
            item.EventFamily == eventFamily &&
            MappingApplies(item, source))
            ?? throw Validation("GHANEPS_MAPPING_NOT_APPLICABLE",
                "The effective DEC-009 decision has no unique applicable mapping for this source and event.");
        if (mapping.Direction != ProcurementGhanepsExchangeDirection.Bidirectional &&
            mapping.Direction != requestedDirection)
            throw Validation("GHANEPS_MAPPING_DIRECTION_MISMATCH",
                "The requested exchange direction is not enabled by the effective DEC-009 mapping.");

        var normalizedPayload = NormalizeContent(request.PayloadContent,
            mapping.PayloadContentType, "GHANEPS_PAYLOAD");
        var payloadChecksum = Hash(normalizedPayload);
        AssertOptionalHash(request.ExpectedPayloadChecksumSha256, payloadChecksum,
            "GHANEPS_PAYLOAD_CHECKSUM_MISMATCH");
        var mappingSnapshot = Serialize(mapping);
        var mappingIntegrityHash = Hash(mappingSnapshot);
        var requestFingerprint = CreateExchangeRequestFingerprint(request,
            requestedDirection, transportReference, source.Reference, mapping.MappingKey,
            mapping.PayloadContentType, payloadChecksum, profile.Profile.Id,
            profile.Profile.Version, profile.Decision.Id, mappingIntegrityHash);
        if (await EventQuery().AnyAsync(item =>
                item.SourceType == sourceType && item.SourceId == sourceId &&
                item.EventFamily == eventFamily &&
                item.Direction == requestedDirection &&
                item.MappingKey == mapping.MappingKey &&
                item.EventReference == source.Reference &&
                item.MappingIntegrityHash == mappingIntegrityHash, cancellationToken))
            throw Conflict("GHANEPS_EVENT_ALREADY_EXISTS",
                "This exact source event and DEC-009 mapping definition already has an exchange record.");

        var now = DateTime.UtcNow;
        var item = new ProcurementGhanepsExchangeEvent
        {
            TenantId = _currentUser.TenantId,
            SourceType = source.Type,
            SourceId = source.Id,
            SourceReference = source.Reference,
            SourceVariant = source.Variant,
            SourceOccurredAtUtc = source.OccurredAtUtc,
            SourceSnapshotJson = source.SnapshotJson,
            SourceIntegrityHash = source.IntegrityHash,
            EventFamily = eventFamily,
            Direction = requestedDirection,
            MappingKey = mapping.MappingKey.Trim(),
            ExternalEventCode = mapping.ExternalEventCode.Trim(),
            EventReference = source.Reference,
            ReferenceField = mapping.ReferenceField.Trim(),
            PayloadContentType = NormalizeContentType(mapping.PayloadContentType,
                "GHANEPS_MAPPING_PAYLOAD_CONTENT_TYPE_INVALID"),
            AcknowledgementContentType = NormalizeContentType(mapping.AcknowledgementContentType,
                "GHANEPS_MAPPING_ACKNOWLEDGEMENT_CONTENT_TYPE_INVALID"),
            AcknowledgementPermissionCode = mapping.AcknowledgementPermissionCode.Trim(),
            ReconciliationPermissionCode = mapping.ReconciliationPermissionCode.Trim(),
            ConfigurationProfileId = profile.Profile.Id,
            ConfigurationProfileCode = profile.Profile.ProfileCode,
            ConfigurationProfileVersion = profile.Profile.Version,
            ConfigurationDecisionId = profile.Decision.Id,
            ConfigurationDecisionSchemaVersion = profile.Decision.SchemaVersion,
            ExchangeProfileCode = profile.Value.ProfileCode.Trim(),
            ConfigurationValueJson = profile.ValueJson,
            ConfigurationValueHash = profile.ValueHash,
            MappingSnapshotJson = mappingSnapshot,
            MappingIntegrityHash = mappingIntegrityHash,
            ConfigurationEffectiveFromUtc = profile.EffectiveFromUtc,
            ConfigurationEffectiveToUtc = profile.EffectiveToUtc,
            Frequency = profile.Value.Frequency.Trim(),
            Owner = profile.Value.Owner.Trim(),
            AcknowledgementRule = profile.Value.AcknowledgementRule.Trim(),
            ReconciliationRule = profile.Value.ReconciliationRule.Trim(),
            AcknowledgementRequired = mapping.AcknowledgementRequired,
            ReconciliationRequired = mapping.ReconciliationRequired,
            MaximumRetryAttempts = mapping.MaximumRetryAttempts,
            Status = ProcurementGhanepsExchangeStatus.Prepared,
            RequestFingerprint = requestFingerprint,
            IdempotencyKey = idempotency,
            CorrelationId = correlation,
            PreparedByUserId = _currentUser.UserId,
            PreparedByName = ActorName(),
            PreparedAtUtc = now,
            EvidenceReference = TrimOrNull(request.EvidenceReference, 500),
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        item.IntegrityHash = Hash(Serialize(new
        {
            item.TenantId,
            item.SourceType,
            item.SourceId,
            item.SourceReference,
            item.SourceVariant,
            item.SourceOccurredAtUtc,
            item.SourceIntegrityHash,
            item.EventFamily,
            item.Direction,
            item.MappingKey,
            item.ExternalEventCode,
            item.EventReference,
            item.ReferenceField,
            item.PayloadContentType,
            item.AcknowledgementContentType,
            item.AcknowledgementPermissionCode,
            item.ReconciliationPermissionCode,
            item.ConfigurationProfileId,
            item.ConfigurationProfileVersion,
            item.ConfigurationDecisionId,
            item.ConfigurationDecisionSchemaVersion,
            item.ExchangeProfileCode,
            item.ConfigurationValueHash,
            item.MappingIntegrityHash,
            item.ConfigurationEffectiveFromUtc,
            item.ConfigurationEffectiveToUtc,
            item.AcknowledgementRequired,
            item.ReconciliationRequired,
            item.MaximumRetryAttempts,
            item.RequestFingerprint,
            item.PreparedByUserId,
            item.PreparedAtUtc
        }));
        var payload = CreatePayload(item, normalizedPayload, item.PayloadContentType, request.FileName,
            request.EvidenceReference, idempotency, 1);
        item.Payloads.Add(payload);

        ProcurementGhanepsExchangeAttempt? importAttempt = null;
        if (requestedDirection == ProcurementGhanepsExchangeDirection.Import)
        {
            importAttempt = CreateAttempt(item, payload, ProcurementGhanepsAttemptOutcome.Succeeded,
                false, null, transportReference, null, null, request.EvidenceReference,
                CreateAutomaticAttemptIdempotency(idempotency), Fingerprint(new
                {
                    action = "RecordImportAttempt",
                    payload.Id,
                    outcome = ProcurementGhanepsAttemptOutcome.Succeeded,
                    transportReference = transportReference!.Trim(),
                    evidenceReference = TrimOrNull(request.EvidenceReference, 500)
                }));
            item.Attempts.Add(importAttempt);
            item.Status = mapping.AcknowledgementRequired
                ? ProcurementGhanepsExchangeStatus.PendingAcknowledgement
                : ProcurementGhanepsExchangeStatus.Transferred;
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await Events.AddAsync(item);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordControlEventAsync(item,
                requestedDirection == ProcurementGhanepsExchangeDirection.Export
                    ? "ExportPrepared"
                    : "ImportRecorded",
                ProcurementControlEventResult.Succeeded, idempotency,
                new
                {
                    payload.Id,
                    payload.Version,
                    payload.PayloadChecksumSha256,
                    importAttemptId = importAttempt?.Id,
                    transportReference
                }, null, request.EvidenceReference, correlation, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _unitOfWork.ClearTrackedChanges();
            var concurrent = await EventQuery().AsNoTracking().SingleOrDefaultAsync(value =>
                value.SourceType == sourceType &&
                value.SourceId == sourceId &&
                value.IdempotencyKey == idempotency, cancellationToken);
            if (concurrent is not null)
            {
                AssertReplayFingerprint(concurrent.RequestFingerprint,
                    requestFingerprint);
                var capabilities = await GetCapabilitiesAsync(
                    concurrent.SourceReference,
                    concurrent.AcknowledgementPermissionCode,
                    concurrent.ReconciliationPermissionCode,
                    cancellationToken);
                return MapEvent(concurrent, capabilities);
            }
            throw Conflict("GHANEPS_IDEMPOTENCY_CONFLICT",
                "A concurrent exchange write used the same unique source identity with different inputs.");
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
        await PublishOperationalAsync(item,
            requestedDirection == ProcurementGhanepsExchangeDirection.Export
                ? "procurement.ghaneps.exchange.export-prepared"
                : "procurement.ghaneps.exchange.import-recorded",
            new
            {
                item.Id,
                item.EventFamily,
                item.Direction,
                item.MappingKey,
                item.EventReference,
                payloadId = payload.Id,
                payload.PayloadChecksumSha256
            }, cancellationToken);
        return await LoadAndMapAsync(item.Id, cancellationToken);
    }

    private async Task<bool> PersistMutationAsync(
        ProcurementGhanepsExchangeEvent item,
        ProcurementGhanepsExchangeAttempt child,
        string action,
        string correlationId,
        ProcurementControlEventResult result,
        string? reason,
        string? evidenceReference,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await Attempts.AddAsync(child);
            await Events.UpdateAsync(item);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordControlEventAsync(item, action, result, child.IdempotencyKey,
                new { child.Id, child.AttemptNumber, child.Outcome, child.TransportReference,
                    child.FailureCode, item.Status }, reason, evidenceReference,
                correlationId, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _unitOfWork.ClearTrackedChanges();
            if (await TryRecoverAttemptReplayAsync(item.Id, child.IdempotencyKey,
                    child.RequestFingerprint, cancellationToken))
                return false;
            throw Conflict("GHANEPS_EXCHANGE_CONCURRENCY_CONFLICT",
                "The exchange event changed after it was loaded.");
        }
        catch (DbUpdateException)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _unitOfWork.ClearTrackedChanges();
            if (await TryRecoverAttemptReplayAsync(item.Id, child.IdempotencyKey,
                    child.RequestFingerprint, cancellationToken))
                return false;
            throw Conflict("GHANEPS_IDEMPOTENCY_CONFLICT",
                "A concurrent attempt write used the same unique exchange identity with different inputs.");
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<bool> PersistMutationAsync(
        ProcurementGhanepsExchangeEvent item,
        ProcurementGhanepsExchangeAcknowledgement child,
        string action,
        string correlationId,
        ProcurementControlEventResult result,
        string? reason,
        string? evidenceReference,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await Acknowledgements.AddAsync(child);
            await Events.UpdateAsync(item);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordControlEventAsync(item, action, result, child.IdempotencyKey,
                new { child.Id, child.Sequence, child.Outcome,
                    child.AcknowledgementReference, child.AcknowledgementChecksumSha256, item.Status },
                reason, evidenceReference, correlationId, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _unitOfWork.ClearTrackedChanges();
            if (await TryRecoverAcknowledgementReplayAsync(item.Id,
                    child.IdempotencyKey, child.RequestFingerprint, cancellationToken))
                return false;
            throw Conflict("GHANEPS_EXCHANGE_CONCURRENCY_CONFLICT",
                "The exchange event changed after it was loaded.");
        }
        catch (DbUpdateException)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _unitOfWork.ClearTrackedChanges();
            if (await TryRecoverAcknowledgementReplayAsync(item.Id,
                    child.IdempotencyKey, child.RequestFingerprint, cancellationToken))
                return false;
            throw Conflict("GHANEPS_IDEMPOTENCY_CONFLICT",
                "A concurrent acknowledgement write used the same unique exchange identity with different inputs.");
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<bool> PersistMutationAsync(
        ProcurementGhanepsExchangeEvent item,
        ProcurementGhanepsExchangeReconciliation child,
        string action,
        string correlationId,
        ProcurementControlEventResult result,
        string? reason,
        string? evidenceReference,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await Reconciliations.AddAsync(child);
            await Events.UpdateAsync(item);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await RecordControlEventAsync(item, action, result, child.IdempotencyKey,
                new { child.Id, child.AttemptId, child.PayloadId, child.Sequence,
                    child.Outcome, child.ExpectedReference,
                    child.ActualReference, child.ExpectedChecksumSha256,
                    child.ActualChecksumSha256, item.Status },
                reason, evidenceReference, correlationId, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _unitOfWork.ClearTrackedChanges();
            if (await TryRecoverReconciliationReplayAsync(item.Id,
                    child.IdempotencyKey, child.RequestFingerprint, cancellationToken))
                return false;
            throw Conflict("GHANEPS_EXCHANGE_CONCURRENCY_CONFLICT",
                "The exchange event changed after it was loaded.");
        }
        catch (DbUpdateException)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _unitOfWork.ClearTrackedChanges();
            if (await TryRecoverReconciliationReplayAsync(item.Id,
                    child.IdempotencyKey, child.RequestFingerprint, cancellationToken))
                return false;
            throw Conflict("GHANEPS_IDEMPOTENCY_CONFLICT",
                "A concurrent reconciliation write used the same unique exchange identity with different inputs.");
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<bool> TryRecoverAttemptReplayAsync(
        Guid exchangeEventId,
        string idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        var replay = await AttemptQuery().AsNoTracking().SingleOrDefaultAsync(value =>
            value.ExchangeEventId == exchangeEventId &&
            value.IdempotencyKey == idempotencyKey, cancellationToken);
        if (replay is null) return false;
        AssertReplayFingerprint(replay.RequestFingerprint, requestFingerprint);
        return true;
    }

    private async Task<bool> TryRecoverAcknowledgementReplayAsync(
        Guid exchangeEventId,
        string idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        var replay = await AcknowledgementQuery().AsNoTracking().SingleOrDefaultAsync(value =>
            value.ExchangeEventId == exchangeEventId &&
            value.IdempotencyKey == idempotencyKey, cancellationToken);
        if (replay is null) return false;
        AssertReplayFingerprint(replay.RequestFingerprint, requestFingerprint);
        return true;
    }

    private async Task<bool> TryRecoverReconciliationReplayAsync(
        Guid exchangeEventId,
        string idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        var replay = await ReconciliationQuery().AsNoTracking().SingleOrDefaultAsync(value =>
            value.ExchangeEventId == exchangeEventId &&
            value.IdempotencyKey == idempotencyKey, cancellationToken);
        if (replay is null) return false;
        AssertReplayFingerprint(replay.RequestFingerprint, requestFingerprint);
        return true;
    }

    private async Task<ResolvedProfile> ResolveProfileAsync(
        DateTime atUtc,
        CancellationToken cancellationToken)
    {
        var candidates = await _unitOfWork.Repository<ProcurementConfigurationDecision>()
            .GetQueryable(item =>
                item.TenantId == _currentUser.TenantId &&
                !item.IsDeleted &&
                item.DecisionKey == "DEC-009" &&
                item.Status == ProcurementConfigurationDecisionStatus.Approved &&
                item.ApprovalStatus == ProcurementConfigurationApprovalStatus.Approved &&
                item.EvidenceStatus != ProcurementConfigurationEvidenceStatus.Missing &&
                item.DecisionDate.HasValue &&
                (!item.EffectiveFrom.HasValue || item.EffectiveFrom.Value <= atUtc) &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= atUtc) &&
                !item.Profile.IsDeleted &&
                item.Profile.LifecycleStatus == ProcurementConfigurationProfileStatus.Published &&
                item.Profile.PublishedAt.HasValue &&
                item.Profile.EffectiveFrom <= atUtc &&
                (!item.Profile.EffectiveTo.HasValue || item.Profile.EffectiveTo.Value >= atUtc))
            .Include(item => item.Profile)
            .Include(item => item.EvidenceLinks.Where(link => !link.IsDeleted))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0)
            throw Conflict("GHANEPS_PROFILE_NOT_EFFECTIVE",
                "No approved, evidenced, Published and effective DEC-009 decision is available.");
        var defaults = candidates.Where(item => item.Profile.IsDefault).ToList();
        if (defaults.Count > 1 || defaults.Count == 0 && candidates.Count > 1)
            throw Conflict("GHANEPS_PROFILE_AMBIGUOUS",
                defaults.Count > 1
                    ? "More than one default approved Published/effective DEC-009 decision is available."
                    : "More than one non-default approved Published/effective DEC-009 decision is available.");
        var decision = defaults.Count == 1 ? defaults[0] : candidates[0];
        if (decision.EvidenceLinks.Count == 0 ||
            decision.EvidenceLinks.Any(link =>
                link.TenantId != _currentUser.TenantId ||
                link.ProfileId != decision.ProfileId ||
                link.DecisionId != decision.Id))
            throw Conflict("GHANEPS_PROFILE_EVIDENCE_INVALID",
                "The effective DEC-009 decision has no complete same-tenant evidence lineage.");

        var normalizedValueJson = NormalizeJson(decision.ValueJson,
            "GHANEPS_PROFILE_VALUE_INVALID",
            "GHANEPS_PROFILE_VALUE_DUPLICATE_PROPERTY");
        ProcurementGhanepsDecisionValueDto value;
        try
        {
            value = JsonSerializer.Deserialize<ProcurementGhanepsDecisionValueDto>(
                        normalizedValueJson, JsonOptions)
                    ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw Conflict("GHANEPS_PROFILE_VALUE_INVALID",
                "The effective DEC-009 value is not valid JSON for the registered decision shape.");
        }
        Require(value.ProfileCode, "GHANEPS_PROFILE_CODE_REQUIRED",
            "The DEC-009 exchange profile code is required.");
        RequireMaximum(value.ProfileCode, 100, "GHANEPS_PROFILE_CODE_TOO_LONG");
        Require(value.Frequency, "GHANEPS_PROFILE_FREQUENCY_REQUIRED",
            "The DEC-009 submission frequency is required.");
        RequireMaximum(value.Frequency, 100, "GHANEPS_PROFILE_FREQUENCY_TOO_LONG");
        Require(value.Owner, "GHANEPS_PROFILE_OWNER_REQUIRED",
            "The DEC-009 reconciliation owner is required.");
        RequireMaximum(value.Owner, 200, "GHANEPS_PROFILE_OWNER_TOO_LONG");
        Require(value.AcknowledgementRule, "GHANEPS_PROFILE_ACK_RULE_REQUIRED",
            "The DEC-009 acknowledgement rule is required.");
        RequireMaximum(value.AcknowledgementRule, 1000, "GHANEPS_PROFILE_ACK_RULE_TOO_LONG");
        Require(value.ReconciliationRule, "GHANEPS_PROFILE_RECONCILIATION_RULE_REQUIRED",
            "The DEC-009 reconciliation rule is required.");
        RequireMaximum(value.ReconciliationRule, 1000,
            "GHANEPS_PROFILE_RECONCILIATION_RULE_TOO_LONG");
        var valueFrom = value.EffectiveFrom == default
            ? decision.EffectiveFrom ?? decision.Profile.EffectiveFrom
            : value.EffectiveFrom;
        var valueTo = value.EffectiveTo ?? decision.EffectiveTo ?? decision.Profile.EffectiveTo;
        if (valueFrom > atUtc || valueTo.HasValue && valueTo.Value < atUtc)
            throw Conflict("GHANEPS_PROFILE_VALUE_NOT_EFFECTIVE",
                "The DEC-009 value is outside its configured effective period.");
        if (value.FileTemplateMappings is null)
            throw Validation("GHANEPS_MAPPING_COLLECTION_REQUIRED",
                "DEC-009 FileTemplateMappings must be a non-null array.");
        if (value.FileTemplateMappings.Count == 0)
            throw Conflict("GHANEPS_MAPPING_MISSING",
                "The effective DEC-009 value contains no file/template mappings.");
        var mappings = new List<ProcurementGhanepsConfiguredMappingDto>();
        foreach (var raw in value.FileTemplateMappings)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw Validation("GHANEPS_MAPPING_ENTRY_REQUIRED",
                    "DEC-009 FileTemplateMappings cannot contain null or empty elements.");
            try
            {
                var normalizedMapping = NormalizeJson(raw, "GHANEPS_MAPPING_INVALID",
                    "GHANEPS_MAPPING_DUPLICATE_PROPERTY");
                var mapping = JsonSerializer.Deserialize<ProcurementGhanepsConfiguredMappingDto>(
                                  normalizedMapping, JsonOptions)
                              ?? throw new JsonException();
                ValidateMapping(mapping);
                mapping.PayloadContentType = NormalizeContentType(mapping.PayloadContentType,
                    "GHANEPS_MAPPING_PAYLOAD_CONTENT_TYPE_INVALID");
                mapping.AcknowledgementContentType = NormalizeContentType(
                    mapping.AcknowledgementContentType,
                    "GHANEPS_MAPPING_ACKNOWLEDGEMENT_CONTENT_TYPE_INVALID");
                mappings.Add(mapping);
            }
            catch (JsonException)
            {
                throw Conflict("GHANEPS_MAPPING_INVALID",
                    "Every DEC-009 FileTemplateMapping must be a valid configured GHANEPS event mapping.");
            }
        }
        if (mappings.GroupBy(item => item.MappingKey, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() != 1))
            throw Conflict("GHANEPS_MAPPING_AMBIGUOUS",
                "DEC-009 mapping keys must be unique within the effective exchange profile.");
        return new ResolvedProfile(
            decision.Profile,
            decision,
            value,
            normalizedValueJson,
            Hash(normalizedValueJson),
            EnsureUtc(valueFrom),
            valueTo.HasValue ? EnsureUtc(valueTo.Value) : null,
            mappings);
    }

    private async Task<ResolvedSource> ResolveSourceAsync(
        ProcurementGhanepsSourceType sourceType,
        Guid sourceId,
        ProcurementGhanepsEventFamily? eventFamily,
        CancellationToken cancellationToken)
    {
        if (sourceId == Guid.Empty)
            throw Validation("GHANEPS_SOURCE_ID_REQUIRED", "SourceId is required.");
        return sourceType switch
        {
            ProcurementGhanepsSourceType.RequestForQuotation =>
                await ResolveRfqSourceAsync(sourceId, eventFamily, cancellationToken),
            ProcurementGhanepsSourceType.Tender =>
                await ResolveTenderSourceAsync(sourceId, false, eventFamily, cancellationToken),
            ProcurementGhanepsSourceType.ExceptionalSourcing =>
                await ResolveTenderSourceAsync(sourceId, true, eventFamily, cancellationToken),
            _ => throw Validation("GHANEPS_SOURCE_TYPE_INVALID",
                "Only Tender, RequestForQuotation and ExceptionalSourcing sources are supported.")
        };
    }

    private async Task<ResolvedSource> ResolveRfqSourceAsync(
        Guid sourceId,
        ProcurementGhanepsEventFamily? eventFamily,
        CancellationToken cancellationToken)
    {
        var source = await _unitOfWork.Repository<RequestForQuotation>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.Id == sourceId && !item.IsDeleted)
            .Include(item => item.AwardLines.Where(line => !line.IsDeleted))
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("GHANEPS_SOURCE_NOT_FOUND",
                "The RFQ source was not found in the current tenant.");
        if (eventFamily is ProcurementGhanepsEventFamily.TenderPublication or
            ProcurementGhanepsEventFamily.TenderReference)
        {
            if (!string.Equals(source.Status, "Sent", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(source.Status, "Closed", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(source.Status, "Awarded", StringComparison.OrdinalIgnoreCase))
                throw Conflict("GHANEPS_SOURCE_NOT_PUBLISHED",
                    "The RFQ must be sent before a publication or tender-reference event can be exchanged.");
            if (!source.SentAt.HasValue)
                throw Conflict("GHANEPS_SOURCE_LINEAGE_INCOMPLETE",
                    "The sent RFQ has no authoritative sent timestamp.");
        }
        ProcurementAwardReadinessDecision? readiness = null;
        ProcurementBidderCommunicationRegister? communication = null;
        if (eventFamily == ProcurementGhanepsEventFamily.AwardNotification)
        {
            if (!string.Equals(source.Status, "Awarded", StringComparison.OrdinalIgnoreCase) ||
                !source.AwardedAt.HasValue || source.AwardLines.Count == 0)
                throw Conflict("GHANEPS_SOURCE_NOT_AWARDED",
                    "The RFQ requires retained award lines and an award timestamp.");
            (readiness, communication) = await ResolveAwardLineageAsync(
                ProcurementAwardReadinessSourceType.RequestForQuotation, source.Id,
                source.RfqNumber,
                new AwardIdentity(
                    ProcurementBidderCommunicationAwardFamily.RequestForQuotation,
                    source.Id,
                    $"{source.RfqNumber}-AWARD",
                    EnsureUtc(source.AwardedAt.Value)),
                cancellationToken);
        }
        var occurredAt = eventFamily == ProcurementGhanepsEventFamily.AwardNotification
            ? source.AwardedAt!.Value
            : source.SentAt ?? source.CreatedAt;
        var snapshot = Serialize(new
        {
            source.Id,
            source.RfqNumber,
            source.Title,
            source.Status,
            source.SentAt,
            source.SubmissionDeadline,
            source.Currency,
            source.EstimatedValue,
            source.SourcingCaseId,
            source.AwardedAt,
            awards = source.AwardLines.OrderBy(item => item.RfqItemId).Select(item => new
            {
                item.Id,
                item.RfqItemId,
                item.QuoteId,
                item.BusinessPartnerId,
                item.LineTotal
            }),
            readiness = readiness is null ? null : new
            {
                readiness.Id,
                readiness.DecisionSequence,
                readiness.IntegrityHash,
                readiness.SourceIntegrityHash
            },
            communication = communication is null ? null : new
            {
                communication.Id,
                communication.IntegrityHash,
                communication.RecipientSnapshotHash,
                communication.AwardReference
            }
        });
        return new ResolvedSource(ProcurementGhanepsSourceType.RequestForQuotation,
            source.Id, source.RfqNumber, "RequestForQuotation", EnsureUtc(occurredAt),
            snapshot, Hash(snapshot));
    }

    private async Task<ResolvedSource> ResolveTenderSourceAsync(
        Guid sourceId,
        bool exceptionalRequested,
        ProcurementGhanepsEventFamily? eventFamily,
        CancellationToken cancellationToken)
    {
        var tender = await _unitOfWork.Repository<Tender>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.Id == sourceId && !item.IsDeleted)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("GHANEPS_SOURCE_NOT_FOUND",
                "The tender source was not found in the current tenant.");
        var formal = await _unitOfWork.Repository<ProcurementTenderControl>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.TenderId == sourceId && !item.IsDeleted)
            .AsNoTracking().ToListAsync(cancellationToken);
        var exceptional = await _unitOfWork.Repository<ProcurementExceptionalSourcingControl>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.TenderId == sourceId && !item.IsDeleted)
            .AsNoTracking().ToListAsync(cancellationToken);
        if (formal.Count > 1 || exceptional.Count > 1 || formal.Count > 0 && exceptional.Count > 0)
            throw Conflict("GHANEPS_SOURCE_LINEAGE_AMBIGUOUS",
                "The tender has ambiguous formal or exceptional sourcing controls.");
        if (exceptionalRequested && exceptional.Count != 1)
            throw NotFound("GHANEPS_EXCEPTIONAL_SOURCE_NOT_FOUND",
                "The source is not an exceptional-sourcing tender in the current tenant.");
        if (!exceptionalRequested && exceptional.Count != 0)
            throw Validation("GHANEPS_SOURCE_TYPE_MISMATCH",
                "This source must be addressed as ExceptionalSourcing.");

        var sourceType = exceptionalRequested
            ? ProcurementGhanepsSourceType.ExceptionalSourcing
            : ProcurementGhanepsSourceType.Tender;
        var variant = exceptionalRequested ? "ExceptionalSourcing" :
            formal.Count == 1 ? "FormalTender" : "LegacyTender";
        DateTime occurredAt;
        object lineage;
        AwardIdentity? awardIdentity = null;
        if (exceptionalRequested)
        {
            var control = exceptional.Single();
            if ((eventFamily is ProcurementGhanepsEventFamily.TenderPublication or
                ProcurementGhanepsEventFamily.TenderReference) &&
                control.Status < ProcurementExceptionalSourcingControlStatus.Approved)
                throw Conflict("GHANEPS_SOURCE_NOT_PUBLISHED",
                    "Exceptional sourcing must be approved before its tender reference can be exchanged.");
            if (eventFamily == ProcurementGhanepsEventFamily.AwardNotification &&
                (control.Status < ProcurementExceptionalSourcingControlStatus.Awarded ||
                 !control.AwardedAtUtc.HasValue ||
                 !control.AwardBidId.HasValue ||
                 string.IsNullOrWhiteSpace(control.AwardReference)))
                throw Conflict("GHANEPS_SOURCE_NOT_AWARDED",
                    "Exceptional sourcing must have a complete retained award.");
            occurredAt = eventFamily == ProcurementGhanepsEventFamily.AwardNotification
                ? control.AwardedAtUtc!.Value
                : control.ApprovedAtUtc ?? control.PreparedAtUtc;
            if (eventFamily == ProcurementGhanepsEventFamily.AwardNotification)
                awardIdentity = new AwardIdentity(
                    ProcurementBidderCommunicationAwardFamily.ExceptionalSourcing,
                    control.Id, control.AwardReference!,
                    EnsureUtc(control.AwardedAtUtc!.Value));
            lineage = new
            {
                control.Id,
                control.Status,
                control.Method,
                control.MethodRuleId,
                control.ExceptionRuleId,
                control.AuthorityRouteId,
                control.ApprovedAtUtc,
                control.AwardBidId,
                control.AwardReference,
                control.AwardEvidenceReference,
                control.AwardedAtUtc,
                control.IntegrityHash
            };
        }
        else if (formal.Count == 1)
        {
            var control = formal.Single();
            if ((eventFamily is ProcurementGhanepsEventFamily.TenderPublication or
                ProcurementGhanepsEventFamily.TenderReference) &&
                (control.Status < ProcurementTenderControlStatus.Advertised ||
                 control.AdvertisedAtUtc == default ||
                 string.IsNullOrWhiteSpace(control.AdvertisementReference) ||
                 string.IsNullOrWhiteSpace(control.AdvertisementEvidenceReference) ||
                 !IsHash(control.IntegrityHash) ||
                 (!string.Equals(tender.Status, "Published", StringComparison.OrdinalIgnoreCase) &&
                  !string.Equals(tender.Status, "Closed", StringComparison.OrdinalIgnoreCase) &&
                  !string.Equals(tender.Status, "Awarded", StringComparison.OrdinalIgnoreCase))))
                throw Conflict("GHANEPS_SOURCE_NOT_PUBLISHED",
                    "The formal tender has no complete authoritative advertised publication lineage.");
            if (eventFamily == ProcurementGhanepsEventFamily.AwardNotification &&
                (control.Status < ProcurementTenderControlStatus.Awarded ||
                 !control.AwardedAtUtc.HasValue ||
                 !control.AwardBidId.HasValue ||
                 string.IsNullOrWhiteSpace(control.AwardReference)))
                throw Conflict("GHANEPS_SOURCE_NOT_AWARDED",
                    "The formal tender must have a complete retained award.");
            occurredAt = eventFamily == ProcurementGhanepsEventFamily.AwardNotification
                ? control.AwardedAtUtc!.Value
                : control.AdvertisedAtUtc;
            if (eventFamily == ProcurementGhanepsEventFamily.AwardNotification)
                awardIdentity = new AwardIdentity(
                    ProcurementBidderCommunicationAwardFamily.FormalTender,
                    control.Id, control.AwardReference!,
                    EnsureUtc(control.AwardedAtUtc!.Value));
            lineage = new
            {
                control.Id,
                control.Status,
                control.Method,
                control.MethodRuleId,
                control.AuthorityRouteId,
                control.AdvertisementReference,
                control.AdvertisementEvidenceReference,
                control.AdvertisedAtUtc,
                control.AwardBidId,
                control.AwardReference,
                control.AwardEvidenceReference,
                control.AwardedAtUtc,
                control.IntegrityHash
            };
        }
        else
        {
            if ((eventFamily is ProcurementGhanepsEventFamily.TenderPublication or
                ProcurementGhanepsEventFamily.TenderReference) &&
                !string.Equals(tender.Status, "Published", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(tender.Status, "Closed", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(tender.Status, "Awarded", StringComparison.OrdinalIgnoreCase))
                throw Conflict("GHANEPS_SOURCE_NOT_PUBLISHED",
                    "The legacy tender must be Published before exchange.");
            var awards = await _unitOfWork.Repository<TenderAward>()
                .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                      item.TenderId == sourceId && !item.IsDeleted &&
                                      item.Status != "Cancelled")
                .AsNoTracking().ToListAsync(cancellationToken);
            if (eventFamily == ProcurementGhanepsEventFamily.AwardNotification &&
                (!string.Equals(tender.Status, "Awarded", StringComparison.OrdinalIgnoreCase) ||
                 awards.Count == 0))
                throw Conflict("GHANEPS_SOURCE_NOT_AWARDED",
                    "The legacy tender must have at least one retained non-cancelled award.");
            occurredAt = eventFamily == ProcurementGhanepsEventFamily.AwardNotification
                ? tender.AwardDate ?? awards.Max(item => item.AwardDate)
                : tender.PublishDate ?? tender.CreatedAt;
            if (eventFamily == ProcurementGhanepsEventFamily.AwardNotification)
                awardIdentity = new AwardIdentity(
                    ProcurementBidderCommunicationAwardFamily.LegacyTenderAward,
                    tender.Id, $"{tender.TenderNumber}-LEGACY-AWARD",
                    EnsureUtc(occurredAt));
            lineage = new
            {
                tender.Status,
                tender.PublishDate,
                tender.AwardDate,
                awards = awards.OrderBy(item => item.Id).Select(item => new
                {
                    item.Id,
                    item.TenderBidId,
                    item.BusinessPartnerId,
                    item.AwardedAmount,
                    item.Currency,
                    item.AwardDate,
                    item.Status
                })
            };
        }

        ProcurementAwardReadinessDecision? readiness = null;
        ProcurementBidderCommunicationRegister? communication = null;
        if (eventFamily == ProcurementGhanepsEventFamily.AwardNotification)
        {
            var readinessType = exceptionalRequested
                ? ProcurementAwardReadinessSourceType.ExceptionalSourcing
                : ProcurementAwardReadinessSourceType.Tender;
            (readiness, communication) = await ResolveAwardLineageAsync(readinessType,
                sourceId, tender.TenderNumber,
                awardIdentity ?? throw Conflict("GHANEPS_AWARD_LINEAGE_STALE",
                    "The authoritative award identity could not be derived."),
                cancellationToken);
        }
        var snapshot = Serialize(new
        {
            tender.Id,
            tender.TenderNumber,
            tender.Title,
            tender.TenderType,
            tender.Status,
            tender.Currency,
            tender.EstimatedValue,
            tender.SourcingCaseId,
            variant,
            lineage,
            readiness = readiness is null ? null : new
            {
                readiness.Id,
                readiness.DecisionSequence,
                readiness.IntegrityHash,
                readiness.SourceIntegrityHash
            },
            communication = communication is null ? null : new
            {
                communication.Id,
                communication.IntegrityHash,
                communication.RecipientSnapshotHash,
                communication.AwardReference
            }
        });
        return new ResolvedSource(sourceType, tender.Id, tender.TenderNumber, variant,
            EnsureUtc(occurredAt), snapshot, Hash(snapshot));
    }

    private async Task<(ProcurementAwardReadinessDecision,
        ProcurementBidderCommunicationRegister)> ResolveAwardLineageAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        string sourceReference,
        AwardIdentity awardIdentity,
        CancellationToken cancellationToken)
    {
        var readiness = await _unitOfWork.Repository<ProcurementAwardReadinessDecision>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.SourceType == sourceType &&
                                  item.SourceId == sourceId &&
                                  !item.IsDeleted)
            .AsNoTracking()
            .OrderByDescending(item => item.DecisionSequence)
            .ToListAsync(cancellationToken);
        if (readiness.Count == 0)
            throw Conflict("GHANEPS_AWARD_READINESS_MISSING",
                "A retained award-readiness decision is required for award notification.");
        var sequences = readiness.Select(item => item.DecisionSequence)
            .OrderBy(value => value).ToList();
        if (sequences.Distinct().Count() != sequences.Count ||
            !sequences.SequenceEqual(Enumerable.Range(1, sequences.Count)))
            throw Conflict("GHANEPS_AWARD_READINESS_AMBIGUOUS",
                "The award-readiness decision sequence is duplicate or non-contiguous.");
        if (readiness[0].Status != ProcurementAwardReadinessDecisionStatus.Ready ||
            !IsHash(readiness[0].IntegrityHash) ||
            !IsHash(readiness[0].SourceIntegrityHash))
            throw Conflict("GHANEPS_AWARD_READINESS_STALE",
                "The exact latest award-readiness decision is not Ready or has invalid integrity.");
        var decision = readiness[0];
        var communication = await _unitOfWork.Repository<ProcurementBidderCommunicationRegister>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                                  item.SourceType == sourceType &&
                                  item.SourceId == sourceId &&
                                  !item.IsDeleted)
            .AsNoTracking().ToListAsync(cancellationToken);
        if (communication.Count != 1)
            throw Conflict("GHANEPS_BIDDER_COMMUNICATION_MISSING",
                "Exactly one bidder-communication register is required for award notification.");
        var register = communication[0];
        if (register.AwardReadinessDecisionId != decision.Id ||
            register.AwardReadinessDecisionSequence != decision.DecisionSequence ||
            !string.Equals(register.AwardReadinessIntegrityHash, decision.IntegrityHash,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(register.AwardReadinessSourceIntegrityHash,
                decision.SourceIntegrityHash, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(register.SourceReference, sourceReference, StringComparison.Ordinal) ||
            register.AwardFamily != awardIdentity.Family ||
            register.AwardId != awardIdentity.Id ||
            !string.Equals(register.AwardReference, awardIdentity.Reference,
                StringComparison.Ordinal) ||
            EnsureUtc(register.AwardedAtUtc) != awardIdentity.AwardedAtUtc ||
            !IsHash(register.IntegrityHash))
            throw Conflict("GHANEPS_AWARD_LINEAGE_STALE",
                "The bidder-communication register does not match the latest Ready award lineage.");
        return (decision, register);
    }

    private IQueryable<ProcurementGhanepsExchangeEvent> EventQuery() =>
        Events.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Payloads.Where(value => !value.IsDeleted))
            .Include(item => item.Attempts.Where(value => !value.IsDeleted))
            .Include(item => item.Acknowledgements.Where(value => !value.IsDeleted))
            .Include(item => item.Reconciliations.Where(value => !value.IsDeleted));

    private IQueryable<ProcurementGhanepsExchangeAttempt> AttemptQuery() =>
        Attempts.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted);
    private IQueryable<ProcurementGhanepsExchangeAcknowledgement> AcknowledgementQuery() =>
        Acknowledgements.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted);
    private IQueryable<ProcurementGhanepsExchangeReconciliation> ReconciliationQuery() =>
        Reconciliations.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted);

    private async Task<ProcurementGhanepsExchangeEvent> LoadEventAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await EventQuery().AsNoTracking().SingleOrDefaultAsync(item => item.Id == id,
            cancellationToken)
        ?? throw NotFound("GHANEPS_EXCHANGE_NOT_FOUND",
            "The exchange event was not found in the current tenant.");

    private async Task<ProcurementGhanepsExchangeEvent> LoadEventTrackedAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await EventQuery().SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw NotFound("GHANEPS_EXCHANGE_NOT_FOUND",
            "The exchange event was not found in the current tenant.");

    private async Task<ProcurementGhanepsExchangeEventDto> LoadAndMapAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await LoadEventAsync(id, cancellationToken);
        var capabilities = await GetCapabilitiesAsync(item.SourceReference,
            item.AcknowledgementPermissionCode, item.ReconciliationPermissionCode,
            cancellationToken);
        return MapEvent(item, capabilities);
    }

    private ProcurementGhanepsExchangePayload CreatePayload(
        ProcurementGhanepsExchangeEvent item,
        string normalizedPayload,
        string contentType,
        string? fileName,
        string? evidenceReference,
        string idempotencyKey,
        int version)
    {
        Require(contentType, "GHANEPS_CONTENT_TYPE_REQUIRED", "The payload content type is required.");
        RequireMaximum(contentType, 100, "GHANEPS_CONTENT_TYPE_TOO_LONG");
        RequireMaximum(fileName, 260, "GHANEPS_FILE_NAME_TOO_LONG");
        RequireMaximum(evidenceReference, 500, "GHANEPS_EVIDENCE_REFERENCE_TOO_LONG");
        var payload = new ProcurementGhanepsExchangePayload
        {
            TenantId = item.TenantId,
            ExchangeEventId = item.Id,
            Version = version,
            Direction = item.Direction,
            TemplateReference = JsonSerializer.Deserialize<ProcurementGhanepsConfiguredMappingDto>(
                item.MappingSnapshotJson, JsonOptions)!.TemplateReference.Trim(),
            SchemaReference = JsonSerializer.Deserialize<ProcurementGhanepsConfiguredMappingDto>(
                item.MappingSnapshotJson, JsonOptions)!.SchemaReference.Trim(),
            ExternalPayloadVersion = JsonSerializer.Deserialize<ProcurementGhanepsConfiguredMappingDto>(
                item.MappingSnapshotJson, JsonOptions)!.PayloadVersion.Trim(),
            ContentType = contentType.Trim(),
            FileName = TrimOrNull(fileName, 260),
            PayloadContent = normalizedPayload,
            PayloadChecksumSha256 = Hash(normalizedPayload),
            IdempotencyKey = idempotencyKey.Trim(),
            RecordedByUserId = _currentUser.UserId,
            RecordedByName = ActorName(),
            RecordedAtUtc = DateTime.UtcNow,
            EvidenceReference = TrimOrNull(evidenceReference, 500)
        };
        payload.IntegrityHash = Hash(Serialize(new
        {
            payload.TenantId,
            payload.ExchangeEventId,
            payload.Version,
            payload.Direction,
            payload.TemplateReference,
            payload.SchemaReference,
            payload.ExternalPayloadVersion,
            payload.ContentType,
            payload.FileName,
            payload.PayloadChecksumSha256,
            payload.RecordedByUserId,
            payload.RecordedAtUtc,
            payload.EvidenceReference
        }));
        return payload;
    }

    private ProcurementGhanepsExchangeAttempt CreateAttempt(
        ProcurementGhanepsExchangeEvent item,
        ProcurementGhanepsExchangePayload payload,
        ProcurementGhanepsAttemptOutcome outcome,
        bool isRetry,
        Guid? supersedesAttemptId,
        string? transportReference,
        string? failureCode,
        string? failureMessage,
        string? evidenceReference,
        string idempotencyKey,
        string requestFingerprint)
    {
        var attempt = new ProcurementGhanepsExchangeAttempt
        {
            TenantId = item.TenantId,
            ExchangeEventId = item.Id,
            PayloadId = payload.Id,
            AttemptNumber = item.Attempts.Count + 1,
            IsRetry = isRetry,
            SupersedesAttemptId = supersedesAttemptId,
            Outcome = outcome,
            TransportReference = TrimOrNull(transportReference, 200),
            FailureCode = TrimOrNull(failureCode, 100),
            FailureMessage = TrimOrNull(failureMessage, 2000),
            PayloadChecksumSha256 = payload.PayloadChecksumSha256,
            RequestFingerprint = requestFingerprint,
            IdempotencyKey = idempotencyKey.Trim(),
            AttemptedByUserId = _currentUser.UserId,
            AttemptedByName = ActorName(),
            AttemptedAtUtc = DateTime.UtcNow,
            EvidenceReference = TrimOrNull(evidenceReference, 500)
        };
        attempt.IntegrityHash = Hash(Serialize(new
        {
            attempt.TenantId,
            attempt.ExchangeEventId,
            attempt.PayloadId,
            attempt.AttemptNumber,
            attempt.IsRetry,
            attempt.SupersedesAttemptId,
            attempt.Outcome,
            attempt.TransportReference,
            attempt.FailureCode,
            attempt.FailureMessage,
            attempt.PayloadChecksumSha256,
            attempt.RequestFingerprint,
            attempt.AttemptedByUserId,
            attempt.AttemptedAtUtc,
            attempt.EvidenceReference
        }));
        return attempt;
    }

    private static ProcurementGhanepsExchangeStatus StatusAfterAttempt(
        ProcurementGhanepsExchangeEvent item,
        ProcurementGhanepsAttemptOutcome outcome)
    {
        if (outcome == ProcurementGhanepsAttemptOutcome.Failed)
            return ProcurementGhanepsExchangeStatus.Failed;
        return item.AcknowledgementRequired
            ? ProcurementGhanepsExchangeStatus.PendingAcknowledgement
            : ProcurementGhanepsExchangeStatus.Transferred;
    }

    private async Task RecordControlEventAsync(
        ProcurementGhanepsExchangeEvent item,
        string action,
        ProcurementControlEventResult result,
        string idempotencyKey,
        object values,
        string? reason,
        string? evidenceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var evidence = new List<ProcurementControlEventEvidenceReference>();
        if (!string.IsNullOrWhiteSpace(evidenceReference))
            evidence.Add(new ProcurementControlEventEvidenceReference
            {
                ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
                Reference = evidenceReference.Trim(),
                Label = "GHANEPS exchange evidence",
                RequirementKey = "DEC-009"
            });
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("ghaneps-exchange",
                item.TenantId, item.Id, action, idempotencyKey),
            EventType = EventType,
            Action = action,
            Result = result,
            RuleCode = "DEC-009",
            RuleId = item.ConfigurationDecisionId,
            RuleVersion = item.ConfigurationProfileVersion.ToString(),
            DecisionKeys = ["DEC-009"],
            SourceType = item.SourceType.ToString(),
            SourceId = item.SourceId,
            SourceReference = item.SourceReference,
            Reason = TrimOrNull(reason, 1000),
            InputValues = new
            {
                item.EventFamily,
                item.Direction,
                item.MappingKey,
                item.ExternalEventCode,
                item.EventReference,
                item.PayloadContentType,
                item.AcknowledgementContentType,
                item.AcknowledgementPermissionCode,
                item.ReconciliationPermissionCode,
                item.SourceIntegrityHash,
                item.ConfigurationProfileId,
                item.ConfigurationProfileCode,
                item.ConfigurationProfileVersion,
                item.ConfigurationDecisionId,
                item.ConfigurationValueHash,
                item.MappingIntegrityHash,
                actorUserId = _currentUser.UserId,
                actorRoles = _currentUser.Roles.OrderBy(value => value)
            },
            ResultValues = values,
            CorrelationId = correlationId,
            CausationId = item.CorrelationId,
            OccurredAtUtc = DateTime.UtcNow,
            Evidence = evidence
        }, cancellationToken);
    }

    private async Task<T> ExecuteMutationAsync<T>(
        DenialContext context,
        string correlationId,
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            // Every mutation core uses an explicit transaction so the entire
            // operation must run inside EF's configured retry strategy. Keeping
            // this at the shared boundary covers create/import/attempt/retry/
            // acknowledgement/reconciliation without duplicating strategy code.
            return await _unitOfWork.ExecuteInStrategyAsync(action, cancellationToken);
        }
        catch (Exception exception) when (IsAuditableDenial(exception))
        {
            _unitOfWork.ClearTrackedChanges();
            await RecordDeniedControlEventAsync(context, correlationId, exception,
                cancellationToken);
            throw;
        }
    }

    private async Task RecordDeniedControlEventAsync(
        DenialContext context,
        string correlationId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.TenantId == Guid.Empty ||
            _currentUser.UserId == Guid.Empty)
            return;

        ProcurementGhanepsExchangeEvent? item = null;
        if (context.ExchangeEventId.HasValue)
        {
            item = await Events.GetQueryable(value =>
                    value.TenantId == _currentUser.TenantId &&
                    value.Id == context.ExchangeEventId.Value &&
                    !value.IsDeleted)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
        }

        ProcurementConfigurationDecision? decision = null;
        ProcurementConfigurationProfile? profile = null;
        if (item is not null)
        {
            decision = await _unitOfWork.Repository<ProcurementConfigurationDecision>()
                .GetQueryable(value =>
                    value.TenantId == _currentUser.TenantId &&
                    value.Id == item.ConfigurationDecisionId &&
                    value.ProfileId == item.ConfigurationProfileId &&
                    !value.IsDeleted)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            profile = decision is null
                ? null
                : await _unitOfWork.Repository<ProcurementConfigurationProfile>()
                    .GetQueryable(value =>
                        value.TenantId == _currentUser.TenantId &&
                        value.Id == decision.ProfileId &&
                        !value.IsDeleted)
                    .AsNoTracking()
                    .SingleOrDefaultAsync(cancellationToken);
        }
        else
        {
            try
            {
                var resolved = await ResolveProfileAsync(DateTime.UtcNow, cancellationToken);
                decision = resolved.Decision;
                profile = resolved.Profile;
            }
            catch (Exception resolutionException) when (
                IsAuditableDenial(resolutionException))
            {
                // A denied decision must never claim ambiguous, ineffective, or invalid DEC-009 lineage.
            }
        }

        var code = DenialCode(exception);
        var sourceType = item?.SourceType.ToString() ?? context.SourceType;
        var sourceId = item?.SourceId ?? context.SourceId ?? context.ExchangeEventId;
        var sourceReference = item?.SourceReference;
        if (string.IsNullOrWhiteSpace(sourceReference))
            sourceReference = string.IsNullOrWhiteSpace(context.SourceReference)
                ? (sourceId ?? _currentUser.UserId).ToString()
                : context.SourceReference.Trim();
        var normalizedCorrelation = string.IsNullOrWhiteSpace(correlationId)
            ? $"ghaneps-denied-{Guid.NewGuid():N}"
            : correlationId.Trim();
        if (normalizedCorrelation.Length > 100)
            normalizedCorrelation = normalizedCorrelation[..100];
        var denialOccurrenceId = Guid.NewGuid();
        var auditSourceType = TrimOrNull(sourceType, 100) ?? EventType;
        var auditRequestedSourceReference = TrimOrNull(context.SourceReference, 500);
        var auditMappingKey = TrimOrNull(context.MappingKey, 100);
        var auditIdempotencyKey = TrimOrNull(context.IdempotencyKey, 100);
        var auditEvidenceReference = TrimOrNull(context.EvidenceReference, 200);
        var evidence = new List<ProcurementControlEventEvidenceReference>();
        if (auditEvidenceReference is not null)
            evidence.Add(new ProcurementControlEventEvidenceReference
            {
                ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
                Reference = auditEvidenceReference,
                Label = "Denied GHANEPS exchange evidence",
                RequirementKey = "DEC-009"
            });

        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("ghaneps-exchange-denied",
                _currentUser.TenantId, context.Action, sourceId, context.ExchangeEventId,
                context.IdempotencyKey?.Trim(), code, normalizedCorrelation,
                denialOccurrenceId),
            EventType = EventType,
            Action = $"{context.Action}Denied",
            Result = ProcurementControlEventResult.Denied,
            RuleCode = "DEC-009",
            RuleId = decision?.Id ?? item?.ConfigurationDecisionId,
            RuleVersion = profile?.Version.ToString() ??
                          (item is null ? null : item.ConfigurationProfileVersion.ToString()),
            DecisionKeys = ["DEC-009"],
            SourceType = auditSourceType,
            SourceId = sourceId,
            SourceReference = sourceReference.Length <= 500
                ? sourceReference
                : sourceReference[..500],
            Reason = TrimOrNull($"{code}: {exception.Message}", 1000),
            InputValues = new
            {
                route = context.Action,
                context.ExchangeEventId,
                requestedSourceType = context.SourceType,
                context.SourceId,
                requestedSourceReference = auditRequestedSourceReference,
                context.EventFamily,
                mappingKey = auditMappingKey,
                idempotencyKey = auditIdempotencyKey,
                evidenceReference = auditEvidenceReference,
                denialOccurrenceId,
                actorUserId = _currentUser.UserId,
                actorRoles = _currentUser.Roles.OrderBy(value => value),
                _currentUser.IsExternalUser
            },
            ResultValues = new
            {
                denied = true,
                code,
                exceptionType = exception.GetType().Name
            },
            CorrelationId = normalizedCorrelation,
            CausationId = item?.CorrelationId,
            OccurredAtUtc = DateTime.UtcNow,
            Evidence = evidence
        }, cancellationToken);
    }

    private static bool IsAuditableDenial(Exception exception) =>
        exception is ProcurementGhanepsExchangeNotFoundException or
            ProcurementGhanepsExchangeConflictException or
            ProcurementGhanepsExchangeValidationException or
            ProcurementGhanepsExchangeAuthorizationException;

    private static string DenialCode(Exception exception) => exception switch
    {
        ProcurementGhanepsExchangeNotFoundException value => value.Code,
        ProcurementGhanepsExchangeConflictException value => value.Code,
        ProcurementGhanepsExchangeValidationException value => value.Code,
        ProcurementGhanepsExchangeAuthorizationException value => value.Code,
        _ => "GHANEPS_EXCHANGE_DENIED"
    };

    private Task PublishOperationalAsync(
        ProcurementGhanepsExchangeEvent item,
        string topic,
        object values,
        CancellationToken cancellationToken)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(
            Serialize(values), JsonOptions) ?? new Dictionary<string, object>();
        data["exchangeEventId"] = item.Id;
        data["sourceType"] = item.SourceType.ToString();
        data["sourceId"] = item.SourceId;
        data["sourceReference"] = item.SourceReference;
        data["eventFamily"] = item.EventFamily.ToString();
        data["eventReference"] = item.EventReference;
        data["status"] = item.Status.ToString();
        return _notifications.PublishAsync(new NotificationTopicEvent
        {
            TenantId = item.TenantId,
            TopicKey = topic,
            NotificationType = EventType,
            EntityType = item.SourceType.ToString(),
            EntityId = item.SourceId,
            TriggeredByUserId = _currentUser.UserId,
            Data = data
        }, cancellationToken);
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureInternalReader();
        if (HasPlatformSuperAdministratorBypass()) return;
        var decision = await _accessControl.EnforceCapabilityAsync(
            new ProcurementAccessCapabilityRequest
            {
                PermissionCode = permission,
                SourceType = EventType,
                SourceReference = sourceReference
            }, correlationId, cancellationToken);
        if (!decision.Allowed) throw Authorization(decision.Message);
    }

    private async Task<Capabilities> GetCapabilitiesAsync(
        string sourceReference,
        CancellationToken cancellationToken) =>
        await GetCapabilitiesAsync(sourceReference, ApprovePermission, ApprovePermission,
            cancellationToken);

    private async Task<Capabilities> GetCapabilitiesAsync(
        string sourceReference,
        string acknowledgementPermissionCode,
        string reconciliationPermissionCode,
        CancellationToken cancellationToken)
    {
        if (HasPlatformSuperAdministratorBypass()) return new Capabilities(true, true, true);
        var correlation = $"ghaneps-status-{Guid.NewGuid():N}";
        var manage = await _accessControl.CheckCapabilityAsync(
            new ProcurementAccessCapabilityRequest
            {
                PermissionCode = ManagePermission,
                SourceType = EventType,
                SourceReference = sourceReference
            }, correlation, cancellationToken);
        var acknowledge = await _accessControl.CheckCapabilityAsync(
            new ProcurementAccessCapabilityRequest
            {
                PermissionCode = acknowledgementPermissionCode,
                SourceType = EventType,
                SourceReference = sourceReference
            }, correlation, cancellationToken);
        var reconcile = string.Equals(acknowledgementPermissionCode,
            reconciliationPermissionCode, StringComparison.OrdinalIgnoreCase)
            ? acknowledge
            : await _accessControl.CheckCapabilityAsync(
                new ProcurementAccessCapabilityRequest
                {
                    PermissionCode = reconciliationPermissionCode,
                    SourceType = EventType,
                    SourceReference = sourceReference
                }, correlation, cancellationToken);
        return new Capabilities(manage.Allowed, acknowledge.Allowed, reconcile.Allowed);
    }

    private static void ValidateCreateRoute(
        ProcurementGhanepsPayloadRequest request,
        ProcurementGhanepsSourceType bodySourceType,
        Guid bodySourceId)
    {
        if (!request.RouteSourceType.HasValue && !request.RouteSourceId.HasValue)
            return;
        if (!request.RouteSourceType.HasValue ||
            !request.RouteSourceId.HasValue ||
            request.RouteSourceType.Value != bodySourceType ||
            request.RouteSourceId.Value != bodySourceId)
            throw Validation(
                "GHANEPS_EXCHANGE_SOURCE_ROUTE_MISMATCH",
                "The request source must exactly match the authoritative route source.");
    }

    private static void ValidateChildRoute(
        ProcurementGhanepsExchangeEvent item,
        ProcurementGhanepsRouteBoundMutationRequest request)
    {
        if (!request.RouteSourceType.HasValue && !request.RouteSourceId.HasValue)
            return;
        if (!request.RouteSourceType.HasValue ||
            !request.RouteSourceId.HasValue ||
            request.RouteSourceType.Value != item.SourceType ||
            request.RouteSourceId.Value != item.SourceId)
            throw NotFound(
                "GHANEPS_EXCHANGE_ROUTE_RESOURCE_NOT_FOUND",
                "The requested GHANEPS exchange event does not belong to the authoritative route source.");
    }

    private async Task EnforceSodAsync(
        string sourceReference,
        IReadOnlyCollection<Guid> prohibitedActorIds,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var decision = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
        {
            ControlCode = InitiatorApproverSod,
            SourceType = EventType,
            SourceReference = sourceReference,
            ProhibitedActorUserIds = prohibitedActorIds.Where(item => item != Guid.Empty)
                .Distinct().ToList()
        }, correlationId, cancellationToken);
        if (!decision.Allowed) throw Authorization(decision.Message);
    }

    private void EnsureInternalReader()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.TenantId == Guid.Empty ||
            _currentUser.UserId == Guid.Empty)
            throw Authorization("An authenticated tenant context is required.");
        if (_currentUser.IsExternalUser)
            throw Authorization("Supplier portal users cannot access the internal GHANEPS exchange register.");
        if (HasPlatformSuperAdministratorBypass() ||
            _currentUser.HasRegisteredProcurementPermission("procurement.records.read"))
            return;
        throw Authorization("The procurement records read permission is required.");
    }

    private bool HasPlatformSuperAdministratorBypass() =>
        _currentUser.HasRole(Constants.Roles.SuperAdmin);

    private static bool MappingApplies(
        ProcurementGhanepsConfiguredMappingDto mapping,
        ResolvedSource source) =>
        mapping.SourceTypes.Contains(source.Type) &&
        (mapping.SourceVariants.Count == 0 ||
         mapping.SourceVariants.Any(value =>
             string.Equals(value, source.Variant, StringComparison.OrdinalIgnoreCase)));

    private static void ValidateMapping(ProcurementGhanepsConfiguredMappingDto mapping)
    {
        if (!Enum.IsDefined(mapping.EventFamily))
            throw Validation("GHANEPS_MAPPING_EVENT_FAMILY_INVALID",
                "The configured mapping EventFamily is not supported.");
        if (!Enum.IsDefined(mapping.Direction))
            throw Validation("GHANEPS_MAPPING_DIRECTION_INVALID",
                "The configured mapping Direction is not supported.");
        Require(mapping.MappingKey, "GHANEPS_MAPPING_KEY_REQUIRED", "MappingKey is required.");
        RequireMaximum(mapping.MappingKey, 100, "GHANEPS_MAPPING_KEY_TOO_LONG");
        Require(mapping.ExternalEventCode, "GHANEPS_EXTERNAL_EVENT_CODE_REQUIRED",
            "ExternalEventCode is required.");
        RequireMaximum(mapping.ExternalEventCode, 100, "GHANEPS_EXTERNAL_EVENT_CODE_TOO_LONG");
        Require(mapping.TemplateReference, "GHANEPS_TEMPLATE_REFERENCE_REQUIRED",
            "TemplateReference is required.");
        RequireMaximum(mapping.TemplateReference, 200, "GHANEPS_TEMPLATE_REFERENCE_TOO_LONG");
        Require(mapping.SchemaReference, "GHANEPS_SCHEMA_REFERENCE_REQUIRED",
            "SchemaReference is required.");
        RequireMaximum(mapping.SchemaReference, 200, "GHANEPS_SCHEMA_REFERENCE_TOO_LONG");
        Require(mapping.PayloadVersion, "GHANEPS_PAYLOAD_VERSION_REQUIRED",
            "PayloadVersion is required.");
        RequireMaximum(mapping.PayloadVersion, 100, "GHANEPS_PAYLOAD_VERSION_TOO_LONG");
        Require(mapping.ReferenceField, "GHANEPS_REFERENCE_FIELD_REQUIRED",
            "ReferenceField is required.");
        RequireMaximum(mapping.ReferenceField, 100, "GHANEPS_REFERENCE_FIELD_TOO_LONG");
        NormalizeContentType(mapping.PayloadContentType,
            "GHANEPS_MAPPING_PAYLOAD_CONTENT_TYPE_INVALID");
        NormalizeContentType(mapping.AcknowledgementContentType,
            "GHANEPS_MAPPING_ACKNOWLEDGEMENT_CONTENT_TYPE_INVALID");
        ValidateConfiguredPermission(mapping.AcknowledgementPermissionCode,
            "GHANEPS_MAPPING_ACKNOWLEDGEMENT_PERMISSION_INVALID");
        ValidateConfiguredPermission(mapping.ReconciliationPermissionCode,
            "GHANEPS_MAPPING_RECONCILIATION_PERMISSION_INVALID");
        if (mapping.SourceTypes is null || mapping.SourceTypes.Count == 0 ||
            mapping.SourceTypes.Distinct().Count() != mapping.SourceTypes.Count ||
            mapping.SourceTypes.Any(value => !Enum.IsDefined(value)))
            throw Validation("GHANEPS_MAPPING_SOURCE_TYPES_INVALID",
                "Every mapping requires a unique non-empty source-type list.");
        if (mapping.SourceVariants is null)
            throw Validation("GHANEPS_MAPPING_SOURCE_VARIANTS_INVALID",
                "SourceVariants must be configured as an array and cannot be null.");
        if (mapping.SourceVariants.Count > 50 ||
            mapping.SourceVariants.Any(string.IsNullOrWhiteSpace) ||
            mapping.SourceVariants.Any(value => value.Trim().Length > 50) ||
            mapping.SourceVariants.Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != mapping.SourceVariants.Count)
            throw Validation("GHANEPS_MAPPING_SOURCE_VARIANTS_INVALID",
                "Source variants must be unique non-empty values of no more than 50 characters.");
        if (mapping.MaximumRetryAttempts is < 0 or > 100)
            throw Validation("GHANEPS_MAPPING_RETRY_INVALID",
                "MaximumRetryAttempts must be between 0 and 100.");
    }

    private static void ValidateAttempt(
        ProcurementGhanepsAttemptOutcome outcome,
        string? transportReference,
        string? failureCode,
        string? failureMessage)
    {
        if (!Enum.IsDefined(outcome))
            throw Validation("GHANEPS_ATTEMPT_OUTCOME_INVALID",
                "The attempt outcome is not supported.");
        RequireMaximum(transportReference, 200, "GHANEPS_TRANSPORT_REFERENCE_TOO_LONG");
        RequireMaximum(failureCode, 100, "GHANEPS_FAILURE_CODE_TOO_LONG");
        RequireMaximum(failureMessage, 2000, "GHANEPS_FAILURE_MESSAGE_TOO_LONG");
        if (outcome == ProcurementGhanepsAttemptOutcome.Succeeded)
        {
            Require(transportReference, "GHANEPS_TRANSPORT_REFERENCE_REQUIRED",
                "A successful attempt requires a transport reference.");
            if (!string.IsNullOrWhiteSpace(failureCode) || !string.IsNullOrWhiteSpace(failureMessage))
                throw Validation("GHANEPS_ATTEMPT_OUTCOME_INVALID",
                    "A successful attempt cannot contain failure details.");
        }
        else
        {
            Require(failureCode, "GHANEPS_FAILURE_CODE_REQUIRED",
                "A failed attempt requires a failure code.");
            Require(failureMessage, "GHANEPS_FAILURE_MESSAGE_REQUIRED",
                "A failed attempt requires a failure message.");
        }
    }

    private static void ValidateConfiguredPermission(string? permissionCode, string code)
    {
        Require(permissionCode, code,
            "DEC-009 must configure an explicit registered mutation permission.");
        RequireMaximum(permissionCode, 100, code);
        var definition = ProcurementAccessControlRegistry.FindPermission(permissionCode!.Trim());
        if (definition is null || !definition.IsMutation)
            throw Validation(code,
                "The configured DEC-009 permission must be a registered procurement mutation permission.");
    }

    private static void AssertRowVersion(
        ProcurementGhanepsExchangeEvent item,
        string? expectedRowVersion)
    {
        Require(expectedRowVersion, "GHANEPS_ROW_VERSION_REQUIRED",
            "ExpectedRowVersion is required.");
        RequireMaximum(expectedRowVersion, 200, "GHANEPS_ROW_VERSION_TOO_LONG");
        byte[] expected;
        try
        {
            expected = Convert.FromBase64String(expectedRowVersion ?? string.Empty);
        }
        catch (FormatException)
        {
            throw Validation("GHANEPS_ROW_VERSION_INVALID",
                "ExpectedRowVersion must be a valid base64 row-version token.");
        }
        if (!expected.SequenceEqual(item.RowVersion))
            throw Conflict("GHANEPS_EXCHANGE_CONCURRENCY_CONFLICT",
                "The exchange event changed after it was loaded.");
    }

    private static ProcurementGhanepsExchangeMappingOptionDto MapOption(
        ProcurementGhanepsConfiguredMappingDto item) => new()
    {
        MappingKey = item.MappingKey,
        EventFamily = item.EventFamily,
        Direction = item.Direction,
        ExternalEventCode = item.ExternalEventCode,
        TemplateReference = item.TemplateReference,
        SchemaReference = item.SchemaReference,
        PayloadVersion = item.PayloadVersion,
        ReferenceField = item.ReferenceField,
        PayloadContentType = item.PayloadContentType,
        AcknowledgementContentType = item.AcknowledgementContentType,
        AcknowledgementPermissionCode = item.AcknowledgementPermissionCode,
        ReconciliationPermissionCode = item.ReconciliationPermissionCode,
        AcknowledgementRequired = item.AcknowledgementRequired,
        ReconciliationRequired = item.ReconciliationRequired,
        MaximumRetryAttempts = item.MaximumRetryAttempts
    };

    private ProcurementGhanepsExchangeEventDto MapEvent(
        ProcurementGhanepsExchangeEvent item,
        Capabilities capabilities)
    {
        var allowed = new List<string>();
        var blocked = new List<string>();
        var latestAttempt = item.Attempts.OrderByDescending(value => value.AttemptNumber).FirstOrDefault();
        var latestAcknowledgement = item.Acknowledgements
            .OrderByDescending(value => value.Sequence).FirstOrDefault();
        var actorLineage = ActorLineage(item);
        if (capabilities.Manage && item.Attempts.Count == 0 &&
            item.Direction == ProcurementGhanepsExchangeDirection.Export)
            allowed.Add("RecordAttempt");
        if (capabilities.Manage && latestAttempt is not null &&
            (latestAttempt.Outcome == ProcurementGhanepsAttemptOutcome.Failed ||
             latestAcknowledgement?.Outcome == ProcurementGhanepsAcknowledgementOutcome.Rejected &&
             latestAcknowledgement.AttemptId == latestAttempt.Id) &&
            item.Attempts.Count(value => value.IsRetry) < item.MaximumRetryAttempts)
            allowed.Add("Retry");
        var latestSuccessfulAttempt = item.Attempts
            .Where(value => value.Outcome == ProcurementGhanepsAttemptOutcome.Succeeded)
            .OrderByDescending(value => value.AttemptNumber).FirstOrDefault();
        if (capabilities.Acknowledge && !actorLineage.Contains(_currentUser.UserId) &&
            item.AcknowledgementRequired &&
            !item.Acknowledgements.Any(value =>
                value.Outcome == ProcurementGhanepsAcknowledgementOutcome.Accepted) &&
            latestSuccessfulAttempt is not null &&
            !item.Acknowledgements.Any(value => value.AttemptId == latestSuccessfulAttempt.Id))
            allowed.Add("RecordAcknowledgement");
        var latestReconciliation = item.Reconciliations
            .OrderByDescending(value => value.Sequence).FirstOrDefault();
        var canIndependentlyReconcile = !actorLineage.Contains(_currentUser.UserId) &&
            (latestReconciliation?.Outcome != ProcurementGhanepsReconciliationOutcome.Mismatch ||
             latestReconciliation.ReconciledByUserId != _currentUser.UserId);
        if (capabilities.Reconcile && canIndependentlyReconcile &&
            item.ReconciliationRequired &&
            item.Attempts.Any(value => value.Outcome == ProcurementGhanepsAttemptOutcome.Succeeded) &&
            (!item.AcknowledgementRequired || item.Acknowledgements.Any(value =>
                value.Outcome == ProcurementGhanepsAcknowledgementOutcome.Accepted)) &&
            latestReconciliation?.Outcome is not (
                ProcurementGhanepsReconciliationOutcome.Matched or
                ProcurementGhanepsReconciliationOutcome.Resolved))
            allowed.Add(latestReconciliation?.Outcome == ProcurementGhanepsReconciliationOutcome.Mismatch
                ? "ResolveReconciliation"
                : "Reconcile");
        if (!capabilities.Manage)
            blocked.Add("The current actor lacks the GHANEPS exchange management capability.");
        if (!capabilities.Acknowledge)
            blocked.Add("The current actor lacks the configured acknowledgement capability.");
        if (!capabilities.Reconcile)
            blocked.Add("The current actor lacks the configured reconciliation capability.");
        if (actorLineage.Contains(_currentUser.UserId))
            blocked.Add("An event preparer, payload recorder, or exchange-attempt actor cannot independently acknowledge or reconcile the same event.");
        if (latestAttempt?.Outcome == ProcurementGhanepsAttemptOutcome.Failed &&
            item.Attempts.Count(value => value.IsRetry) >= item.MaximumRetryAttempts)
            blocked.Add("The configured retry limit has been exhausted.");
        if (latestReconciliation?.Outcome is ProcurementGhanepsReconciliationOutcome.Matched or
            ProcurementGhanepsReconciliationOutcome.Resolved)
            blocked.Add("The reconciliation is terminal.");
        if (latestReconciliation?.Outcome == ProcurementGhanepsReconciliationOutcome.Mismatch &&
            latestReconciliation.ReconciledByUserId == _currentUser.UserId)
            blocked.Add("The actor who recorded a mismatch cannot independently resolve it.");

        var payloads = item.Payloads.OrderBy(value => value.Version).Select(MapPayload).ToList();
        var attempts = item.Attempts.OrderBy(value => value.AttemptNumber).Select(MapAttempt).ToList();
        var acknowledgements = item.Acknowledgements.OrderBy(value => value.Sequence)
            .Select(MapAcknowledgement).ToList();
        var reconciliations = item.Reconciliations.OrderBy(value => value.Sequence)
            .Select(MapReconciliation).ToList();
        var history = new List<ProcurementGhanepsExchangeHistoryItemDto>
        {
            History(item, "Exchange", item.Id, 0, "Prepared", item.EventReference,
                item.EvidenceReference, item.PreparedByUserId, item.PreparedByName,
                item.PreparedAtUtc, item.IntegrityHash)
        };
        history.AddRange(item.Payloads.Select(value =>
            History(item, "Payload", value.Id, value.Version, value.Direction.ToString(),
                value.PayloadChecksumSha256, value.EvidenceReference, value.RecordedByUserId,
                value.RecordedByName, value.RecordedAtUtc, value.IntegrityHash)));
        history.AddRange(item.Attempts.Select(value =>
            History(item, value.IsRetry ? "RetryAttempt" : "Attempt", value.Id,
                value.AttemptNumber, value.Outcome.ToString(), value.TransportReference,
                value.EvidenceReference, value.AttemptedByUserId, value.AttemptedByName,
                value.AttemptedAtUtc, value.IntegrityHash)));
        history.AddRange(item.Acknowledgements.Select(value =>
            History(item, "Acknowledgement", value.Id, value.Sequence, value.Outcome.ToString(),
                value.AcknowledgementReference, value.EvidenceReference,
                value.AcknowledgedByUserId, value.AcknowledgedByName,
                value.AcknowledgedAtUtc, value.IntegrityHash)));
        history.AddRange(item.Reconciliations.Select(value =>
            History(item, "Reconciliation", value.Id, value.Sequence, value.Outcome.ToString(),
                value.ActualReference, value.EvidenceReference, value.ReconciledByUserId,
                value.ReconciledByName, value.ReconciledAtUtc, value.IntegrityHash)));

        return new ProcurementGhanepsExchangeEventDto
        {
            Id = item.Id,
            SourceType = item.SourceType,
            SourceId = item.SourceId,
            SourceReference = item.SourceReference,
            SourceVariant = item.SourceVariant,
            SourceOccurredAtUtc = item.SourceOccurredAtUtc,
            SourceIntegrityHash = item.SourceIntegrityHash,
            EventFamily = item.EventFamily,
            Direction = item.Direction,
            MappingKey = item.MappingKey,
            ExternalEventCode = item.ExternalEventCode,
            EventReference = item.EventReference,
            ReferenceField = item.ReferenceField,
            PayloadContentType = item.PayloadContentType,
            AcknowledgementContentType = item.AcknowledgementContentType,
            AcknowledgementPermissionCode = item.AcknowledgementPermissionCode,
            ReconciliationPermissionCode = item.ReconciliationPermissionCode,
            ConfigurationProfileId = item.ConfigurationProfileId,
            ConfigurationProfileCode = item.ConfigurationProfileCode,
            ConfigurationProfileVersion = item.ConfigurationProfileVersion,
            ConfigurationDecisionId = item.ConfigurationDecisionId,
            ExchangeProfileCode = item.ExchangeProfileCode,
            ConfigurationValueHash = item.ConfigurationValueHash,
            MappingIntegrityHash = item.MappingIntegrityHash,
            ConfigurationEffectiveFromUtc = item.ConfigurationEffectiveFromUtc,
            ConfigurationEffectiveToUtc = item.ConfigurationEffectiveToUtc,
            Frequency = item.Frequency,
            Owner = item.Owner,
            AcknowledgementRule = item.AcknowledgementRule,
            ReconciliationRule = item.ReconciliationRule,
            AcknowledgementRequired = item.AcknowledgementRequired,
            ReconciliationRequired = item.ReconciliationRequired,
            MaximumRetryAttempts = item.MaximumRetryAttempts,
            Status = item.Status,
            RequestFingerprint = item.RequestFingerprint,
            CorrelationId = item.CorrelationId,
            PreparedByUserId = item.PreparedByUserId,
            PreparedByName = item.PreparedByName,
            PreparedAtUtc = item.PreparedAtUtc,
            EvidenceReference = item.EvidenceReference,
            IntegrityHash = item.IntegrityHash,
            RowVersion = Convert.ToBase64String(item.RowVersion),
            AllowedActions = allowed,
            BlockedReasons = blocked.Distinct(StringComparer.Ordinal).ToList(),
            Payloads = payloads,
            Attempts = attempts,
            Acknowledgements = acknowledgements,
            Reconciliations = reconciliations,
            History = history.OrderBy(value => value.OccurredAtUtc)
                .ThenBy(value => value.Kind, StringComparer.Ordinal)
                .ThenBy(value => value.Sequence)
                .ToList()
        };
    }

    private static ProcurementGhanepsExchangeHistoryItemDto History(
        ProcurementGhanepsExchangeEvent item,
        string kind,
        Guid recordId,
        int sequence,
        string outcome,
        string? reference,
        string? evidenceReference,
        Guid actorUserId,
        string actorName,
        DateTime occurredAtUtc,
        string integrityHash) => new()
    {
        ExchangeEventId = item.Id,
        EventFamily = item.EventFamily,
        EventReference = item.EventReference,
        Kind = kind,
        RecordId = recordId,
        Sequence = sequence,
        Outcome = outcome,
        Reference = reference,
        EvidenceReference = evidenceReference,
        ActorUserId = actorUserId,
        ActorName = actorName,
        OccurredAtUtc = occurredAtUtc,
        IntegrityHash = integrityHash
    };

    private static ProcurementGhanepsExchangePayloadDto MapPayload(
        ProcurementGhanepsExchangePayload item) => new()
    {
        Id = item.Id,
        Version = item.Version,
        Direction = item.Direction,
        TemplateReference = item.TemplateReference,
        SchemaReference = item.SchemaReference,
        ExternalPayloadVersion = item.ExternalPayloadVersion,
        ContentType = item.ContentType,
        FileName = item.FileName,
        PayloadContent = item.PayloadContent,
        PayloadChecksumSha256 = item.PayloadChecksumSha256,
        RecordedAtUtc = item.RecordedAtUtc,
        RecordedByUserId = item.RecordedByUserId,
        RecordedByName = item.RecordedByName,
        EvidenceReference = item.EvidenceReference,
        IntegrityHash = item.IntegrityHash
    };

    private static ProcurementGhanepsExchangeAttemptDto MapAttempt(
        ProcurementGhanepsExchangeAttempt item) => new()
    {
        Id = item.Id,
        PayloadId = item.PayloadId,
        AttemptNumber = item.AttemptNumber,
        IsRetry = item.IsRetry,
        SupersedesAttemptId = item.SupersedesAttemptId,
        Outcome = item.Outcome,
        TransportReference = item.TransportReference,
        FailureCode = item.FailureCode,
        FailureMessage = item.FailureMessage,
        RequestFingerprint = item.RequestFingerprint,
        AttemptedAtUtc = item.AttemptedAtUtc,
        AttemptedByUserId = item.AttemptedByUserId,
        AttemptedByName = item.AttemptedByName,
        EvidenceReference = item.EvidenceReference,
        IntegrityHash = item.IntegrityHash
    };

    private static ProcurementGhanepsExchangeAcknowledgementDto MapAcknowledgement(
        ProcurementGhanepsExchangeAcknowledgement item) => new()
    {
        Id = item.Id,
        AttemptId = item.AttemptId,
        PayloadId = item.PayloadId,
        Sequence = item.Sequence,
        Outcome = item.Outcome,
        AcknowledgementReference = item.AcknowledgementReference,
        ExternalStatusCode = item.ExternalStatusCode,
        ContentType = item.ContentType,
        AcknowledgementContent = item.AcknowledgementContent,
        AcknowledgementChecksumSha256 = item.AcknowledgementChecksumSha256,
        RequestFingerprint = item.RequestFingerprint,
        AcknowledgedAtUtc = item.AcknowledgedAtUtc,
        AcknowledgedByUserId = item.AcknowledgedByUserId,
        AcknowledgedByName = item.AcknowledgedByName,
        EvidenceReference = item.EvidenceReference,
        IntegrityHash = item.IntegrityHash
    };

    private static List<Guid> ActorLineage(ProcurementGhanepsExchangeEvent item) =>
        new[] { item.PreparedByUserId }
            .Concat(item.Payloads.Select(value => value.RecordedByUserId))
            .Concat(item.Attempts.Select(value => value.AttemptedByUserId))
            .Concat(item.Acknowledgements.Select(value => value.AcknowledgedByUserId))
            .Where(value => value != Guid.Empty)
            .Distinct()
            .ToList();

    private static ProcurementGhanepsExchangeReconciliationDto MapReconciliation(
        ProcurementGhanepsExchangeReconciliation item) => new()
    {
        Id = item.Id,
        AttemptId = item.AttemptId,
        PayloadId = item.PayloadId,
        Sequence = item.Sequence,
        Outcome = item.Outcome,
        ExpectedReference = item.ExpectedReference,
        ActualReference = item.ActualReference,
        ExpectedChecksumSha256 = item.ExpectedChecksumSha256,
        ActualChecksumSha256 = item.ActualChecksumSha256,
        RequestFingerprint = item.RequestFingerprint,
        Notes = item.Notes,
        ReconciledAtUtc = item.ReconciledAtUtc,
        ReconciledByUserId = item.ReconciledByUserId,
        ReconciledByName = item.ReconciledByName,
        EvidenceReference = item.EvidenceReference,
        IntegrityHash = item.IntegrityHash
    };

    private string ActorName() =>
        string.IsNullOrWhiteSpace(_currentUser.FullName)
            ? _currentUser.Username
            : _currentUser.FullName;

    private static string NormalizeJson(
        string value,
        string code,
        string duplicatePropertyCode = "GHANEPS_JSON_DUPLICATE_PROPERTY")
    {
        try
        {
            using var document = JsonDocument.Parse(value ?? string.Empty);
            ValidateNoDuplicateJsonProperties(document.RootElement,
                duplicatePropertyCode);
            return JsonSerializer.Serialize(document.RootElement, JsonOptions);
        }
        catch (JsonException)
        {
            throw Validation(code, "A valid JSON payload is required.");
        }
    }

    private static string NormalizeContent(string? value, string contentType, string codePrefix)
    {
        var normalizedType = NormalizeContentType(contentType,
            $"{codePrefix}_CONTENT_TYPE_UNSUPPORTED");
        if (value is null)
            throw Validation($"{codePrefix}_CONTENT_REQUIRED",
                "Exchange content is required.");
        if (Encoding.UTF8.GetByteCount(value) > MaximumContentBytes)
            throw Validation($"{codePrefix}_CONTENT_TOO_LARGE",
                $"Raw UTF-8 exchange content cannot exceed {MaximumContentBytes} bytes.");

        string normalized;
        if (normalizedType == "application/json")
        {
            try
            {
                using var document = JsonDocument.Parse(value);
                ValidateNoDuplicateJsonProperties(document.RootElement,
                    $"{codePrefix}_CONTENT_DUPLICATE_PROPERTY");
                using var stream = new MemoryStream();
                using (var writer = new Utf8JsonWriter(stream))
                    WriteCanonicalJson(writer, document.RootElement);
                normalized = Encoding.UTF8.GetString(stream.ToArray());
            }
            catch (JsonException)
            {
                throw Validation($"{codePrefix}_CONTENT_INVALID",
                    "The configured JSON representation requires valid JSON content.");
            }
        }
        else
        {
            normalized = value.Normalize(NormalizationForm.FormC)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            if (normalized.Any(character =>
                    character == '\0' ||
                    character < ' ' && character is not '\n' and not '\t'))
                throw Validation($"{codePrefix}_CONTENT_UNSAFE",
                    "Text exchange content contains NUL or unsupported control characters.");
            if (normalizedType is "application/xml" or "text/xml")
            {
                try
                {
                    var settings = new XmlReaderSettings
                    {
                        DtdProcessing = DtdProcessing.Prohibit,
                        XmlResolver = null,
                        MaxCharactersInDocument = MaximumContentBytes
                    };
                    using var textReader = new StringReader(normalized);
                    using var xmlReader = XmlReader.Create(textReader, settings);
                    _ = XDocument.Load(xmlReader, LoadOptions.PreserveWhitespace);
                }
                catch (XmlException)
                {
                    throw Validation($"{codePrefix}_CONTENT_INVALID",
                        "The configured XML representation requires well-formed XML content.");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(normalized))
            throw Validation($"{codePrefix}_CONTENT_REQUIRED",
                "Exchange content cannot be empty.");
        if (Encoding.UTF8.GetByteCount(normalized) > MaximumContentBytes)
            throw Validation($"{codePrefix}_CONTENT_TOO_LARGE",
                $"Canonical UTF-8 exchange content cannot exceed {MaximumContentBytes} bytes.");
        return normalized;
    }

    private static string NormalizeContentType(string? value, string code)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized is "application/json" or "text/csv" or "application/csv" or
            "application/xml" or "text/xml" or "text/plain")
            return normalized;
        throw Validation(code,
            "DEC-009 must configure one supported canonical content type: application/json, text/csv, application/csv, application/xml, text/xml, or text/plain.");
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                             .OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteCanonicalJson(writer, item);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static void ValidateNoDuplicateJsonProperties(
        JsonElement element,
        string code)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw Validation(code,
                        $"Duplicate JSON property '{property.Name}' is not allowed.");
                ValidateNoDuplicateJsonProperties(property.Value, code);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                ValidateNoDuplicateJsonProperties(item, code);
        }
    }

    private static void AssertOptionalHash(string? expected, string actual, string code)
    {
        if (string.IsNullOrWhiteSpace(expected)) return;
        if (!string.Equals(NormalizeHash(expected), actual, StringComparison.OrdinalIgnoreCase))
            throw Conflict(code, "The supplied checksum does not match the server-computed SHA-256 value.");
    }

    private static void AssertReplayFingerprint(string stored, string requested)
    {
        if (!string.Equals(stored, requested, StringComparison.OrdinalIgnoreCase))
            throw Conflict("GHANEPS_IDEMPOTENCY_CONFLICT",
                "The idempotency key is already bound to different immutable exchange inputs.");
    }

    private static string Fingerprint(object value) => Hash(Serialize(value));

    private static string CreateAutomaticAttemptIdempotency(
        string exchangeIdempotencyKey)
    {
        const string suffix = ":received";
        var normalized = exchangeIdempotencyKey.Trim();
        return normalized.Length + suffix.Length <= 100
            ? $"{normalized}{suffix}"
            : $"received:{Hash(normalized)}";
    }

    private static string CreateExchangeRequestFingerprint(
        ProcurementGhanepsPayloadRequest request,
        ProcurementGhanepsExchangeDirection direction,
        string? transportReference,
        string sourceReference,
        string mappingKey,
        string payloadContentType,
        string payloadChecksum,
        Guid profileId,
        int profileVersion,
        Guid decisionId,
        string mappingIntegrityHash) => Fingerprint(new
        {
            action = direction == ProcurementGhanepsExchangeDirection.Export
                ? "PrepareExport"
                : "RecordImport",
            request.SourceType,
            request.SourceId,
            request.EventFamily,
            mappingKey = mappingKey.Trim().ToUpperInvariant(),
            eventReference = sourceReference,
            direction,
            payloadContentType,
            payloadChecksum,
            fileName = TrimOrNull(request.FileName, 260),
            transportReference = TrimOrNull(transportReference, 200),
            evidenceReference = TrimOrNull(request.EvidenceReference, 500),
            profileId,
            profileVersion,
            decisionId,
            mappingIntegrityHash
        });

    private static string NormalizeHash(string? value)
    {
        var hash = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (!IsHash(hash))
            throw Validation("GHANEPS_CHECKSUM_INVALID",
                "A 64-character SHA-256 checksum is required.");
        return hash;
    }

    private static bool IsHash(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static string Serialize(object value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string NormalizeCorrelation(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? Guid.NewGuid().ToString("N")
            : value.Trim();
        RequireMaximum(normalized, 100, "GHANEPS_CORRELATION_ID_TOO_LONG");
        return normalized;
    }

    private static void RequireIdempotency(string? value)
    {
        Require(value, "GHANEPS_IDEMPOTENCY_KEY_REQUIRED", "IdempotencyKey is required.");
        RequireMaximum(value, 100, "GHANEPS_IDEMPOTENCY_KEY_TOO_LONG");
    }

    private static void Require(string? value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw Validation(code, message);
    }

    private static void RequireMaximum(string? value, int maximumLength, string code)
    {
        if (value is not null && value.Trim().Length > maximumLength)
            throw Validation(code, $"The value cannot exceed {maximumLength} characters.");
    }

    private static string? TrimOrNull(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength];
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static ProcurementGhanepsExchangeNotFoundException NotFound(
        string code,
        string message) => new(code, message);

    private static ProcurementGhanepsExchangeConflictException Conflict(
        string code,
        string message) => new(code, message);

    private static ProcurementGhanepsExchangeValidationException Validation(
        string code,
        string message) => new(code, message);

    private static ProcurementGhanepsExchangeAuthorizationException Authorization(
        string message) => new(message);

    private sealed record ResolvedProfile(
        ProcurementConfigurationProfile Profile,
        ProcurementConfigurationDecision Decision,
        ProcurementGhanepsDecisionValueDto Value,
        string ValueJson,
        string ValueHash,
        DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        IReadOnlyList<ProcurementGhanepsConfiguredMappingDto> Mappings);

    private sealed record ResolvedSource(
        ProcurementGhanepsSourceType Type,
        Guid Id,
        string Reference,
        string Variant,
        DateTime OccurredAtUtc,
        string SnapshotJson,
        string IntegrityHash);

    private sealed record AwardIdentity(
        ProcurementBidderCommunicationAwardFamily Family,
        Guid Id,
        string Reference,
        DateTime AwardedAtUtc);

    private sealed record Capabilities(bool Manage, bool Acknowledge, bool Reconcile);

    private sealed record DenialContext(
        string Action,
        Guid? ExchangeEventId,
        string SourceType,
        Guid? SourceId,
        string? SourceReference,
        ProcurementGhanepsEventFamily? EventFamily,
        string? MappingKey,
        string? IdempotencyKey,
        string? EvidenceReference)
    {
        public static DenialContext ForCreate(
            string action,
            ProcurementGhanepsPayloadRequest request) => new(
            action,
            null,
            (request.RouteSourceType ?? request.SourceType)?.ToString() ??
            EventType,
            request.RouteSourceId ?? request.SourceId,
            request.EventReference,
            request.EventFamily,
            request.MappingKey,
            request.IdempotencyKey,
            request.EvidenceReference);

        public static DenialContext ForChild(
            string action,
            Guid exchangeEventId,
            string? idempotencyKey,
            string? evidenceReference,
            ProcurementGhanepsSourceType? routeSourceType,
            Guid? routeSourceId) => new(
            action,
            exchangeEventId,
            routeSourceType?.ToString() ?? EventType,
            routeSourceId,
            exchangeEventId.ToString(),
            null,
            null,
            idempotencyKey,
            evidenceReference);
    }
}
