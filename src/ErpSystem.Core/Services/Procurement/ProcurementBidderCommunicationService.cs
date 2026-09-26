using ErpSystem.Shared;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementBidderCommunicationService : IProcurementBidderCommunicationService
{
    private const string EventType = "ProcurementBidderCommunication";
    private const string ManagePermission = "procurement.tender.administer";
    private const string ApprovePermission = "procurement.tender.approve";
    private const string InitiatorApproverSod = "SOD-INITIATOR-APPROVER";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyList<string> DecisionKeys =
        Enumerable.Range(1, 14).Select(item => $"DEC-{item:000}").ToArray();

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly IProcurementSodGuardService _sodGuard;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly IProcurementAwardReadinessService _awardReadiness;
    private readonly INotificationTopicPublisher _notifications;

    public ProcurementBidderCommunicationService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService accessControl,
        IProcurementSodGuardService sodGuard,
        IProcurementControlEventService controlEvents,
        IProcurementAwardReadinessService awardReadiness,
        INotificationTopicPublisher notifications)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _accessControl = accessControl;
        _sodGuard = sodGuard;
        _controlEvents = controlEvents;
        _awardReadiness = awardReadiness;
        _notifications = notifications;
    }

    private IGenericRepository<ProcurementBidderCommunicationRegister> Registers =>
        _unitOfWork.Repository<ProcurementBidderCommunicationRegister>();
    private IGenericRepository<ProcurementBidderCommunicationRecipient> Recipients =>
        _unitOfWork.Repository<ProcurementBidderCommunicationRecipient>();
    private IGenericRepository<ProcurementBidderCommunicationLetterVersion> Letters =>
        _unitOfWork.Repository<ProcurementBidderCommunicationLetterVersion>();
    private IGenericRepository<ProcurementBidderCommunicationDispatch> Dispatches =>
        _unitOfWork.Repository<ProcurementBidderCommunicationDispatch>();
    private IGenericRepository<ProcurementBidderCommunicationDelivery> Deliveries =>
        _unitOfWork.Repository<ProcurementBidderCommunicationDelivery>();
    private IGenericRepository<ProcurementBidderCommunicationAcknowledgement> Acknowledgements =>
        _unitOfWork.Repository<ProcurementBidderCommunicationAcknowledgement>();
    private IGenericRepository<ProcurementBidderAppeal> Appeals =>
        _unitOfWork.Repository<ProcurementBidderAppeal>();
    private IGenericRepository<ProcurementBidderAppealDecision> AppealDecisions =>
        _unitOfWork.Repository<ProcurementBidderAppealDecision>();
    private IGenericRepository<ProcurementTenderSecurityInstrument> Securities =>
        _unitOfWork.Repository<ProcurementTenderSecurityInstrument>();
    private IGenericRepository<ProcurementTenderSecurityAction> SecurityActions =>
        _unitOfWork.Repository<ProcurementTenderSecurityAction>();

    public async Task<ProcurementBidderCommunicationOverviewDto> GetOverviewAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        EnsureInternalReader();
        var register = await LoadRegisterBySourceAsync(sourceType, sourceId, cancellationToken);
        return await MapOverviewAsync(register, externalPartnerIds: null, cancellationToken);
    }

    public async Task<ProcurementBidderCommunicationOverviewDto> GetExternalOverviewAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        EnsureExternalReader();
        var partnerIds = await GetExternalPartnerIdsAsync(cancellationToken);
        if (partnerIds.Count == 0)
            throw Authorization("The current user is not linked to an active approved supplier account.");
        var register = await LoadRegisterBySourceAsync(sourceType, sourceId, cancellationToken);
        if (!register.Recipients.Any(item => partnerIds.Contains(item.BusinessPartnerId)))
            throw NotFound("BIDDER_COMMUNICATION_NOT_FOUND",
                "No bidder communication is available to the current supplier account.");
        return await MapOverviewAsync(register, partnerIds, cancellationToken);
    }

    public async Task<ProcurementBidderCommunicationOverviewDto> InitializeAsync(
        InitializeProcurementBidderCommunicationRegisterRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        if (request.SourceId == Guid.Empty)
            throw Validation("BIDDER_COMMUNICATION_SOURCE_REQUIRED", "SourceId is required.");
        Require(request.StandstillAuthorityReference, "BIDDER_COMMUNICATION_STANDSTILL_AUTHORITY_REQUIRED",
            "The authority for the configured standstill and appeal dates is required.");
        RequireIdempotency(request.IdempotencyKey);
        var correlation = NormalizeCorrelation(correlationId);
        var source = await ResolveSourceAsync(request.SourceType, request.SourceId, cancellationToken);
        await EnsureCapabilityAsync(ManagePermission, source.SourceReference, correlation, cancellationToken);

        var existing = await RegisterQuery().AsNoTracking().SingleOrDefaultAsync(
            item => item.SourceType == request.SourceType && item.SourceId == request.SourceId,
            cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.IdempotencyKey, request.IdempotencyKey.Trim(), StringComparison.Ordinal) ||
                existing.StandstillEndsAtUtc != EnsureUtc(request.StandstillEndsAtUtc) ||
                existing.AppealWindowEndsAtUtc != EnsureUtc(request.AppealWindowEndsAtUtc) ||
                existing.AwardReadinessDecisionId != request.ExpectedAwardReadinessDecisionId ||
                !string.Equals(existing.AwardReadinessIntegrityHash,
                    NormalizeHash(request.ExpectedAwardReadinessIntegrityHash),
                    StringComparison.OrdinalIgnoreCase))
                throw Conflict("BIDDER_COMMUNICATION_REGISTER_EXISTS",
                    "A register already exists for this source with different immutable inputs.");
            return await MapOverviewAsync(existing, null, cancellationToken);
        }

        var readiness = await _awardReadiness.GetLatestAsync(
            request.SourceType, request.SourceId, cancellationToken)
            ?? throw Conflict("BIDDER_COMMUNICATION_READINESS_MISSING",
                "A retained Ready award-readiness decision is required.");
        ValidateReadiness(readiness, source, request);
        var standstillEnd = EnsureUtc(request.StandstillEndsAtUtc);
        var appealEnd = EnsureUtc(request.AppealWindowEndsAtUtc);
        if (standstillEnd <= source.AwardedAtUtc)
            throw Validation("BIDDER_COMMUNICATION_STANDSTILL_INVALID",
                "StandstillEndsAtUtc must be after the server-derived award time.");
        if (appealEnd < standstillEnd)
            throw Validation("BIDDER_COMMUNICATION_APPEAL_WINDOW_INVALID",
                "AppealWindowEndsAtUtc cannot be before StandstillEndsAtUtc.");

        var now = DateTime.UtcNow;
        var recipientSnapshot = source.Recipients
            .OrderBy(item => item.BusinessPartnerId)
            .Select(item => new
            {
                item.BusinessPartnerId,
                item.Outcome,
                subjectIds = item.SubjectIds.OrderBy(id => id),
                item.PartnerCode,
                item.PartnerName,
                item.Email,
                item.Phone
            }).ToList();
        var register = new ProcurementBidderCommunicationRegister
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            SourceType = request.SourceType,
            SourceId = request.SourceId,
            SourceReference = source.SourceReference,
            AwardFamily = source.AwardFamily,
            AwardId = source.AwardId,
            AwardReference = source.AwardReference,
            AwardedAtUtc = source.AwardedAtUtc,
            AwardReadinessDecisionId = readiness.Id,
            AwardReadinessDecisionSequence = readiness.DecisionSequence,
            AwardReadinessIntegrityHash = readiness.IntegrityHash,
            AwardReadinessSourceIntegrityHash = readiness.SourceIntegrityHash,
            StandstillStartsAtUtc = source.AwardedAtUtc,
            StandstillEndsAtUtc = standstillEnd,
            AppealWindowEndsAtUtc = appealEnd,
            StandstillAuthorityReference = request.StandstillAuthorityReference.Trim(),
            AwardSnapshotJson = Serialize(source.AwardSnapshot),
            RecipientSnapshotJson = Serialize(recipientSnapshot),
            RecipientSnapshotHash = Hash(Serialize(recipientSnapshot)),
            IdempotencyKey = request.IdempotencyKey.Trim(),
            CorrelationId = correlation,
            InitializedAtUtc = now,
            InitializedByUserId = _currentUser.UserId,
            InitializedByName = ActorName(),
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        register.IntegrityHash = Hash(Serialize(new
        {
            schemaVersion = "tdc.bidder-communication-register.v1",
            register.Id,
            register.TenantId,
            register.SourceType,
            register.SourceId,
            register.SourceReference,
            register.AwardFamily,
            register.AwardId,
            register.AwardReference,
            register.AwardedAtUtc,
            register.AwardReadinessDecisionId,
            register.AwardReadinessDecisionSequence,
            register.AwardReadinessIntegrityHash,
            register.AwardReadinessSourceIntegrityHash,
            register.StandstillStartsAtUtc,
            register.StandstillEndsAtUtc,
            register.AppealWindowEndsAtUtc,
            register.StandstillAuthorityReference,
            register.RecipientSnapshotHash,
            register.IdempotencyKey,
            register.InitializedAtUtc,
            register.InitializedByUserId
        }));

        foreach (var item in source.Recipients)
        {
            var recipient = new ProcurementBidderCommunicationRecipient
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUser.TenantId,
                RegisterId = register.Id,
                BusinessPartnerId = item.BusinessPartnerId,
                Outcome = item.Outcome,
                BidOrQuoteIdsJson = Serialize(item.SubjectIds.OrderBy(id => id)),
                PartnerCode = item.PartnerCode,
                PartnerName = item.PartnerName,
                RecipientEmail = item.Email,
                RecipientPhone = item.Phone,
                LineageHash = item.LineageHash,
                CreatedAt = now,
                CreatedBy = _currentUser.Username,
                CreatedById = _currentUser.UserId
            };
            recipient.IntegrityHash = Hash(Serialize(new
            {
                schemaVersion = "tdc.bidder-communication-recipient.v1",
                recipient.Id,
                recipient.TenantId,
                recipient.RegisterId,
                recipient.BusinessPartnerId,
                recipient.Outcome,
                recipient.BidOrQuoteIdsJson,
                recipient.PartnerCode,
                recipient.PartnerName,
                recipient.RecipientEmail,
                recipient.RecipientPhone,
                recipient.LineageHash
            }));
            register.Recipients.Add(recipient);
        }

        await Registers.AddAsync(register);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(register, "RegisterInitialized", ProcurementControlEventResult.Allowed,
            request.IdempotencyKey, new
            {
                register.AwardFamily,
                register.AwardId,
                register.AwardReadinessDecisionId,
                successful = register.Recipients.Count(item =>
                    item.Outcome == ProcurementBidderCommunicationRecipientOutcome.Successful),
                unsuccessful = register.Recipients.Count(item =>
                    item.Outcome == ProcurementBidderCommunicationRecipientOutcome.Unsuccessful),
                register.StandstillEndsAtUtc,
                register.AppealWindowEndsAtUtc
            }, register.StandstillAuthorityReference, correlation, now, [], cancellationToken);
        return await MapOverviewAsync(
            await LoadRegisterByIdAsync(register.Id, cancellationToken), null, cancellationToken);
    }

    public async Task<ProcurementBidderCommunicationLetterVersionDto> ApproveLetterAsync(
        Guid recipientId,
        ApproveProcurementBidderCommunicationLetterRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        RequireIdempotency(request.IdempotencyKey);
        Require(request.ContentReference, "BIDDER_LETTER_CONTENT_REQUIRED", "ContentReference is required.");
        Require(request.ApprovalReference, "BIDDER_LETTER_APPROVAL_REQUIRED", "ApprovalReference is required.");
        Require(request.ApprovalEvidenceReference, "BIDDER_LETTER_EVIDENCE_REQUIRED",
            "Approval evidence is required.");
        var correlation = NormalizeCorrelation(correlationId);
        var recipient = await LoadRecipientAsync(recipientId, cancellationToken);
        var register = recipient.Register;
        await EnsureCapabilityAsync(ApprovePermission, register.SourceReference, correlation, cancellationToken);
        AssertHash(request.ExpectedRegisterIntegrityHash, register.IntegrityHash,
            "BIDDER_COMMUNICATION_REGISTER_STALE");

        var existing = await LetterQuery().AsNoTracking()
            .SingleOrDefaultAsync(item => item.RecipientId == recipientId &&
                item.IdempotencyKey == request.IdempotencyKey.Trim(), cancellationToken);
        if (existing is not null)
        {
            if (existing.TemplateVersionId != request.TemplateVersionId ||
                !string.Equals(existing.ContentChecksumSha256,
                    NormalizeHash(request.ContentChecksumSha256), StringComparison.OrdinalIgnoreCase))
                throw Conflict("BIDDER_LETTER_IDEMPOTENCY_CONFLICT",
                    "The idempotency key was already used for a different letter.");
            return MapLetter(existing);
        }

        var template = await LoadApprovedLetterTemplateAsync(
            request.TemplateVersionId, recipient.Outcome, cancellationToken);
        var workflow = await ValidateCompletedWorkflowAsync(
            request.WorkflowInstanceId, recipient.Id, cancellationToken);
        await ValidateEvidenceAsync(request.ApprovalWorkflowEvidenceDocumentId,
            request.ApprovalFileUploadRecordId, cancellationToken);
        await EnforceSodAsync(register.SourceReference, register.InitializedByUserId,
            correlation, cancellationToken);
        var nextVersion = (await LetterQuery().Where(item => item.RecipientId == recipientId)
            .Select(item => (int?)item.Version).MaxAsync(cancellationToken) ?? 0) + 1;
        var now = DateTime.UtcNow;
        var letter = new ProcurementBidderCommunicationLetterVersion
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            RecipientId = recipient.Id,
            Version = nextVersion,
            TemplateVersionId = template.Id,
            TemplateReference = template.ContentReference,
            TemplateChecksumSha256 = NormalizeHash(template.ContentChecksumSha256),
            ContentReference = request.ContentReference.Trim(),
            ContentChecksumSha256 = NormalizeHash(request.ContentChecksumSha256),
            WorkflowDefinitionId = workflow.WorkflowDefinitionId,
            WorkflowInstanceId = workflow.Id,
            ApprovalReference = request.ApprovalReference.Trim(),
            ApprovalEvidenceReference = request.ApprovalEvidenceReference.Trim(),
            ApprovalWorkflowEvidenceDocumentId = request.ApprovalWorkflowEvidenceDocumentId,
            ApprovalFileUploadRecordId = request.ApprovalFileUploadRecordId,
            ApprovedAtUtc = workflow.CompletedDate!.Value,
            ApprovedByUserId = _currentUser.UserId,
            ApprovedByName = ActorName(),
            IdempotencyKey = request.IdempotencyKey.Trim(),
            CorrelationId = correlation,
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId
        };
        letter.IntegrityHash = Hash(Serialize(new
        {
            schemaVersion = "tdc.bidder-letter.v1",
            letter.Id,
            letter.TenantId,
            letter.RecipientId,
            letter.Version,
            recipient.Outcome,
            letter.TemplateVersionId,
            letter.TemplateReference,
            letter.TemplateChecksumSha256,
            letter.ContentReference,
            letter.ContentChecksumSha256,
            letter.WorkflowDefinitionId,
            letter.WorkflowInstanceId,
            letter.ApprovalReference,
            letter.ApprovalEvidenceReference,
            letter.ApprovedAtUtc,
            letter.ApprovedByUserId,
            letter.IdempotencyKey
        }));
        await Letters.AddAsync(letter);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(register, "LetterApproved", ProcurementControlEventResult.Allowed,
            request.IdempotencyKey, new
            {
                recipientId = recipient.Id,
                recipient.BusinessPartnerId,
                recipient.Outcome,
                letterId = letter.Id,
                letter.Version,
                letter.TemplateVersionId,
                letter.WorkflowInstanceId,
                letter.IntegrityHash
            }, request.ApprovalReference, correlation, now,
            Evidence(request.ApprovalEvidenceReference,
                request.ApprovalWorkflowEvidenceDocumentId,
                request.ApprovalFileUploadRecordId, "Approved bidder letter", "SRC-011"),
            cancellationToken);
        return MapLetter(letter);
    }

    public async Task<ProcurementBidderCommunicationDispatchDto> DispatchLetterAsync(
        Guid letterVersionId,
        DispatchProcurementBidderCommunicationLetterRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        RequireIdempotency(request.IdempotencyKey);
        Require(request.Destination, "BIDDER_LETTER_DESTINATION_REQUIRED", "Destination is required.");
        Require(request.DispatchReference, "BIDDER_LETTER_DISPATCH_REFERENCE_REQUIRED",
            "DispatchReference is required.");
        Require(request.DispatchEvidenceReference, "BIDDER_LETTER_DISPATCH_EVIDENCE_REQUIRED",
            "Dispatch evidence is required.");
        var correlation = NormalizeCorrelation(correlationId);
        var letter = await LoadLetterAsync(letterVersionId, cancellationToken);
        var recipient = letter.Recipient;
        var register = recipient.Register;
        await EnsureCapabilityAsync(ManagePermission, register.SourceReference, correlation, cancellationToken);
        var latestVersion = await LetterQuery().Where(item => item.RecipientId == recipient.Id)
            .MaxAsync(item => item.Version, cancellationToken);
        if (letter.Version != latestVersion)
            throw Conflict("BIDDER_LETTER_VERSION_STALE",
                "Only the latest approved immutable letter version can be dispatched.");
        ValidateDestination(recipient, request.Channel, request.Destination);
        await ValidateEvidenceAsync(request.DispatchWorkflowEvidenceDocumentId,
            request.DispatchFileUploadRecordId, cancellationToken);
        var existing = await DispatchQuery().AsNoTracking()
            .SingleOrDefaultAsync(item => item.LetterVersionId == letter.Id &&
                item.IdempotencyKey == request.IdempotencyKey.Trim(), cancellationToken);
        if (existing is not null) return MapDispatch(existing);
        var sequence = (await DispatchQuery().Where(item => item.LetterVersionId == letter.Id)
            .Select(item => (int?)item.Sequence).MaxAsync(cancellationToken) ?? 0) + 1;
        var now = DateTime.UtcNow;
        var dispatch = new ProcurementBidderCommunicationDispatch
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            LetterVersionId = letter.Id,
            Sequence = sequence,
            Channel = request.Channel,
            Destination = request.Destination.Trim(),
            DispatchReference = request.DispatchReference.Trim(),
            DispatchEvidenceReference = request.DispatchEvidenceReference.Trim(),
            DispatchWorkflowEvidenceDocumentId = request.DispatchWorkflowEvidenceDocumentId,
            DispatchFileUploadRecordId = request.DispatchFileUploadRecordId,
            DispatchedAtUtc = now,
            DispatchedByUserId = _currentUser.UserId,
            DispatchedByName = ActorName(),
            IdempotencyKey = request.IdempotencyKey.Trim(),
            CorrelationId = correlation,
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId
        };
        dispatch.IntegrityHash = Hash(Serialize(new
        {
            schemaVersion = "tdc.bidder-letter-dispatch.v1",
            dispatch.Id,
            dispatch.TenantId,
            dispatch.LetterVersionId,
            dispatch.Sequence,
            dispatch.Channel,
            dispatch.Destination,
            dispatch.DispatchReference,
            dispatch.DispatchEvidenceReference,
            dispatch.DispatchedAtUtc,
            dispatch.DispatchedByUserId,
            dispatch.IdempotencyKey,
            letter.IntegrityHash
        }));
        await Dispatches.AddAsync(dispatch);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(register, "LetterDispatched", ProcurementControlEventResult.Allowed,
            request.IdempotencyKey, new
            {
                recipientId = recipient.Id,
                recipient.BusinessPartnerId,
                recipient.Outcome,
                letterId = letter.Id,
                letter.Version,
                dispatchId = dispatch.Id,
                dispatch.Sequence,
                dispatch.Channel,
                dispatch.DispatchReference,
                dispatch.IntegrityHash
            }, request.DispatchReference, correlation, now,
            Evidence(request.DispatchEvidenceReference,
                request.DispatchWorkflowEvidenceDocumentId,
                request.DispatchFileUploadRecordId, "Bidder letter dispatch", "SRC-011"),
            cancellationToken);
        await PublishAsync("procurement.bidder-communication.dispatched", register, recipient,
            new
            {
                letterId = letter.Id,
                letter.Version,
                dispatchId = dispatch.Id,
                dispatch.Channel,
                dispatch.DispatchReference
            },
            cancellationToken);
        return MapDispatch(dispatch);
    }

    public async Task<ProcurementBidderCommunicationDeliveryDto> RecordDeliveryAsync(
        Guid dispatchId,
        RecordProcurementBidderCommunicationDeliveryRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        RequireIdempotency(request.IdempotencyKey);
        Require(request.ProviderReference, "BIDDER_DELIVERY_REFERENCE_REQUIRED",
            "ProviderReference is required.");
        Require(request.EvidenceReference, "BIDDER_DELIVERY_EVIDENCE_REQUIRED",
            "Delivery evidence is required.");
        var correlation = NormalizeCorrelation(correlationId);
        var dispatch = await LoadDispatchAsync(dispatchId, cancellationToken);
        var register = dispatch.LetterVersion.Recipient.Register;
        await EnsureCapabilityAsync(ManagePermission, register.SourceReference, correlation, cancellationToken);
        var occurred = EnsureUtc(request.OccurredAtUtc);
        if (occurred < dispatch.DispatchedAtUtc || occurred > DateTime.UtcNow.AddMinutes(5))
            throw Validation("BIDDER_DELIVERY_TIME_INVALID",
                "Delivery time must follow dispatch and cannot be in the future.");
        var existing = await DeliveryQuery().AsNoTracking()
            .SingleOrDefaultAsync(item => item.DispatchId == dispatchId &&
                item.IdempotencyKey == request.IdempotencyKey.Trim(), cancellationToken);
        if (existing is not null) return MapDelivery(existing);
        var previous = await DeliveryQuery().Where(item => item.DispatchId == dispatchId)
            .OrderByDescending(item => item.Sequence).AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (previous is not null && previous.Outcome != ProcurementBidderCommunicationDeliveryOutcome.Sent)
            throw Conflict("BIDDER_DELIVERY_TERMINAL",
                "A terminal delivery outcome is already recorded; use a new dispatch for retry.");
        if (previous is not null && occurred < previous.OccurredAtUtc)
            throw Conflict("BIDDER_DELIVERY_SEQUENCE_STALE",
                "Delivery history must be recorded in chronological order.");
        var now = DateTime.UtcNow;
        var delivery = new ProcurementBidderCommunicationDelivery
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            DispatchId = dispatch.Id,
            Sequence = (previous?.Sequence ?? 0) + 1,
            Outcome = request.Outcome,
            OccurredAtUtc = occurred,
            ProviderReference = request.ProviderReference.Trim(),
            Detail = TrimOrNull(request.Detail, 1000),
            EvidenceReference = request.EvidenceReference.Trim(),
            RecordedByUserId = _currentUser.UserId,
            IdempotencyKey = request.IdempotencyKey.Trim(),
            CorrelationId = correlation,
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId
        };
        delivery.IntegrityHash = Hash(Serialize(new
        {
            schemaVersion = "tdc.bidder-letter-delivery.v1",
            delivery.Id,
            delivery.TenantId,
            delivery.DispatchId,
            delivery.Sequence,
            delivery.Outcome,
            delivery.OccurredAtUtc,
            delivery.ProviderReference,
            delivery.Detail,
            delivery.EvidenceReference,
            delivery.RecordedByUserId,
            delivery.IdempotencyKey,
            previousHash = previous?.IntegrityHash,
            dispatch.IntegrityHash
        }));
        await Deliveries.AddAsync(delivery);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(register, "DeliveryRecorded", ProcurementControlEventResult.Allowed,
            request.IdempotencyKey, new
            {
                dispatchId = dispatch.Id,
                deliveryId = delivery.Id,
                delivery.Sequence,
                delivery.Outcome,
                delivery.ProviderReference,
                delivery.IntegrityHash
            }, request.Detail, correlation, now,
            Evidence(request.EvidenceReference, null, null, "Bidder letter delivery", "SRC-011"),
            cancellationToken);
        await PublishAsync("procurement.bidder-communication.delivery", register,
            dispatch.LetterVersion.Recipient,
            new
            {
                dispatchId = dispatch.Id,
                deliveryId = delivery.Id,
                delivery.Outcome,
                delivery.ProviderReference
            },
            cancellationToken);
        return MapDelivery(delivery);
    }

    public Task<ProcurementBidderCommunicationAcknowledgementDto> RecordAcknowledgementAsync(
        Guid dispatchId,
        RecordProcurementBidderCommunicationAcknowledgementRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        RecordAcknowledgementCoreAsync(dispatchId, request, correlationId, external: false,
            cancellationToken);

    public Task<ProcurementBidderCommunicationAcknowledgementDto> RecordExternalAcknowledgementAsync(
        Guid dispatchId,
        RecordProcurementBidderCommunicationAcknowledgementRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        RecordAcknowledgementCoreAsync(dispatchId, request, correlationId, external: true,
            cancellationToken);

    public Task<ProcurementBidderAppealDto> FileAppealAsync(
        Guid recipientId,
        FileProcurementBidderAppealRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        FileAppealCoreAsync(recipientId, request, correlationId, external: false, cancellationToken);

    public Task<ProcurementBidderAppealDto> FileExternalAppealAsync(
        Guid recipientId,
        FileProcurementBidderAppealRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        FileAppealCoreAsync(recipientId, request, correlationId, external: true, cancellationToken);

    public async Task<ProcurementBidderAppealDecisionDto> ResolveAppealAsync(
        Guid appealId,
        ResolveProcurementBidderAppealRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        RequireIdempotency(request.IdempotencyKey);
        Require(request.Reason, "BIDDER_APPEAL_DECISION_REASON_REQUIRED", "Decision reason is required.");
        Require(request.DecisionReference, "BIDDER_APPEAL_DECISION_REFERENCE_REQUIRED",
            "DecisionReference is required.");
        Require(request.EvidenceReference, "BIDDER_APPEAL_DECISION_EVIDENCE_REQUIRED",
            "Decision evidence is required.");
        var correlation = NormalizeCorrelation(correlationId);
        var appeal = await LoadAppealAsync(appealId, cancellationToken);
        var register = appeal.Recipient.Register;
        await EnsureCapabilityAsync(ApprovePermission, register.SourceReference, correlation, cancellationToken);
        var existing = await AppealDecisionQuery().AsNoTracking()
            .SingleOrDefaultAsync(item => item.AppealId == appealId, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.IdempotencyKey, request.IdempotencyKey.Trim(),
                    StringComparison.Ordinal))
                throw Conflict("BIDDER_APPEAL_ALREADY_DECIDED",
                    "This appeal already has an immutable outcome.");
            return MapAppealDecision(existing);
        }
        var workflow = await ValidateCompletedWorkflowAsync(
            request.WorkflowInstanceId, appeal.Id, cancellationToken);
        await ValidateEvidenceAsync(request.EvidenceWorkflowDocumentId,
            request.EvidenceFileUploadRecordId, cancellationToken);
        await EnforceSodAsync(register.SourceReference, appeal.FiledByUserId,
            correlation, cancellationToken);
        var now = DateTime.UtcNow;
        var decision = new ProcurementBidderAppealDecision
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            AppealId = appeal.Id,
            Outcome = request.Outcome,
            Reason = request.Reason.Trim(),
            WorkflowDefinitionId = workflow.WorkflowDefinitionId,
            WorkflowInstanceId = workflow.Id,
            DecisionReference = request.DecisionReference.Trim(),
            EvidenceReference = request.EvidenceReference.Trim(),
            EvidenceWorkflowDocumentId = request.EvidenceWorkflowDocumentId,
            EvidenceFileUploadRecordId = request.EvidenceFileUploadRecordId,
            DecidedAtUtc = workflow.CompletedDate!.Value,
            DecidedByUserId = _currentUser.UserId,
            DecidedByName = ActorName(),
            IdempotencyKey = request.IdempotencyKey.Trim(),
            CorrelationId = correlation,
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId
        };
        decision.IntegrityHash = Hash(Serialize(new
        {
            schemaVersion = "tdc.bidder-appeal-decision.v1",
            decision.Id,
            decision.TenantId,
            decision.AppealId,
            decision.Outcome,
            decision.Reason,
            decision.WorkflowDefinitionId,
            decision.WorkflowInstanceId,
            decision.DecisionReference,
            decision.EvidenceReference,
            decision.DecidedAtUtc,
            decision.DecidedByUserId,
            decision.IdempotencyKey,
            appeal.IntegrityHash
        }));
        await AppealDecisions.AddAsync(decision);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(register, "AppealResolved", ProcurementControlEventResult.Allowed,
            request.IdempotencyKey, new
            {
                appealId = appeal.Id,
                decisionId = decision.Id,
                decision.Outcome,
                decision.WorkflowInstanceId,
                decision.DecisionReference,
                decision.IntegrityHash
            }, request.Reason, correlation, now,
            Evidence(request.EvidenceReference, request.EvidenceWorkflowDocumentId,
                request.EvidenceFileUploadRecordId, "Bidder appeal decision", "SRC-011"),
            cancellationToken);
        await PublishAsync("procurement.bidder-communication.appeal-resolved",
            register, appeal.Recipient,
            new
            {
                appealId = appeal.Id,
                decisionId = decision.Id,
                decision.Outcome,
                decision.DecisionReference
            },
            cancellationToken);
        return MapAppealDecision(decision);
    }

    public async Task<ProcurementTenderSecurityInstrumentDto> RegisterSecurityAsync(
        Guid recipientId,
        RegisterProcurementTenderSecurityRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        RequireIdempotency(request.IdempotencyKey);
        Require(request.InstrumentReference, "TENDER_SECURITY_REFERENCE_REQUIRED",
            "InstrumentReference is required.");
        Require(request.IssuerName, "TENDER_SECURITY_ISSUER_REQUIRED", "IssuerName is required.");
        Require(request.EvidenceReference, "TENDER_SECURITY_EVIDENCE_REQUIRED",
            "Security evidence is required.");
        if ((request.TenderBidId.HasValue ? 1 : 0) +
            (request.RequestForQuotationQuoteId.HasValue ? 1 : 0) != 1)
            throw Validation("TENDER_SECURITY_SUBJECT_REQUIRED",
                "Exactly one TenderBidId or RequestForQuotationQuoteId is required.");
        if (request.Amount <= 0)
            throw Validation("TENDER_SECURITY_AMOUNT_INVALID", "Security amount must be positive.");
        var issuedAt = EnsureUtc(request.IssuedAtUtc);
        var expiresAt = EnsureUtc(request.ExpiresAtUtc);
        if (expiresAt <= issuedAt)
            throw Validation("TENDER_SECURITY_EXPIRY_INVALID", "Security expiry must follow issue time.");
        var correlation = NormalizeCorrelation(correlationId);
        var recipient = await LoadRecipientAsync(recipientId, cancellationToken);
        var register = recipient.Register;
        await EnsureCapabilityAsync(ManagePermission, register.SourceReference, correlation, cancellationToken);
        var subjects = DeserializeIds(recipient.BidOrQuoteIdsJson);
        var subjectId = request.TenderBidId ?? request.RequestForQuotationQuoteId!.Value;
        if (!subjects.Contains(subjectId))
            throw Validation("TENDER_SECURITY_SUBJECT_MISMATCH",
                "The security subject is not part of the server-derived recipient lineage.");
        if (request.TenderBidId.HasValue &&
            register.SourceType == ProcurementAwardReadinessSourceType.RequestForQuotation ||
            request.RequestForQuotationQuoteId.HasValue &&
            register.SourceType != ProcurementAwardReadinessSourceType.RequestForQuotation)
            throw Validation("TENDER_SECURITY_SUBJECT_TYPE_MISMATCH",
                "The security subject type does not match the procurement source.");
        await ValidateEvidenceAsync(request.EvidenceWorkflowDocumentId,
            request.EvidenceFileUploadRecordId, cancellationToken);
        var existing = await SecurityQuery().AsNoTracking()
            .SingleOrDefaultAsync(item => item.RecipientId == recipientId &&
                item.IdempotencyKey == request.IdempotencyKey.Trim(), cancellationToken);
        if (existing is not null) return MapSecurity(existing, recipient, register);
        if (await SecurityQuery().AnyAsync(item => item.RecipientId == recipientId &&
            item.InstrumentReference == request.InstrumentReference.Trim(), cancellationToken))
            throw Conflict("TENDER_SECURITY_REFERENCE_EXISTS",
                "This security reference is already registered for the recipient.");
        var now = DateTime.UtcNow;
        var security = new ProcurementTenderSecurityInstrument
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            RecipientId = recipient.Id,
            TenderBidId = request.TenderBidId,
            RequestForQuotationQuoteId = request.RequestForQuotationQuoteId,
            InstrumentType = request.InstrumentType,
            InstrumentReference = request.InstrumentReference.Trim(),
            IssuerName = request.IssuerName.Trim(),
            Amount = request.Amount,
            CurrencyCode = NormalizeCurrency(request.CurrencyCode),
            IssuedAtUtc = issuedAt,
            ExpiresAtUtc = expiresAt,
            EvidenceReference = request.EvidenceReference.Trim(),
            EvidenceWorkflowDocumentId = request.EvidenceWorkflowDocumentId,
            EvidenceFileUploadRecordId = request.EvidenceFileUploadRecordId,
            RegisteredAtUtc = now,
            RegisteredByUserId = _currentUser.UserId,
            IdempotencyKey = request.IdempotencyKey.Trim(),
            CorrelationId = correlation,
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId
        };
        security.IntegrityHash = Hash(Serialize(new
        {
            schemaVersion = "tdc.tender-security.v1",
            security.Id,
            security.TenantId,
            security.RecipientId,
            security.TenderBidId,
            security.RequestForQuotationQuoteId,
            security.InstrumentType,
            security.InstrumentReference,
            security.IssuerName,
            security.Amount,
            security.CurrencyCode,
            security.IssuedAtUtc,
            security.ExpiresAtUtc,
            security.EvidenceReference,
            security.RegisteredAtUtc,
            security.RegisteredByUserId,
            security.IdempotencyKey,
            recipient.LineageHash
        }));
        await Securities.AddAsync(security);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(register, "SecurityRegistered", ProcurementControlEventResult.Allowed,
            request.IdempotencyKey, new
            {
                recipientId = recipient.Id,
                recipient.BusinessPartnerId,
                recipient.Outcome,
                securityId = security.Id,
                security.TenderBidId,
                security.RequestForQuotationQuoteId,
                security.InstrumentType,
                security.InstrumentReference,
                security.Amount,
                security.CurrencyCode,
                security.IntegrityHash
            }, request.InstrumentReference, correlation, now,
            Evidence(request.EvidenceReference, request.EvidenceWorkflowDocumentId,
                request.EvidenceFileUploadRecordId, "Tender security instrument", "SRC-012"),
            cancellationToken);
        return MapSecurity(security, recipient, register);
    }

    public async Task<ProcurementTenderSecurityActionDto> RecordSecurityActionAsync(
        Guid securityId,
        RecordProcurementTenderSecurityActionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticatedTenant();
        RequireIdempotency(request.IdempotencyKey);
        Require(request.ActionReference, "TENDER_SECURITY_ACTION_REFERENCE_REQUIRED",
            "ActionReference is required.");
        Require(request.Reason, "TENDER_SECURITY_ACTION_REASON_REQUIRED", "Action reason is required.");
        Require(request.EvidenceReference, "TENDER_SECURITY_ACTION_EVIDENCE_REQUIRED",
            "Action evidence is required.");
        var correlation = NormalizeCorrelation(correlationId);
        var security = await LoadSecurityAsync(securityId, cancellationToken);
        var recipient = security.Recipient;
        var register = recipient.Register;
        await EnsureCapabilityAsync(ApprovePermission, register.SourceReference, correlation, cancellationToken);
        var existing = await SecurityActionQuery().AsNoTracking()
            .SingleOrDefaultAsync(item => item.SecurityInstrumentId == securityId &&
                item.IdempotencyKey == request.IdempotencyKey.Trim(), cancellationToken);
        if (existing is not null) return MapSecurityAction(existing);
        var latest = await SecurityActionQuery()
            .Where(item => item.SecurityInstrumentId == securityId)
            .OrderByDescending(item => item.Sequence).AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (latest is not null)
            throw Conflict("TENDER_SECURITY_ALREADY_ACTIONED",
                "This security instrument already has an immutable terminal action.");
        if (!string.IsNullOrWhiteSpace(request.ExpectedLatestActionIntegrityHash))
            throw Conflict("TENDER_SECURITY_ACTION_STALE",
                "No prior action exists, but an expected action hash was supplied.");
        ValidateSecurityEligibility(register, recipient, request.ActionType);
        var workflow = await ValidateCompletedWorkflowAsync(
            request.WorkflowInstanceId, security.Id, cancellationToken);
        await ValidateEvidenceAsync(request.EvidenceWorkflowDocumentId,
            request.EvidenceFileUploadRecordId, cancellationToken);
        await EnforceSodAsync(register.SourceReference, security.RegisteredByUserId,
            correlation, cancellationToken);
        var now = DateTime.UtcNow;
        var action = new ProcurementTenderSecurityAction
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            SecurityInstrumentId = security.Id,
            Sequence = 1,
            ActionType = request.ActionType,
            WorkflowDefinitionId = workflow.WorkflowDefinitionId,
            WorkflowInstanceId = workflow.Id,
            ActionReference = request.ActionReference.Trim(),
            Reason = request.Reason.Trim(),
            EvidenceReference = request.EvidenceReference.Trim(),
            EvidenceWorkflowDocumentId = request.EvidenceWorkflowDocumentId,
            EvidenceFileUploadRecordId = request.EvidenceFileUploadRecordId,
            ActionedAtUtc = workflow.CompletedDate!.Value,
            ActionedByUserId = _currentUser.UserId,
            ActionedByName = ActorName(),
            IdempotencyKey = request.IdempotencyKey.Trim(),
            CorrelationId = correlation,
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId
        };
        action.IntegrityHash = Hash(Serialize(new
        {
            schemaVersion = "tdc.tender-security-action.v1",
            action.Id,
            action.TenantId,
            action.SecurityInstrumentId,
            action.Sequence,
            action.ActionType,
            action.WorkflowDefinitionId,
            action.WorkflowInstanceId,
            action.ActionReference,
            action.Reason,
            action.EvidenceReference,
            action.ActionedAtUtc,
            action.ActionedByUserId,
            action.IdempotencyKey,
            security.IntegrityHash
        }));
        await SecurityActions.AddAsync(action);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(register, "SecurityActionRecorded", ProcurementControlEventResult.Allowed,
            request.IdempotencyKey, new
            {
                recipientId = recipient.Id,
                recipient.BusinessPartnerId,
                recipient.Outcome,
                securityId = security.Id,
                security.InstrumentReference,
                actionId = action.Id,
                action.ActionType,
                action.WorkflowInstanceId,
                action.IntegrityHash
            }, request.Reason, correlation, now,
            Evidence(request.EvidenceReference, request.EvidenceWorkflowDocumentId,
                request.EvidenceFileUploadRecordId, "Tender security action", "SRC-012"),
            cancellationToken);
        await PublishAsync("procurement.bidder-communication.security-action",
            register, recipient,
            new
            {
                securityId = security.Id,
                security.InstrumentReference,
                actionId = action.Id,
                action.ActionType,
                action.ActionReference
            }, cancellationToken);
        return MapSecurityAction(action);
    }

    private async Task<ProcurementBidderCommunicationAcknowledgementDto>
        RecordAcknowledgementCoreAsync(
            Guid dispatchId,
            RecordProcurementBidderCommunicationAcknowledgementRequest request,
            string correlationId,
            bool external,
            CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        RequireIdempotency(request.IdempotencyKey);
        Require(request.AcknowledgementChannel, "BIDDER_ACKNOWLEDGEMENT_CHANNEL_REQUIRED",
            "AcknowledgementChannel is required.");
        Require(request.AcknowledgementReference, "BIDDER_ACKNOWLEDGEMENT_REFERENCE_REQUIRED",
            "AcknowledgementReference is required.");
        Require(request.EvidenceReference, "BIDDER_ACKNOWLEDGEMENT_EVIDENCE_REQUIRED",
            "Acknowledgement evidence is required.");
        var correlation = NormalizeCorrelation(correlationId);
        var dispatch = await LoadDispatchAsync(dispatchId, cancellationToken);
        var recipient = dispatch.LetterVersion.Recipient;
        var register = recipient.Register;
        Guid? externalPartnerId = null;
        if (external)
        {
            EnsureExternalReader();
            var partnerIds = await GetExternalPartnerIdsAsync(cancellationToken);
            if (!partnerIds.Contains(recipient.BusinessPartnerId))
                throw Authorization("The dispatch does not belong to the current supplier account.");
            externalPartnerId = recipient.BusinessPartnerId;
        }
        else
        {
            await EnsureCapabilityAsync(ManagePermission, register.SourceReference,
                correlation, cancellationToken);
        }
        var existing = await AcknowledgementQuery().AsNoTracking()
            .SingleOrDefaultAsync(item => item.DispatchId == dispatchId &&
                item.IdempotencyKey == request.IdempotencyKey.Trim(), cancellationToken);
        if (existing is not null) return MapAcknowledgement(existing);
        var previous = await AcknowledgementQuery().Where(item => item.DispatchId == dispatchId)
            .OrderByDescending(item => item.Sequence).AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var acknowledgement = new ProcurementBidderCommunicationAcknowledgement
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            DispatchId = dispatch.Id,
            Sequence = (previous?.Sequence ?? 0) + 1,
            Outcome = request.Outcome,
            AcknowledgedAtUtc = now,
            AcknowledgedByUserId = _currentUser.UserId,
            AcknowledgedByBusinessPartnerId = externalPartnerId,
            AcknowledgementChannel = request.AcknowledgementChannel.Trim(),
            AcknowledgementReference = request.AcknowledgementReference.Trim(),
            EvidenceReference = request.EvidenceReference.Trim(),
            IdempotencyKey = request.IdempotencyKey.Trim(),
            CorrelationId = correlation,
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId
        };
        acknowledgement.IntegrityHash = Hash(Serialize(new
        {
            schemaVersion = "tdc.bidder-letter-acknowledgement.v1",
            acknowledgement.Id,
            acknowledgement.TenantId,
            acknowledgement.DispatchId,
            acknowledgement.Sequence,
            acknowledgement.Outcome,
            acknowledgement.AcknowledgedAtUtc,
            acknowledgement.AcknowledgedByUserId,
            acknowledgement.AcknowledgedByBusinessPartnerId,
            acknowledgement.AcknowledgementChannel,
            acknowledgement.AcknowledgementReference,
            acknowledgement.EvidenceReference,
            acknowledgement.IdempotencyKey,
            previousHash = previous?.IntegrityHash,
            dispatch.IntegrityHash
        }));
        await Acknowledgements.AddAsync(acknowledgement);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(register, "AcknowledgementRecorded",
            ProcurementControlEventResult.Allowed, request.IdempotencyKey, new
            {
                recipientId = recipient.Id,
                recipient.BusinessPartnerId,
                dispatchId = dispatch.Id,
                acknowledgementId = acknowledgement.Id,
                acknowledgement.Sequence,
                acknowledgement.Outcome,
                acknowledgement.AcknowledgedByBusinessPartnerId,
                acknowledgement.IntegrityHash
            }, request.AcknowledgementReference, correlation, now,
            Evidence(request.EvidenceReference, null, null,
                "Bidder letter acknowledgement", "SRC-011"), cancellationToken);
        await PublishAsync("procurement.bidder-communication.acknowledged",
            register, recipient,
            new
            {
                dispatchId = dispatch.Id,
                acknowledgementId = acknowledgement.Id,
                acknowledgement.Outcome
            },
            cancellationToken);
        return MapAcknowledgement(acknowledgement);
    }

    private async Task<ProcurementBidderAppealDto> FileAppealCoreAsync(
        Guid recipientId,
        FileProcurementBidderAppealRequest request,
        string correlationId,
        bool external,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        RequireIdempotency(request.IdempotencyKey);
        Require(request.Grounds, "BIDDER_APPEAL_GROUNDS_REQUIRED", "Appeal grounds are required.");
        Require(request.EvidenceReference, "BIDDER_APPEAL_EVIDENCE_REQUIRED",
            "Appeal evidence is required.");
        var correlation = NormalizeCorrelation(correlationId);
        var recipient = await LoadRecipientAsync(recipientId, cancellationToken);
        var register = recipient.Register;
        Guid? externalPartnerId = null;
        if (external)
        {
            EnsureExternalReader();
            var partnerIds = await GetExternalPartnerIdsAsync(cancellationToken);
            if (!partnerIds.Contains(recipient.BusinessPartnerId))
                throw Authorization("The recipient does not belong to the current supplier account.");
            externalPartnerId = recipient.BusinessPartnerId;
        }
        else
        {
            await EnsureCapabilityAsync(ManagePermission, register.SourceReference,
                correlation, cancellationToken);
        }
        if (recipient.Outcome != ProcurementBidderCommunicationRecipientOutcome.Unsuccessful)
            throw Validation("BIDDER_APPEAL_OUTCOME_INELIGIBLE",
                "Only an unsuccessful bidder may file an appeal against this award.");
        var now = DateTime.UtcNow;
        if (now < register.StandstillStartsAtUtc || now > register.AppealWindowEndsAtUtc)
            throw Conflict("BIDDER_APPEAL_WINDOW_CLOSED",
                "The configured appeal window is not open.");
        if (!await DispatchQuery().AnyAsync(item =>
                item.LetterVersion.RecipientId == recipient.Id, cancellationToken))
            throw Conflict("BIDDER_APPEAL_COMMUNICATION_REQUIRED",
                "The approved bidder communication must be dispatched before an appeal is filed.");
        var existing = await AppealQuery().AsNoTracking()
            .SingleOrDefaultAsync(item => item.RecipientId == recipientId &&
                item.IdempotencyKey == request.IdempotencyKey.Trim(), cancellationToken);
        if (existing is not null) return MapAppeal(existing);
        if (await AppealQuery().AnyAsync(item => item.RecipientId == recipientId &&
            !item.Decisions.Any(), cancellationToken))
            throw Conflict("BIDDER_APPEAL_ALREADY_OPEN",
                "An unresolved appeal already exists for this recipient.");
        await ValidateEvidenceAsync(request.EvidenceWorkflowDocumentId,
            request.EvidenceFileUploadRecordId, cancellationToken);
        var sequence = (await AppealQuery().Where(item => item.RecipientId == recipientId)
            .Select(item => (int?)item.Sequence).MaxAsync(cancellationToken) ?? 0) + 1;
        var appeal = new ProcurementBidderAppeal
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            RecipientId = recipient.Id,
            Sequence = sequence,
            Grounds = request.Grounds.Trim(),
            EvidenceReference = request.EvidenceReference.Trim(),
            EvidenceWorkflowDocumentId = request.EvidenceWorkflowDocumentId,
            EvidenceFileUploadRecordId = request.EvidenceFileUploadRecordId,
            FiledAtUtc = now,
            FiledByUserId = _currentUser.UserId,
            FiledByBusinessPartnerId = externalPartnerId,
            IdempotencyKey = request.IdempotencyKey.Trim(),
            CorrelationId = correlation,
            CreatedAt = now,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId
        };
        appeal.IntegrityHash = Hash(Serialize(new
        {
            schemaVersion = "tdc.bidder-appeal.v1",
            appeal.Id,
            appeal.TenantId,
            appeal.RecipientId,
            appeal.Sequence,
            appeal.Grounds,
            appeal.EvidenceReference,
            appeal.FiledAtUtc,
            appeal.FiledByUserId,
            appeal.FiledByBusinessPartnerId,
            appeal.IdempotencyKey,
            recipient.IntegrityHash
        }));
        await Appeals.AddAsync(appeal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecordEventAsync(register, "AppealFiled", ProcurementControlEventResult.Allowed,
            request.IdempotencyKey, new
            {
                recipientId = recipient.Id,
                recipient.BusinessPartnerId,
                appealId = appeal.Id,
                appeal.Sequence,
                appeal.FiledAtUtc,
                appeal.FiledByBusinessPartnerId,
                appeal.IntegrityHash
            }, request.Grounds, correlation, now,
            Evidence(request.EvidenceReference, request.EvidenceWorkflowDocumentId,
                request.EvidenceFileUploadRecordId, "Bidder appeal", "SRC-011"),
            cancellationToken);
        await PublishAsync("procurement.bidder-communication.appeal-filed",
            register, recipient, new { appeal.Id, appeal.Sequence, appeal.FiledAtUtc },
            cancellationToken);
        return MapAppeal(appeal);
    }

    private async Task<SourceResolution> ResolveSourceAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        return sourceType switch
        {
            ProcurementAwardReadinessSourceType.RequestForQuotation =>
                await ResolveRfqAsync(sourceId, cancellationToken),
            ProcurementAwardReadinessSourceType.Tender =>
                await ResolveTenderAsync(sourceId, exceptional: false, cancellationToken),
            ProcurementAwardReadinessSourceType.ExceptionalSourcing =>
                await ResolveTenderAsync(sourceId, exceptional: true, cancellationToken),
            _ => throw Validation("BIDDER_COMMUNICATION_SOURCE_TYPE_INVALID",
                "Unsupported bidder-communication source type.")
        };
    }

    private async Task<SourceResolution> ResolveRfqAsync(
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var rfq = await _unitOfWork.Repository<RequestForQuotation>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.Id == sourceId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("RFQ_NOT_FOUND", "The RFQ was not found in the current tenant.");
        if (!string.Equals(rfq.Status, "Awarded", StringComparison.OrdinalIgnoreCase) ||
            !rfq.AwardedAt.HasValue)
            throw Conflict("BIDDER_COMMUNICATION_AWARD_MISSING",
                "The RFQ does not have a completed award.");
        var awardLines = await _unitOfWork.Repository<RequestForQuotationAwardLine>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.RfqId == sourceId && !item.IsDeleted)
            .AsNoTracking().ToListAsync(cancellationToken);
        if (awardLines.Count == 0)
            throw Conflict("BIDDER_COMMUNICATION_AWARD_LINEAGE_MISSING",
                "The RFQ award has no exact award-line lineage.");
        var quotes = await _unitOfWork.Repository<RequestForQuotationQuote>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.RfqId == sourceId && !item.IsDeleted &&
                item.Status == "Submitted")
            .AsNoTracking().ToListAsync(cancellationToken);
        if (quotes.Count == 0)
            throw Conflict("BIDDER_COMMUNICATION_RECIPIENT_LINEAGE_MISSING",
                "No submitted RFQ recipient lineage can be derived.");
        var winners = awardLines.Select(item => item.BusinessPartnerId).Distinct().ToHashSet();
        if (rfq.AwardedBusinessPartnerId.HasValue &&
            !winners.Contains(rfq.AwardedBusinessPartnerId.Value))
            throw Conflict("BIDDER_COMMUNICATION_AWARD_LINEAGE_AMBIGUOUS",
                "RFQ header and award-line winners contradict each other.");
        return await BuildSourceResolutionAsync(
            ProcurementAwardReadinessSourceType.RequestForQuotation,
            rfq.Id, rfq.RfqNumber,
            ProcurementBidderCommunicationAwardFamily.RequestForQuotation,
            rfq.Id, $"{rfq.RfqNumber}-AWARD", EnsureUtc(rfq.AwardedAt.Value),
            quotes.Select(item => new Candidate(item.Id, item.BusinessPartnerId)).ToList(),
            winners,
            new
            {
                rfq.Id,
                rfq.RfqNumber,
                rfq.Status,
                awardedAtUtc = EnsureUtc(rfq.AwardedAt.Value),
                rfq.AwardedBusinessPartnerId,
                awardLines = awardLines.OrderBy(item => item.Id).Select(item => new
                {
                    item.Id, item.RfqItemId, item.QuoteId, item.BusinessPartnerId,
                    item.UnitPrice, item.LineTotal
                })
            }, cancellationToken);
    }

    private async Task<SourceResolution> ResolveTenderAsync(
        Guid sourceId,
        bool exceptional,
        CancellationToken cancellationToken)
    {
        var tender = await _unitOfWork.Repository<Tender>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.Id == sourceId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("TENDER_NOT_FOUND", "The tender was not found in the current tenant.");
        var formal = await _unitOfWork.Repository<ProcurementTenderControl>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.TenderId == sourceId && !item.IsDeleted)
            .AsNoTracking().ToListAsync(cancellationToken);
        var exceptionalRows = await _unitOfWork.Repository<ProcurementExceptionalSourcingControl>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.TenderId == sourceId && !item.IsDeleted)
            .AsNoTracking().ToListAsync(cancellationToken);
        if (formal.Count > 1 || exceptionalRows.Count > 1 || formal.Count != 0 && exceptionalRows.Count != 0)
            throw Conflict("BIDDER_COMMUNICATION_SOURCE_LINEAGE_AMBIGUOUS",
                "The tender has duplicate or contradictory award-control lineage.");
        if (exceptional)
        {
            if (exceptionalRows.Count != 1)
                throw NotFound("EXCEPTIONAL_CONTROL_NOT_FOUND",
                    "No exceptional-sourcing award exists for this tender.");
            return await ResolveControlledTenderAsync(tender, exceptionalRows[0], cancellationToken);
        }
        if (exceptionalRows.Count != 0)
            throw Validation("BIDDER_COMMUNICATION_SOURCE_TYPE_MISMATCH",
                "This tender is controlled by ExceptionalSourcing.");
        if (formal.Count == 1)
            return await ResolveControlledTenderAsync(tender, formal[0], cancellationToken);
        return await ResolveLegacyTenderAsync(tender, cancellationToken);
    }

    private async Task<SourceResolution> ResolveControlledTenderAsync(
        Tender tender,
        ProcurementTenderControl control,
        CancellationToken cancellationToken)
    {
        if (control.Status < ProcurementTenderControlStatus.Awarded ||
            !control.AwardBidId.HasValue || !control.AwardedAtUtc.HasValue ||
            string.IsNullOrWhiteSpace(control.AwardReference))
            throw Conflict("BIDDER_COMMUNICATION_AWARD_MISSING",
                "The formal tender does not have a complete controlled award.");
        var receipts = await _unitOfWork.Repository<ProcurementTenderSubmissionReceipt>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.TenderControlId == control.Id && !item.IsDeleted &&
                item.Disposition == ProcurementTenderSubmissionDisposition.OnTimeAccepted)
            .AsNoTracking().ToListAsync(cancellationToken);
        var candidates = receipts.Select(item => new Candidate(
            item.TenderBidId, item.BusinessPartnerId)).ToList();
        var winner = candidates.SingleOrDefault(item => item.SubjectId == control.AwardBidId.Value);
        if (winner is null)
            throw Conflict("BIDDER_COMMUNICATION_AWARD_LINEAGE_AMBIGUOUS",
                "The awarded bid is not an exact accepted submission.");
        return await BuildSourceResolutionAsync(
            ProcurementAwardReadinessSourceType.Tender,
            tender.Id, tender.TenderNumber,
            ProcurementBidderCommunicationAwardFamily.FormalTender,
            control.Id, control.AwardReference!, EnsureUtc(control.AwardedAtUtc.Value),
            candidates, new HashSet<Guid> { winner.BusinessPartnerId },
            new
            {
                control.Id, control.TenderId, control.AwardBidId, control.AwardReference,
                control.AwardedAtUtc, control.AwardEvidenceReference, control.IntegrityHash,
                submissionReceiptIds = receipts.Select(item => item.Id).OrderBy(id => id)
            }, cancellationToken);
    }

    private async Task<SourceResolution> ResolveControlledTenderAsync(
        Tender tender,
        ProcurementExceptionalSourcingControl control,
        CancellationToken cancellationToken)
    {
        if (control.Status < ProcurementExceptionalSourcingControlStatus.Awarded ||
            !control.AwardBidId.HasValue || !control.AwardedAtUtc.HasValue ||
            string.IsNullOrWhiteSpace(control.AwardReference))
            throw Conflict("BIDDER_COMMUNICATION_AWARD_MISSING",
                "The exceptional-sourcing tender does not have a complete controlled award.");
        var bids = await _unitOfWork.Repository<TenderBid>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.TenderId == tender.Id && !item.IsDeleted &&
                item.Status != "Draft" && item.Status != "Withdrawn")
            .AsNoTracking().ToListAsync(cancellationToken);
        var candidates = bids.Select(item => new Candidate(item.Id, item.BusinessPartnerId)).ToList();
        var winner = candidates.SingleOrDefault(item => item.SubjectId == control.AwardBidId.Value);
        if (winner is null)
            throw Conflict("BIDDER_COMMUNICATION_AWARD_LINEAGE_AMBIGUOUS",
                "The exceptional award bid is not part of the retained source bids.");
        return await BuildSourceResolutionAsync(
            ProcurementAwardReadinessSourceType.ExceptionalSourcing,
            tender.Id, tender.TenderNumber,
            ProcurementBidderCommunicationAwardFamily.ExceptionalSourcing,
            control.Id, control.AwardReference!, EnsureUtc(control.AwardedAtUtc.Value),
            candidates, new HashSet<Guid> { winner.BusinessPartnerId },
            new
            {
                control.Id, control.TenderId, control.AwardBidId, control.AwardReference,
                control.AwardedAtUtc, control.AwardEvidenceReference, control.IntegrityHash
            }, cancellationToken);
    }

    private async Task<SourceResolution> ResolveLegacyTenderAsync(
        Tender tender,
        CancellationToken cancellationToken)
    {
        var awards = await _unitOfWork.Repository<TenderAward>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.TenderId == tender.Id && !item.IsDeleted &&
                item.Status != "Cancelled")
            .AsNoTracking().ToListAsync(cancellationToken);
        if (awards.Count == 0)
            throw Conflict("BIDDER_COMMUNICATION_AWARD_MISSING",
                "The legacy tender has no retained award.");
        var awardedAtUtc = tender.AwardDate.HasValue
            ? EnsureUtc(tender.AwardDate.Value)
            : awards.Max(item => EnsureUtc(item.AwardDate));
        if (awards.Any(item => EnsureUtc(item.AwardDate) > awardedAtUtc))
            throw Conflict("BIDDER_COMMUNICATION_AWARD_LINEAGE_AMBIGUOUS",
                "A legacy award is later than the server-derived tender award timestamp.");
        var bids = await _unitOfWork.Repository<TenderBid>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.TenderId == tender.Id && !item.IsDeleted &&
                item.Status != "Draft" && item.Status != "Withdrawn")
            .AsNoTracking().ToListAsync(cancellationToken);
        var candidates = bids.Select(item => new Candidate(item.Id, item.BusinessPartnerId)).ToList();
        var winners = awards.Select(item => item.BusinessPartnerId).Distinct().ToHashSet();
        if (awards.Any(award => !candidates.Any(candidate =>
                candidate.SubjectId == award.TenderBidId &&
                candidate.BusinessPartnerId == award.BusinessPartnerId)))
            throw Conflict("BIDDER_COMMUNICATION_AWARD_LINEAGE_AMBIGUOUS",
                "A legacy award does not match its retained bid and supplier.");
        return await BuildSourceResolutionAsync(
            ProcurementAwardReadinessSourceType.Tender,
            tender.Id, tender.TenderNumber,
            ProcurementBidderCommunicationAwardFamily.LegacyTenderAward,
            tender.Id, $"{tender.TenderNumber}-LEGACY-AWARD", awardedAtUtc,
            candidates, winners,
            new
            {
                tender.Id,
                tender.TenderNumber,
                awards = awards.OrderBy(item => item.Id).Select(item => new
                {
                    item.Id, item.TenderBidId, item.BusinessPartnerId, item.LotId,
                    item.AwardDate, item.AwardedAmount, item.Currency, item.Status
                })
            }, cancellationToken);
    }

    private async Task<SourceResolution> BuildSourceResolutionAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        string sourceReference,
        ProcurementBidderCommunicationAwardFamily awardFamily,
        Guid awardId,
        string awardReference,
        DateTime awardedAtUtc,
        IReadOnlyCollection<Candidate> candidates,
        IReadOnlySet<Guid> winners,
        object awardSnapshot,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0 || winners.Count == 0)
            throw Conflict("BIDDER_COMMUNICATION_RECIPIENT_LINEAGE_MISSING",
                "Successful and bidder recipient lineage cannot be derived.");
        if (winners.Any(winner => candidates.All(item => item.BusinessPartnerId != winner)))
            throw Conflict("BIDDER_COMMUNICATION_AWARD_LINEAGE_AMBIGUOUS",
                "An awarded supplier does not occur in the retained bidder lineage.");
        var partnerIds = candidates.Select(item => item.BusinessPartnerId).Distinct().ToList();
        var partners = await _unitOfWork.Repository<BusinessPartner>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                partnerIds.Contains(item.Id) && !item.IsDeleted)
            .AsNoTracking().ToListAsync(cancellationToken);
        if (partners.Count != partnerIds.Count ||
            partners.GroupBy(item => item.Id).Any(group => group.Count() != 1))
            throw Conflict("BIDDER_COMMUNICATION_SUPPLIER_LINEAGE_AMBIGUOUS",
                "One or more bidder suppliers are missing, foreign, or duplicated.");
        var recipients = partners.Select(partner =>
        {
            var subjectIds = candidates.Where(item => item.BusinessPartnerId == partner.Id)
                .Select(item => item.SubjectId).Distinct().OrderBy(id => id).ToList();
            var outcome = winners.Contains(partner.Id)
                ? ProcurementBidderCommunicationRecipientOutcome.Successful
                : ProcurementBidderCommunicationRecipientOutcome.Unsuccessful;
            var lineageHash = Hash(Serialize(new
            {
                sourceType, sourceId, awardFamily, awardId, partner.Id, outcome, subjectIds,
                partner.PartnerCode, partner.PartnerName, partner.PrimaryEmail, partner.PrimaryPhone
            }));
            return new ResolvedRecipient(partner.Id, outcome, subjectIds,
                partner.PartnerCode, partner.PartnerName,
                TrimOrNull(partner.PrimaryEmail, 320), TrimOrNull(partner.PrimaryPhone, 30),
                lineageHash);
        }).OrderBy(item => item.BusinessPartnerId).ToList();
        return new SourceResolution(sourceType, sourceId, sourceReference, awardFamily,
            awardId, awardReference.Trim(), awardedAtUtc, recipients, awardSnapshot);
    }

    private static void ValidateReadiness(
        ProcurementAwardReadinessDto readiness,
        SourceResolution source,
        InitializeProcurementBidderCommunicationRegisterRequest request)
    {
        if (!readiness.IsReady)
            throw Conflict("BIDDER_COMMUNICATION_READINESS_NOT_READY",
                "The retained award-readiness decision is not Ready.");
        if (readiness.Id != request.ExpectedAwardReadinessDecisionId ||
            !string.Equals(readiness.IntegrityHash,
                NormalizeHash(request.ExpectedAwardReadinessIntegrityHash),
                StringComparison.OrdinalIgnoreCase))
            throw Conflict("BIDDER_COMMUNICATION_READINESS_STALE",
                "The expected award-readiness decision does not match the retained decision.");
        if (readiness.SourceType != source.SourceType || readiness.SourceId != source.SourceId ||
            !string.Equals(readiness.SourceReference, source.SourceReference,
                StringComparison.OrdinalIgnoreCase))
            throw Conflict("BIDDER_COMMUNICATION_READINESS_FOREIGN",
                "The retained award-readiness decision belongs to a different source.");
        var successful = source.Recipients
            .Where(item => item.Outcome == ProcurementBidderCommunicationRecipientOutcome.Successful)
            .Select(item => item.BusinessPartnerId).OrderBy(id => id).ToList();
        var recommended = readiness.Recommendation.BusinessPartnerIds
            .Distinct().OrderBy(id => id).ToList();
        if (!successful.SequenceEqual(recommended))
            throw Conflict("BIDDER_COMMUNICATION_AWARD_READINESS_MISMATCH",
                "The server-derived award recipients do not match the exact readiness recommendation.");
    }

    private async Task<ProcurementTenderDocumentTemplateVersion> LoadApprovedLetterTemplateAsync(
        Guid templateVersionId,
        ProcurementBidderCommunicationRecipientOutcome outcome,
        CancellationToken cancellationToken)
    {
        if (templateVersionId == Guid.Empty)
            throw Validation("BIDDER_LETTER_TEMPLATE_REQUIRED", "TemplateVersionId is required.");
        var template = await _unitOfWork.Repository<ProcurementTenderDocumentTemplateVersion>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.Id == templateVersionId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("BIDDER_LETTER_TEMPLATE_NOT_FOUND",
                "The letter template was not found in the current tenant.");
        var expectedCode = outcome == ProcurementBidderCommunicationRecipientOutcome.Successful
            ? "SUCCESSFUL_BIDDER_LETTER"
            : "UNSUCCESSFUL_BIDDER_LETTER";
        var now = DateTime.UtcNow;
        if (template.Status != ProcurementTenderDocumentTemplateStatus.Published ||
            template.EffectiveFromUtc > now ||
            template.EffectiveToUtc.HasValue && template.EffectiveToUtc.Value < now ||
            !string.Equals(template.DocumentTypeCode, expectedCode,
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(template.ContentReference) ||
            !IsHash(template.ContentChecksumSha256) ||
            !template.PublishedAtUtc.HasValue ||
            !template.PublishedById.HasValue ||
            string.IsNullOrWhiteSpace(template.ApprovalEvidenceReference))
            throw Conflict("BIDDER_LETTER_TEMPLATE_NOT_APPROVED",
                $"An effective approved {expectedCode} template version is required.");
        return template;
    }

    private async Task<WorkflowInstance> ValidateCompletedWorkflowAsync(
        Guid workflowInstanceId,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        if (workflowInstanceId == Guid.Empty)
            throw Validation("BIDDER_COMMUNICATION_WORKFLOW_REQUIRED",
                "WorkflowInstanceId is required.");
        var workflow = await _unitOfWork.Repository<WorkflowInstance>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.Id == workflowInstanceId && !item.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("BIDDER_COMMUNICATION_WORKFLOW_NOT_FOUND",
                "The workflow instance was not found in the current tenant.");
        if (workflow.EntityId != entityId ||
            workflow.Status != WorkflowInstanceStatus.Completed ||
            !workflow.CompletedDate.HasValue)
            throw Conflict("BIDDER_COMMUNICATION_WORKFLOW_NOT_APPROVED",
                "The exact source-bound workflow must have an approved Completed outcome.");
        return workflow;
    }

    private async Task ValidateEvidenceAsync(
        Guid? workflowEvidenceDocumentId,
        Guid? fileUploadRecordId,
        CancellationToken cancellationToken)
    {
        if (!workflowEvidenceDocumentId.HasValue && !fileUploadRecordId.HasValue) return;
        if (workflowEvidenceDocumentId.HasValue)
        {
            var evidence = await _unitOfWork.Repository<WorkflowEvidenceDocument>()
                .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                    item.Id == workflowEvidenceDocumentId.Value && !item.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw NotFound("BIDDER_COMMUNICATION_EVIDENCE_NOT_FOUND",
                    "Workflow evidence was not found in the current tenant.");
            if (!evidence.IsCurrent ||
                evidence.VerificationStatus != WorkflowEvidenceVerificationStatus.Verified ||
                evidence.MalwareScanStatus != WorkflowMalwareScanStatus.Clean)
                throw Conflict("BIDDER_COMMUNICATION_EVIDENCE_NOT_APPROVED",
                    "Workflow evidence must be current, verified, and malware-clean.");
        }
        if (fileUploadRecordId.HasValue)
        {
            var upload = await _unitOfWork.Repository<FileUploadRecord>()
                .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                    item.Id == fileUploadRecordId.Value && !item.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw NotFound("BIDDER_COMMUNICATION_UPLOAD_NOT_FOUND",
                    "Uploaded evidence was not found in the current tenant.");
            if (upload.VirusScanStatus is FileVirusScanStatus.Infected or FileVirusScanStatus.Error)
                throw Conflict("BIDDER_COMMUNICATION_UPLOAD_UNSAFE",
                    "Uploaded evidence failed its safety check.");
        }
    }

    private void ValidateSecurityEligibility(
        ProcurementBidderCommunicationRegister register,
        ProcurementBidderCommunicationRecipient recipient,
        ProcurementTenderSecurityActionType action)
    {
        var now = DateTime.UtcNow;
        if (now < register.StandstillEndsAtUtc || now < register.AppealWindowEndsAtUtc)
            throw Conflict("TENDER_SECURITY_WINDOW_OPEN",
                "Tender security cannot be actioned until the standstill and appeal windows have elapsed.");
        if (!recipient.LetterVersions.SelectMany(item => item.Dispatches).Any())
            throw Conflict("TENDER_SECURITY_COMMUNICATION_REQUIRED",
                "The approved bidder communication must be dispatched before security action.");
        var appeals = recipient.Appeals.Where(item => !item.IsDeleted).ToList();
        if (appeals.Any(item => item.Decisions.Count(decision => !decision.IsDeleted) != 1))
            throw Conflict("TENDER_SECURITY_APPEAL_UNRESOLVED",
                "Tender security cannot be actioned while an appeal is unresolved or ambiguous.");
        if (appeals.SelectMany(item => item.Decisions)
            .Any(item => !item.IsDeleted && item.Outcome == ProcurementBidderAppealOutcome.Upheld))
            throw Conflict("TENDER_SECURITY_APPEAL_UPHELD",
                "Tender security cannot be actioned against an award with an upheld appeal.");
        if (recipient.Outcome == ProcurementBidderCommunicationRecipientOutcome.Successful &&
            action != ProcurementTenderSecurityActionType.Released)
            throw Validation("TENDER_SECURITY_ACTION_OUTCOME_MISMATCH",
                "A successful bidder's tender security must be released.");
        if (recipient.Outcome == ProcurementBidderCommunicationRecipientOutcome.Unsuccessful &&
            action == ProcurementTenderSecurityActionType.Released)
            throw Validation("TENDER_SECURITY_ACTION_OUTCOME_MISMATCH",
                "An unsuccessful bidder's tender security must be returned or validly forfeited.");
    }

    private async Task EnforceSodAsync(
        string sourceReference,
        Guid prohibitedActorId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var decision = await _sodGuard.EnforceAsync(new ProcurementSodGuardRequest
        {
            ControlCode = InitiatorApproverSod,
            SourceType = EventType,
            SourceReference = sourceReference,
            ProhibitedActorUserIds = [prohibitedActorId]
        }, correlationId, cancellationToken);
        if (!decision.Allowed)
            throw Authorization(decision.Message);
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw Authorization("Supplier portal users cannot perform internal bidder-control actions.");
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

    private async Task<IReadOnlySet<Guid>> GetExternalPartnerIdsAsync(
        CancellationToken cancellationToken) =>
        (await _unitOfWork.Repository<BusinessPartnerUser>()
            .GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.UserId == _currentUser.UserId && item.IsActive && !item.IsDeleted &&
                item.BusinessPartner.TenantId == _currentUser.TenantId &&
                !item.BusinessPartner.IsDeleted && item.BusinessPartner.IsActive &&
                !item.BusinessPartner.IsBlacklisted &&
                item.BusinessPartner.ApprovalStatus ==
                    BusinessPartnerLifecyclePolicy.ApprovedApprovalStatus &&
                (item.BusinessPartner.RegistrationStatus ==
                     BusinessPartnerLifecyclePolicy.ActiveRegistrationStatus ||
                 item.BusinessPartner.RegistrationStatus ==
                     BusinessPartnerLifecyclePolicy.LegacyApprovedRegistrationStatus) &&
                BusinessPartnerRoles.ProcurementTypes.Contains(item.BusinessPartner.PartnerType))
            .Select(item => item.BusinessPartnerId)
            .ToListAsync(cancellationToken)).ToHashSet();

    private IQueryable<ProcurementBidderCommunicationRegister> RegisterQuery() =>
        Registers.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Recipients.Where(recipient => !recipient.IsDeleted))
                .ThenInclude(item => item.LetterVersions.Where(letter => !letter.IsDeleted))
                    .ThenInclude(item => item.Dispatches.Where(dispatch => !dispatch.IsDeleted))
                        .ThenInclude(item => item.Deliveries.Where(delivery => !delivery.IsDeleted))
            .Include(item => item.Recipients.Where(recipient => !recipient.IsDeleted))
                .ThenInclude(item => item.LetterVersions.Where(letter => !letter.IsDeleted))
                    .ThenInclude(item => item.Dispatches.Where(dispatch => !dispatch.IsDeleted))
                        .ThenInclude(item => item.Acknowledgements.Where(ack => !ack.IsDeleted))
            .Include(item => item.Recipients.Where(recipient => !recipient.IsDeleted))
                .ThenInclude(item => item.Appeals.Where(appeal => !appeal.IsDeleted))
                    .ThenInclude(item => item.Decisions.Where(decision => !decision.IsDeleted))
            .Include(item => item.Recipients.Where(recipient => !recipient.IsDeleted))
                .ThenInclude(item => item.SecurityInstruments.Where(security => !security.IsDeleted))
                    .ThenInclude(item => item.Actions.Where(action => !action.IsDeleted));

    private IQueryable<ProcurementBidderCommunicationLetterVersion> LetterQuery() =>
        Letters.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted);
    private IQueryable<ProcurementBidderCommunicationDispatch> DispatchQuery() =>
        Dispatches.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted);
    private IQueryable<ProcurementBidderCommunicationDelivery> DeliveryQuery() =>
        Deliveries.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted);
    private IQueryable<ProcurementBidderCommunicationAcknowledgement> AcknowledgementQuery() =>
        Acknowledgements.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted);
    private IQueryable<ProcurementBidderAppeal> AppealQuery() =>
        Appeals.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted);
    private IQueryable<ProcurementBidderAppealDecision> AppealDecisionQuery() =>
        AppealDecisions.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted);
    private IQueryable<ProcurementTenderSecurityInstrument> SecurityQuery() =>
        Securities.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted);
    private IQueryable<ProcurementTenderSecurityAction> SecurityActionQuery() =>
        SecurityActions.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted);

    private async Task<ProcurementBidderCommunicationRegister> LoadRegisterBySourceAsync(
        ProcurementAwardReadinessSourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken) =>
        await RegisterQuery().AsNoTracking().SingleOrDefaultAsync(
            item => item.SourceType == sourceType && item.SourceId == sourceId, cancellationToken)
        ?? throw NotFound("BIDDER_COMMUNICATION_REGISTER_NOT_FOUND",
            "The bidder communication register was not found in the current tenant.");

    private async Task<ProcurementBidderCommunicationRegister> LoadRegisterByIdAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await RegisterQuery().AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw NotFound("BIDDER_COMMUNICATION_REGISTER_NOT_FOUND",
            "The bidder communication register was not found in the current tenant.");

    private async Task<ProcurementBidderCommunicationRecipient> LoadRecipientAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await Recipients.GetQueryable(item => item.TenantId == _currentUser.TenantId &&
                item.Id == id && !item.IsDeleted)
            .Include(item => item.Register)
            .Include(item => item.LetterVersions.Where(letter => !letter.IsDeleted))
                .ThenInclude(item => item.Dispatches.Where(dispatch => !dispatch.IsDeleted))
            .Include(item => item.Appeals.Where(appeal => !appeal.IsDeleted))
                .ThenInclude(item => item.Decisions.Where(decision => !decision.IsDeleted))
            .Include(item => item.SecurityInstruments.Where(security => !security.IsDeleted))
                .ThenInclude(item => item.Actions.Where(action => !action.IsDeleted))
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
        ?? throw NotFound("BIDDER_COMMUNICATION_RECIPIENT_NOT_FOUND",
            "The bidder recipient was not found in the current tenant.");

    private async Task<ProcurementBidderCommunicationLetterVersion> LoadLetterAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await LetterQuery().Include(item => item.Recipient).ThenInclude(item => item.Register)
            .AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw NotFound("BIDDER_LETTER_NOT_FOUND",
            "The approved bidder letter was not found in the current tenant.");

    private async Task<ProcurementBidderCommunicationDispatch> LoadDispatchAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await DispatchQuery()
            .Include(item => item.LetterVersion).ThenInclude(item => item.Recipient)
                .ThenInclude(item => item.Register)
            .AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw NotFound("BIDDER_DISPATCH_NOT_FOUND",
            "The bidder-letter dispatch was not found in the current tenant.");

    private async Task<ProcurementBidderAppeal> LoadAppealAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await AppealQuery().Include(item => item.Recipient).ThenInclude(item => item.Register)
            .AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw NotFound("BIDDER_APPEAL_NOT_FOUND",
            "The bidder appeal was not found in the current tenant.");

    private async Task<ProcurementTenderSecurityInstrument> LoadSecurityAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await SecurityQuery()
            .Include(item => item.Recipient).ThenInclude(item => item.Register)
            .Include(item => item.Recipient).ThenInclude(item => item.LetterVersions)
                .ThenInclude(item => item.Dispatches)
            .Include(item => item.Recipient).ThenInclude(item => item.Appeals)
                .ThenInclude(item => item.Decisions)
            .Include(item => item.Actions)
            .AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw NotFound("TENDER_SECURITY_NOT_FOUND",
            "The tender-security instrument was not found in the current tenant.");

    private async Task<ProcurementBidderCommunicationOverviewDto> MapOverviewAsync(
        ProcurementBidderCommunicationRegister register,
        IReadOnlySet<Guid>? externalPartnerIds,
        CancellationToken cancellationToken)
    {
        var latestReadiness = externalPartnerIds is null
            ? await _awardReadiness.GetLatestAsync(
                register.SourceType, register.SourceId, cancellationToken)
            : null;
        var recipients = register.Recipients
            .Where(item => externalPartnerIds is null ||
                           externalPartnerIds.Contains(item.BusinessPartnerId))
            .OrderBy(item => item.Outcome).ThenBy(item => item.PartnerName)
            .Select(item => MapRecipient(item, register)).ToList();
        var now = DateTime.UtcNow;
        var hasOpenAppeals = recipients.SelectMany(item => item.Appeals)
            .Any(item => item.Decision is null);
        return new ProcurementBidderCommunicationOverviewDto
        {
            Id = register.Id,
            SourceType = register.SourceType,
            SourceId = register.SourceId,
            SourceReference = register.SourceReference,
            AwardFamily = register.AwardFamily,
            AwardId = register.AwardId,
            AwardReference = register.AwardReference,
            AwardedAtUtc = register.AwardedAtUtc,
            AwardReadinessDecisionId = register.AwardReadinessDecisionId,
            AwardReadinessDecisionSequence = register.AwardReadinessDecisionSequence,
            AwardReadinessIntegrityHash = register.AwardReadinessIntegrityHash,
            AwardReadinessSourceIntegrityHash = register.AwardReadinessSourceIntegrityHash,
            AwardReadinessIsCurrent = latestReadiness is not null &&
                latestReadiness.Id == register.AwardReadinessDecisionId &&
                string.Equals(latestReadiness.IntegrityHash, register.AwardReadinessIntegrityHash,
                    StringComparison.OrdinalIgnoreCase),
            StandstillStartsAtUtc = register.StandstillStartsAtUtc,
            StandstillEndsAtUtc = register.StandstillEndsAtUtc,
            AppealWindowEndsAtUtc = register.AppealWindowEndsAtUtc,
            StandstillAuthorityReference = register.StandstillAuthorityReference,
            StandstillElapsed = now >= register.StandstillEndsAtUtc,
            AppealWindowOpen = now >= register.StandstillStartsAtUtc &&
                               now <= register.AppealWindowEndsAtUtc,
            HasOpenAppeals = hasOpenAppeals,
            InitializedAtUtc = register.InitializedAtUtc,
            InitializedByUserId = register.InitializedByUserId,
            InitializedByName = register.InitializedByName,
            RecipientSnapshotHash = register.RecipientSnapshotHash,
            IntegrityHash = register.IntegrityHash,
            RowVersion = Convert.ToBase64String(register.RowVersion ?? []),
            Recipients = recipients,
            AllowedActions = externalPartnerIds is null ? ["ApproveLetter", "RegisterSecurity"] : [],
            BlockedReasons = externalPartnerIds is null && latestReadiness is null
                ? ["The retained award-readiness decision is unavailable."]
                : []
        };
    }

    private static ProcurementBidderCommunicationRecipientDto MapRecipient(
        ProcurementBidderCommunicationRecipient item,
        ProcurementBidderCommunicationRegister register)
    {
        var now = DateTime.UtcNow;
        return new ProcurementBidderCommunicationRecipientDto
        {
            Id = item.Id,
            BusinessPartnerId = item.BusinessPartnerId,
            Outcome = item.Outcome,
            BidOrQuoteIds = DeserializeIds(item.BidOrQuoteIdsJson),
            PartnerCode = item.PartnerCode,
            PartnerName = item.PartnerName,
            RecipientEmail = item.RecipientEmail,
            RecipientPhone = item.RecipientPhone,
            LineageHash = item.LineageHash,
            IntegrityHash = item.IntegrityHash,
            LetterVersions = item.LetterVersions.OrderByDescending(letter => letter.Version)
                .Select(MapLetter).ToList(),
            Appeals = item.Appeals.OrderByDescending(appeal => appeal.Sequence)
                .Select(MapAppeal).ToList(),
            SecurityInstruments = item.SecurityInstruments.OrderByDescending(security =>
                    security.RegisteredAtUtc)
                .Select(security => MapSecurity(security, item, register)).ToList(),
            AllowedActions = item.Outcome == ProcurementBidderCommunicationRecipientOutcome.Unsuccessful &&
                             now <= register.AppealWindowEndsAtUtc
                ? ["Acknowledge", "FileAppeal"]
                : ["Acknowledge"]
        };
    }

    private static ProcurementBidderCommunicationLetterVersionDto MapLetter(
        ProcurementBidderCommunicationLetterVersion item) => new()
    {
        Id = item.Id,
        Version = item.Version,
        TemplateVersionId = item.TemplateVersionId,
        TemplateReference = item.TemplateReference,
        TemplateChecksumSha256 = item.TemplateChecksumSha256,
        ContentReference = item.ContentReference,
        ContentChecksumSha256 = item.ContentChecksumSha256,
        WorkflowDefinitionId = item.WorkflowDefinitionId,
        WorkflowInstanceId = item.WorkflowInstanceId,
        ApprovalReference = item.ApprovalReference,
        ApprovalEvidenceReference = item.ApprovalEvidenceReference,
        ApprovedAtUtc = item.ApprovedAtUtc,
        ApprovedByUserId = item.ApprovedByUserId,
        ApprovedByName = item.ApprovedByName,
        IntegrityHash = item.IntegrityHash,
        Dispatches = item.Dispatches.OrderByDescending(dispatch => dispatch.Sequence)
            .Select(MapDispatch).ToList()
    };

    private static ProcurementBidderCommunicationDispatchDto MapDispatch(
        ProcurementBidderCommunicationDispatch item) => new()
    {
        Id = item.Id,
        Sequence = item.Sequence,
        Channel = item.Channel,
        Destination = item.Destination,
        DispatchReference = item.DispatchReference,
        DispatchEvidenceReference = item.DispatchEvidenceReference,
        DispatchedAtUtc = item.DispatchedAtUtc,
        DispatchedByUserId = item.DispatchedByUserId,
        DispatchedByName = item.DispatchedByName,
        IntegrityHash = item.IntegrityHash,
        Deliveries = item.Deliveries.OrderBy(delivery => delivery.Sequence)
            .Select(MapDelivery).ToList(),
        Acknowledgements = item.Acknowledgements.OrderBy(ack => ack.Sequence)
            .Select(MapAcknowledgement).ToList()
    };

    private static ProcurementBidderCommunicationDeliveryDto MapDelivery(
        ProcurementBidderCommunicationDelivery item) => new()
    {
        Id = item.Id,
        Sequence = item.Sequence,
        Outcome = item.Outcome,
        OccurredAtUtc = item.OccurredAtUtc,
        ProviderReference = item.ProviderReference,
        Detail = item.Detail,
        EvidenceReference = item.EvidenceReference,
        IntegrityHash = item.IntegrityHash
    };

    private static ProcurementBidderCommunicationAcknowledgementDto MapAcknowledgement(
        ProcurementBidderCommunicationAcknowledgement item) => new()
    {
        Id = item.Id,
        Sequence = item.Sequence,
        Outcome = item.Outcome,
        AcknowledgedAtUtc = item.AcknowledgedAtUtc,
        AcknowledgedByUserId = item.AcknowledgedByUserId,
        AcknowledgedByBusinessPartnerId = item.AcknowledgedByBusinessPartnerId,
        AcknowledgementChannel = item.AcknowledgementChannel,
        AcknowledgementReference = item.AcknowledgementReference,
        EvidenceReference = item.EvidenceReference,
        IntegrityHash = item.IntegrityHash
    };

    private static ProcurementBidderAppealDto MapAppeal(ProcurementBidderAppeal item) => new()
    {
        Id = item.Id,
        Sequence = item.Sequence,
        Grounds = item.Grounds,
        EvidenceReference = item.EvidenceReference,
        FiledAtUtc = item.FiledAtUtc,
        FiledByUserId = item.FiledByUserId,
        FiledByBusinessPartnerId = item.FiledByBusinessPartnerId,
        IntegrityHash = item.IntegrityHash,
        Decision = item.Decisions.Where(decision => !decision.IsDeleted)
            .Select(MapAppealDecision).SingleOrDefault()
    };

    private static ProcurementBidderAppealDecisionDto MapAppealDecision(
        ProcurementBidderAppealDecision item) => new()
    {
        Id = item.Id,
        Outcome = item.Outcome,
        Reason = item.Reason,
        WorkflowDefinitionId = item.WorkflowDefinitionId,
        WorkflowInstanceId = item.WorkflowInstanceId,
        DecisionReference = item.DecisionReference,
        EvidenceReference = item.EvidenceReference,
        DecidedAtUtc = item.DecidedAtUtc,
        DecidedByUserId = item.DecidedByUserId,
        DecidedByName = item.DecidedByName,
        IntegrityHash = item.IntegrityHash
    };

    private static ProcurementTenderSecurityInstrumentDto MapSecurity(
        ProcurementTenderSecurityInstrument item,
        ProcurementBidderCommunicationRecipient recipient,
        ProcurementBidderCommunicationRegister register)
    {
        var actions = item.Actions.Where(action => !action.IsDeleted)
            .OrderBy(action => action.Sequence).Select(MapSecurityAction).ToList();
        var blocked = new List<string>();
        if (DateTime.UtcNow < register.StandstillEndsAtUtc)
            blocked.Add("The configured standstill period has not elapsed.");
        if (DateTime.UtcNow < register.AppealWindowEndsAtUtc)
            blocked.Add("The configured appeal window has not elapsed.");
        if (recipient.Appeals.Any(appeal => !appeal.IsDeleted &&
            appeal.Decisions.Count(decision => !decision.IsDeleted) != 1))
            blocked.Add("An appeal remains unresolved.");
        if (actions.Count != 0)
            blocked.Add("A terminal security action is already retained.");
        return new ProcurementTenderSecurityInstrumentDto
        {
            Id = item.Id,
            TenderBidId = item.TenderBidId,
            RequestForQuotationQuoteId = item.RequestForQuotationQuoteId,
            InstrumentType = item.InstrumentType,
            InstrumentReference = item.InstrumentReference,
            IssuerName = item.IssuerName,
            Amount = item.Amount,
            CurrencyCode = item.CurrencyCode,
            IssuedAtUtc = item.IssuedAtUtc,
            ExpiresAtUtc = item.ExpiresAtUtc,
            EvidenceReference = item.EvidenceReference,
            RegisteredAtUtc = item.RegisteredAtUtc,
            IntegrityHash = item.IntegrityHash,
            CurrentAction = actions.LastOrDefault()?.ActionType,
            Actions = actions,
            AllowedActions = blocked.Count == 0
                ? recipient.Outcome == ProcurementBidderCommunicationRecipientOutcome.Successful
                    ? ["Release"]
                    : ["Return", "Forfeit"]
                : [],
            BlockedReasons = blocked
        };
    }

    private static ProcurementTenderSecurityActionDto MapSecurityAction(
        ProcurementTenderSecurityAction item) => new()
    {
        Id = item.Id,
        Sequence = item.Sequence,
        ActionType = item.ActionType,
        WorkflowDefinitionId = item.WorkflowDefinitionId,
        WorkflowInstanceId = item.WorkflowInstanceId,
        ActionReference = item.ActionReference,
        Reason = item.Reason,
        EvidenceReference = item.EvidenceReference,
        ActionedAtUtc = item.ActionedAtUtc,
        ActionedByUserId = item.ActionedByUserId,
        ActionedByName = item.ActionedByName,
        IntegrityHash = item.IntegrityHash
    };

    private async Task RecordEventAsync(
        ProcurementBidderCommunicationRegister register,
        string action,
        ProcurementControlEventResult result,
        string idempotencyKey,
        object values,
        string? reason,
        string correlationId,
        DateTime occurredAtUtc,
        IReadOnlyCollection<ProcurementControlEventEvidenceReference> evidence,
        CancellationToken cancellationToken) =>
        await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = ProcurementControlEventKey.Create("bidder-communication",
                register.TenantId, register.Id, action, idempotencyKey),
            EventType = EventType,
            Action = action,
            Result = result,
            DecisionKeys = DecisionKeys.ToList(),
            SourceType = register.SourceType.ToString(),
            SourceId = register.SourceId,
            SourceReference = register.SourceReference,
            Reason = TrimOrNull(reason, 1000),
            InputValues = new
            {
                register.AwardFamily,
                register.AwardId,
                register.AwardReference,
                register.AwardReadinessDecisionId,
                register.AwardReadinessDecisionSequence,
                register.AwardReadinessIntegrityHash,
                register.AwardReadinessSourceIntegrityHash,
                actorUserId = _currentUser.UserId,
                actorRoles = _currentUser.Roles.OrderBy(item => item),
                register.CorrelationId
            },
            ResultValues = values,
            CorrelationId = correlationId,
            CausationId = register.CorrelationId,
            OccurredAtUtc = occurredAtUtc,
            Evidence = evidence.ToList()
        }, cancellationToken);

    private Task PublishAsync(
        string topic,
        ProcurementBidderCommunicationRegister register,
        ProcurementBidderCommunicationRecipient recipient,
        object values,
        CancellationToken cancellationToken)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(
            Serialize(values), JsonOptions) ?? new Dictionary<string, object>();
        data["sourceType"] = register.SourceType.ToString();
        data["sourceId"] = register.SourceId;
        data["sourceReference"] = register.SourceReference;
        data["registerId"] = register.Id;
        data["recipientId"] = recipient.Id;
        data["businessPartnerId"] = recipient.BusinessPartnerId;
        data["recipientEmail"] = recipient.RecipientEmail ?? string.Empty;
        data["recipientPhone"] = recipient.RecipientPhone ?? string.Empty;
        data["recipientOutcome"] = recipient.Outcome.ToString();
        return _notifications.PublishAsync(new NotificationTopicEvent
        {
            TenantId = _currentUser.TenantId,
            TopicKey = topic,
            NotificationType = EventType,
            EntityType = register.SourceType.ToString(),
            EntityId = register.SourceId,
            TriggeredByUserId = _currentUser.UserId,
            Data = data
        }, cancellationToken);
    }

    private static List<ProcurementControlEventEvidenceReference> Evidence(
        string reference,
        Guid? workflowEvidenceDocumentId,
        Guid? fileUploadRecordId,
        string label,
        string requirement)
    {
        var items = new List<ProcurementControlEventEvidenceReference>
        {
            new()
            {
                ReferenceKind = ProcurementControlEvidenceReferenceKind.ExternalReference,
                Reference = reference.Trim(),
                Label = label,
                RequirementKey = requirement
            }
        };
        if (workflowEvidenceDocumentId.HasValue)
            items.Add(new ProcurementControlEventEvidenceReference
            {
                ReferenceKind = ProcurementControlEvidenceReferenceKind.WorkflowEvidenceDocument,
                ReferenceId = workflowEvidenceDocumentId,
                Label = label,
                RequirementKey = requirement
            });
        if (fileUploadRecordId.HasValue)
            items.Add(new ProcurementControlEventEvidenceReference
            {
                ReferenceKind = ProcurementControlEvidenceReferenceKind.FileUploadRecord,
                ReferenceId = fileUploadRecordId,
                Label = label,
                RequirementKey = requirement
            });
        return items;
    }

    private static void ValidateDestination(
        ProcurementBidderCommunicationRecipient recipient,
        ProcurementBidderCommunicationDispatchChannel channel,
        string destination)
    {
        var value = destination.Trim();
        if (channel == ProcurementBidderCommunicationDispatchChannel.Email &&
            !string.Equals(value, recipient.RecipientEmail, StringComparison.OrdinalIgnoreCase))
            throw Validation("BIDDER_LETTER_DESTINATION_MISMATCH",
                "Email dispatch must use the server-derived recipient email.");
        if (channel == ProcurementBidderCommunicationDispatchChannel.Sms &&
            !string.Equals(NormalizePhone(value), NormalizePhone(recipient.RecipientPhone),
                StringComparison.Ordinal))
            throw Validation("BIDDER_LETTER_DESTINATION_MISMATCH",
                "SMS dispatch must use the server-derived recipient phone.");
        if (channel == ProcurementBidderCommunicationDispatchChannel.SupplierPortal &&
            value != recipient.BusinessPartnerId.ToString() &&
            !string.Equals(value, "portal", StringComparison.OrdinalIgnoreCase))
            throw Validation("BIDDER_LETTER_DESTINATION_MISMATCH",
                "Portal dispatch must target the server-derived supplier account.");
    }

    private void EnsureInternalReader()
    {
        EnsureAuthenticatedTenant();
        if (_currentUser.IsExternalUser)
            throw Authorization("Supplier portal users must use the supplier-scoped overview.");
        if (HasPlatformSuperAdministratorBypass() ||
            _currentUser.HasRegisteredProcurementPermission("procurement.records.read"))
            return;
        throw Authorization("The procurement records read permission is required.");
    }

    private void EnsureExternalReader()
    {
        EnsureAuthenticatedTenant();
        if (!_currentUser.IsExternalUser)
            throw Authorization("This operation is restricted to supplier portal users.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty ||
            _currentUser.TenantId == Guid.Empty)
            throw Authorization("An authenticated tenant context is required.");
    }

    private bool HasPlatformSuperAdministratorBypass() =>
        _currentUser.HasRole(Constants.Roles.SuperAdmin);

    private string ActorName() =>
        string.IsNullOrWhiteSpace(_currentUser.FullName)
            ? _currentUser.Username
            : _currentUser.FullName;

    private static string NormalizeCorrelation(string correlationId) =>
        string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId.Trim().Length <= 100
                ? correlationId.Trim()
                : correlationId.Trim()[..100];

    private static string NormalizeCurrency(string value)
    {
        var currency = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (currency.Length != 3 || currency.Any(character => !char.IsLetter(character)))
            throw Validation("TENDER_SECURITY_CURRENCY_INVALID",
                "CurrencyCode must be a three-letter code.");
        return currency;
    }

    private static string NormalizeHash(string value)
    {
        var hash = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (!IsHash(hash))
            throw Validation("BIDDER_COMMUNICATION_HASH_INVALID",
                "A 64-character SHA-256 hash is required.");
        return hash;
    }

    private static bool IsHash(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static void AssertHash(string expected, string actual, string code)
    {
        if (!string.Equals(NormalizeHash(expected), actual,
                StringComparison.OrdinalIgnoreCase))
            throw Conflict(code, "The expected immutable aggregate hash is stale.");
    }

    private static void Require(string? value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw Validation(code, message);
    }

    private static void RequireIdempotency(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw Validation("BIDDER_COMMUNICATION_IDEMPOTENCY_REQUIRED",
                "IdempotencyKey is required.");
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

    private static string Serialize(object value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static List<Guid> DeserializeIds(string json) =>
        JsonSerializer.Deserialize<List<Guid>>(json, JsonOptions) ?? [];

    private static string? TrimOrNull(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().Length <= maximumLength
                ? value.Trim()
                : value.Trim()[..maximumLength];

    private static string NormalizePhone(string? value) =>
        new((value ?? string.Empty).Where(character => char.IsDigit(character) || character == '+')
            .ToArray());

    private static ProcurementBidderCommunicationNotFoundException NotFound(
        string code,
        string message) => new(code, message);
    private static ProcurementBidderCommunicationConflictException Conflict(
        string code,
        string message) => new(code, message);
    private static ProcurementBidderCommunicationValidationException Validation(
        string code,
        string message) => new(code, message);
    private static ProcurementBidderCommunicationAuthorizationException Authorization(
        string message) => new(message);

    private sealed record Candidate(Guid SubjectId, Guid BusinessPartnerId);
    private sealed record ResolvedRecipient(
        Guid BusinessPartnerId,
        ProcurementBidderCommunicationRecipientOutcome Outcome,
        List<Guid> SubjectIds,
        string PartnerCode,
        string PartnerName,
        string? Email,
        string? Phone,
        string LineageHash);
    private sealed record SourceResolution(
        ProcurementAwardReadinessSourceType SourceType,
        Guid SourceId,
        string SourceReference,
        ProcurementBidderCommunicationAwardFamily AwardFamily,
        Guid AwardId,
        string AwardReference,
        DateTime AwardedAtUtc,
        List<ResolvedRecipient> Recipients,
        object AwardSnapshot);
}
